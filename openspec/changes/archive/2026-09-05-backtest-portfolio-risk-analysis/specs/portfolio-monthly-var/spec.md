# Delta for Portfolio Monthly VaR

## ADDED Requirements

### Requirement: Backtest-Derived Daily Net Series Are Subject To A Density Gate The Real-Account Path Does Not Apply

For a daily net series produced by the `backtest-portfolio-analytics` dated bridge (not a
real-account `StrategyTrade` series) of `N` dense calendar-day elements, `Percentile(sorted, p)`
does NOT read a single index: it returns the linear INTERPOLATION between
`sorted[floor(p × (N-1))]` and `sorted[ceil(p × (N-1))]`. Sorted ascending, negative-net days
occupy indices `0..negativeDayCount-1`, then zero-net days, then positive-net days — a zero-net day
and a positive-net day both sort ABOVE the negatives and neither can supply the mass a loss
estimate needs. **The gate MUST defend the value that is published, not one index of it**: every
observation the published figure is composed of must itself be a loss, so the read is supported
only when `negativeDayCount >= ceil(p × (N-1)) + 1`.

**Why `ceil(...) + 1` rather than `floor(...) + 1`, and rather than `floor(...) + 2`.** The
superseded `floor(...) + 1` form gated the LOWER index alone. Exactly on that threshold the upper
interpolation endpoint is, by construction, the first NON-negative observation, so the authorised
figure was partly determined by a zero-fill or by a win — measured: 3,860 observations with 193
negative days published `0.0500`, 95% of it drawn from the zero block, and a construction whose
upper endpoint is a win published a NEGATIVE loss magnitude. `ceil(...) + 1` and `floor(...) + 2`
agree for every rank that is not a whole number. They differ when `p × (N-1)` IS a whole number:
there both endpoints are the same index, `Percentile` returns `sorted[floor(p × (N-1))]` verbatim,
no second endpoint exists, and one loss is the entire published figure — so `floor(...) + 2` would
withhold a figure composed exclusively of a genuine loss. The relation MUST be exactly as strict as
the published number requires, and no stricter.

**Sign convention, stated here because every figure below depends on it.** A reported VaR is a
**positive loss magnitude**, not the raw percentile. `VarFromDaily` returns `-p95.Value` / `-p99.Value`
and `ComputeMonthlyVar` returns `-p05.Value`, so a percentile of `-400.19` is published as `400.19`.
Scenarios below assert the PUBLISHED value. Confusing the two is how a raw measurement reaches a
requirement with its sign inverted, which happened to these anchors before this sentence existed.

The daily VaR at confidence `p` (0.05 for VaR95, 0.01 for VaR99) MUST be withheld — represented by
an explicit withheld state, never rendered as a numeric `0` — whenever `negativeDayCount <
ceil(p × (N-1)) + 1`. The gate is evaluated **independently per confidence level**: a series can
support a more extreme percentile while failing to support a less extreme one, because the more
extreme percentile's index reads deeper into the negative block. This is a relation against
`negativeDayCount`, not against the series' non-zero-day share: a series may clear an arbitrary
non-zero-day threshold while its negative-day count still fails the relation above, and such a
series MUST still be withheld. This gate MUST NOT be applied to real-account
`StrategyTrade`-derived series; that shipped path and its known calendar-dense bias (this spec's
existing note) remain unchanged and out of scope.

The 30-calendar-day rolling monthly VaR95 estimator is subject to the same percentile-index
relation, evaluated over the rolling window-sum count: for `M` window sums, withhold when
`negativeWindowCount < ceil(p × (M-1)) + 1`. This check lives **inside** `ComputeMonthlyVar`
itself, selected by a required policy parameter — **not** in the backtest adapter, and **not** as
a re-derivation of the count on a second code path. `ComputeMonthlyVar` MUST NOT be described as
"reused unchanged": what MUST hold is that the **live path's output through it remains
bit-identical** to its pre-change value, not that the function itself is untouched. The
30-calendar-day **horizon**, the 90-day `MinHistoryDays` **history floor**, and the 5th-percentile
**method** are unchanged regardless of source — but the **density gate itself is not** uniform
across sources: it is evaluated (`RequireSupport`) on the backtest path and never evaluated
(`Unconditional`, so a sparse real account still reports its number) on the real-account path. On
both committed fixtures the backtest-path gate does not fire: the monthly estimate is produced,
not withheld (measured below). A gate that does not fire on today's fixtures is still correct to
have — it is what withholds a legitimately sparser monthly series that no fixture here exhibits.

#### Scenario: Daily VaR95 withheld — IST fixture
- GIVEN the `ListOfTrades_XAUUSD_H1_IST.csv` fixture's dense daily net series (`N` = 3,860 days, 164 negative-net days = 4.25%, 318 non-zero-net days = 8.24%)
- WHEN the daily VaR95 is requested
- THEN it is withheld, because `negativeDayCount` (164) is below `ceil(0.05 × 3859) + 1` (194)

#### Scenario: Daily VaR95 withheld — OOST fixture
- GIVEN the population matching `ListOfTrades_XAUUSD_H1_OOST.csv`'s dense daily net series (`N` = 3,804 days, 172 negative-net days = 4.52%, 320 non-zero-net days = 8.41%)
- WHEN the daily VaR95 is requested
- THEN it is withheld, because `negativeDayCount` (172) is below `ceil(0.05 × 3803) + 1` (192)

#### Scenario: VaR99 reports while VaR95 is withheld on the same run
- GIVEN the IST fixture's dense daily net series (`N` = 3,860 days, 164 negative-net days)
- WHEN both VaR95 (`p` = 0.05) and VaR99 (`p` = 0.01) are requested
- THEN VaR99 is published as `199.4423` — because 164 ≥ `ceil(0.01 × 3859) + 1` = 40, and BOTH indices it interpolates between (`sorted[38] = -199.46`, `sorted[39] = -199.43`) are losses — while VaR95 is withheld (164 < 194): one run, two verdicts, because the gate is evaluated per confidence level and not as a single blanket rule

> `199.4423`, not `199.46`. `Percentile` computes `rank = 0.01 × 3859 = 38.59`, which is fractional,
> so it INTERPOLATES between `sorted[38] = -199.46` and `sorted[39] = -199.43`. An earlier version of
> this scenario said "`sorted[38]`, negated", which is the value at the lower index rather than the
> value the function returns. The shortcut is only safe when the rank lands on a whole index or both
> neighbours are equal — which is exactly why it survived unnoticed in the VaR95 case, where
> `rank = 192.95` interpolates between two zeros and gives `0.00` either way.

#### Scenario: Clearing a non-zero-day threshold does not clear the gate
- GIVEN a dense daily net series whose non-zero-day share is 8%+ (both fixtures above qualify) but whose negative-day count is below `ceil(0.05 × (N-1)) + 1`
- WHEN the daily VaR95 is requested
- THEN it is still withheld — the gate is evaluated against `negativeDayCount`, never against the non-zero-day share

#### Scenario: Monthly gate exists and does not fire — IST fixture
- GIVEN the IST fixture's 30-day rolling window sums (`M` = 3,831 windows, 1,148 negative windows)
- WHEN the monthly VaR95 is computed
- THEN the gate clears (`1,148 >= ceil(0.05 × 3,830) + 1 = 193`) and the estimate is published as `400.19` — the percentile is `-400.19`, negated into a positive loss magnitude

#### Scenario: Monthly gate exists and does not fire — OOST fixture
- GIVEN the population matching `ListOfTrades_XAUUSD_H1_OOST.csv`'s 30-day rolling window sums (`M` = 3,775 windows, 1,203 negative windows)
- WHEN the monthly VaR95 is computed
- THEN the gate clears (`1,203 >= ceil(0.05 × 3,774) + 1 = 190`) and the estimate is published as `378.62` — the percentile is `-378.62`, negated into a positive loss magnitude

#### Scenario: A figure the lower index alone would authorise is withheld
- GIVEN a dense series of 3,860 observations holding exactly 193 negative-net days — one short of `ceil(0.05 × 3859) + 1` and exactly the count the superseded relation called supported
- WHEN the VaR95 is requested
- THEN it is withheld, because the published figure would be `0.0500`: `Percentile` interpolates between `sorted[192] = -1` and `sorted[193] = 0` at weight `0.95`, so 95% of it comes from the zero block
- AND the same construction whose `sorted[193]` is a WIN rather than a zero is withheld for the same reason, instead of publishing a NEGATIVE loss magnitude

#### Scenario: A whole-number rank needs only the one observation it publishes
- GIVEN a dense series of 101 observations holding exactly 6 negative-net days, where `0.05 × 100 = 5` is a whole number
- WHEN the VaR95 is requested
- THEN it is published, because `Percentile` returns `sorted[5]` verbatim with no interpolation and `sorted[5]` is one of the 6 losses — the gate MUST NOT withhold a figure every part of which is a loss

#### Scenario: Real-account daily VaR is unchanged
- GIVEN a real-account `StrategyTrade` daily net series with the same sparse density
- WHEN its daily VaR95 is computed
- THEN the shipped output (including a possible `0.00`) is unchanged by this gate
