# Delta for Portfolio Monthly VaR

## ADDED Requirements

### Requirement: Backtest-Derived Daily Net Series Are Subject To A Density Gate The Real-Account Path Does Not Apply

For a daily net series produced by the `backtest-portfolio-analytics` dated bridge (not a
real-account `StrategyTrade` series) of `N` dense calendar-day elements, `Percentile(sorted, p)`
reads `sorted[floor(p × (N-1))]`. Sorted ascending, negative-net days occupy indices
`0..negativeDayCount-1`, then zero-net days, then positive-net days — a positive-net day sorts
ABOVE the zero block and can never supply the mass that index needs. That index holds a negative
value only when `negativeDayCount >= floor(p × (N-1)) + 1`.

The daily VaR at confidence `p` (0.05 for VaR95, 0.01 for VaR99) MUST be withheld — represented by
an explicit withheld state, never rendered as a numeric `0` — whenever `negativeDayCount <
floor(p × (N-1)) + 1`. The gate is evaluated **independently per confidence level**: a series can
support a more extreme percentile while failing to support a less extreme one, because the more
extreme percentile's index reads deeper into the negative block. This is a relation against
`negativeDayCount`, not against the series' non-zero-day share: a series may clear an arbitrary
non-zero-day threshold while its negative-day count still fails the relation above, and such a
series MUST still be withheld. This gate MUST NOT be applied to real-account
`StrategyTrade`-derived series; that shipped path and its known calendar-dense bias (this spec's
existing note) remain unchanged and out of scope.

The 30-calendar-day rolling monthly VaR95 estimator is subject to the same percentile-index
relation, evaluated over the rolling window-sum count: for `M` window sums, withhold when
`negativeWindowCount < floor(p × (M-1)) + 1`. This check lives **inside** `ComputeMonthlyVar`
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
- THEN it is withheld, because `negativeDayCount` (164) is below `floor(0.05 × 3859) + 1` (193)

#### Scenario: Daily VaR95 withheld — OOST fixture
- GIVEN the population matching `ListOfTrades_XAUUSD_H1_OOST.csv`'s dense daily net series (`N` = 3,804 days, 172 negative-net days = 4.52%, 320 non-zero-net days = 8.41%)
- WHEN the daily VaR95 is requested
- THEN it is withheld, because `negativeDayCount` (172) is below `floor(0.05 × 3803) + 1` (191)

#### Scenario: VaR99 reports while VaR95 is withheld on the same run
- GIVEN the IST fixture's dense daily net series (`N` = 3,860 days, 164 negative-net days)
- WHEN both VaR95 (`p` = 0.05) and VaR99 (`p` = 0.01) are requested
- THEN VaR99 is reported as `sorted[38]` (164 ≥ `floor(0.01 × 3859) + 1` = 39) while VaR95 is withheld (164 < 193) — one run, two verdicts, because the gate is evaluated per confidence level, not as a single blanket rule

#### Scenario: Clearing a non-zero-day threshold does not clear the gate
- GIVEN a dense daily net series whose non-zero-day share is 8%+ (both fixtures above qualify) but whose negative-day count is below `floor(0.05 × (N-1)) + 1`
- WHEN the daily VaR95 is requested
- THEN it is still withheld — the gate is evaluated against `negativeDayCount`, never against the non-zero-day share

#### Scenario: Monthly gate exists and does not fire — IST fixture
- GIVEN the IST fixture's 30-day rolling window sums (`M` = 3,831 windows, 1,148 negative windows)
- WHEN the monthly VaR95 is computed
- THEN the gate clears (`1,148 >= floor(0.05 × 3,830) + 1 = 192`) and the estimate is produced as `-400.19`, not withheld

#### Scenario: Monthly gate exists and does not fire — OOST fixture
- GIVEN the population matching `ListOfTrades_XAUUSD_H1_OOST.csv`'s 30-day rolling window sums (`M` = 3,775 windows, 1,203 negative windows)
- WHEN the monthly VaR95 is computed
- THEN the gate clears (`1,203 >= floor(0.05 × 3,774) + 1 = 189`) and the estimate is produced as `-378.62`, not withheld

#### Scenario: Real-account daily VaR is unchanged
- GIVEN a real-account `StrategyTrade` daily net series with the same sparse density
- WHEN its daily VaR95 is computed
- THEN the shipped output (including a possible `0.00`) is unchanged by this gate
