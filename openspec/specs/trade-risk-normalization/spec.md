# Trade Risk Normalization Specification

## Purpose

Derive each backtest trade's dollar risk from realized data — never
configuration — and resize a trade series to an operator-chosen risk per
trade on the lot grid. Scope: risk basis and resizing only. No correlation,
breach probability, VaR, or selector logic; non-SL risk is an interval,
never a measurement.

## Requirements

> **Fixture note.** `ListOfTrades_XAUUSD_H1_OOST.csv` carries two `Sample type`
> values (`IS` 151, `OOS1` 186) and is therefore rejected wholesale by the
> importer's file-level single-sample-type guard. It is a NEGATIVE fixture for
> import. These calculators take already-parsed trades, not a CSV, so scenarios
> below describe a *population matching* that file and its tests load the rows
> directly — they never route it through the import path.

### Requirement: Realized Risk Is Read From MAE, Never From Profit

For an `SL`-closed trade, risk MUST be computed from `|MAE|`. `Profit` MUST
NOT be the source — it carries spread and commission.

#### Scenario: Spread contaminates Profit but not MAE

- GIVEN ticket 1851 in `ListOfTrades_XAUUSD_H1_IST.csv` (MAE -173.76, Profit -174.70)
- WHEN realized risk is computed
- THEN the value is 173.76, not 174.70

### Requirement: Risk Basis Reflects What Is Actually Known

Each trade MUST carry a basis: `Measured` (`CloseType == SL`, `|MAE|` as both
bounds), `Imputed` (other close types, an interval around the run's `Â`),
`Unbounded` (`Size` pinned at the grid's minimum or maximum lot, so one side
of the interval is open), or `Unavailable` (a row whose own data cannot carry
risk at all: `Size <= 0`, or an SL close with null or zero realized risk).
Non-`Measured` risk MUST NOT be presented as a bare number.

`Unavailable` is a per-row label **inside a successful run**. It is not the
state of a refused run: by the requirement below, a run that fails to
estimate `Â` emits no rows at all, so there is nothing left to label.

#### Scenario: TrailingStop is not a loss category

- GIVEN the 96 `TrailingStop` trades of `ListOfTrades_XAUUSD_H1_IST.csv` (74 below 75% of the SL median 196.43, 28 profitable)
- WHEN basis is assigned
- THEN none are `Measured`; each gets an `Imputed` interval, not a point

#### Scenario: Minimum-lot pin breaks the bound upward

- GIVEN a non-SL trade recorded at the grid's minimum lot
- WHEN basis is assigned
- THEN it is `Unbounded`, not `Imputed` — the interval's HIGH side is open, because a clamp up to the minimum lot can only raise realized risk above the target

#### Scenario: The minimum-lot pin is what actually overshoots

- GIVEN a population matching the 95 `SL`-closed trades of `ListOfTrades_XAUUSD_H1_OOST.csv` against a $200 target
- WHEN the 7 trades inconsistent with `Â` are examined
- THEN all 7 sit at the minimum lot 0.10 and realize $229.40–$404.60, while none of the 66 non-pinned trades is inconsistent

### Requirement: Target Risk Is Estimated From The Run, Gated At 85% Consistency

`Â` MUST be the best-supported value across the run's `Measured` trades'
feasible intervals, never the configured risk amount. A run whose `Â` is
consistent with fewer than 85% of `Measured` trades MUST refuse to
normalize, reporting the measured fraction.

The estimate MUST also report the fraction of trades pinned at the grid's
minimum lot. That fraction MUST NOT gate normalization: it measures whether
the grid can express the target, which is a different question from whether
the sizing model fits, and the two separate in opposite directions on the
available fixtures.

#### Scenario: IST fixture clears the gate on its own data

- GIVEN the 90 `SL`-closed trades of `ListOfTrades_XAUUSD_H1_IST.csv`
- WHEN `Â` is estimated
- THEN `Â = 199.98`, supported by 90/90 trades (100%), and the feasible band `[199.98, 200.16)` brackets the configured $200 without the estimator ever returning it

#### Scenario: A coarse grid clears the consistency gate and is flagged by pinning instead

- GIVEN a population matching `ListOfTrades_XAUUSD_H1_OOST.csv` (`Â` = 200.00, 88/95 consistent)
- WHEN normalization is attempted
- THEN it is NOT refused — 93% clears the 85% floor — and `MinLotPinnedFraction` reports 33.8%, against 0.3% for the 2-decimal fixture

#### Scenario: A population below the gate is refused

- GIVEN a population whose `Â` is consistent with fewer than 85% of its `Measured` trades
- WHEN normalization is attempted
- THEN it is refused, `Â` is null, and the measured fraction is reported

### Requirement: Zero SL Closes Refuses Normalization

A run with too few `SL`-closed trades to estimate `Â` MUST refuse to
normalize and MUST NOT fall back to the configured risk amount. A refused
run MUST produce no per-trade output at all — not a collection of
`Unavailable` rows, which a consumer could iterate and aggregate as though
it carried information.

#### Scenario: No SL closes, no fallback

- GIVEN a run with zero `SL`-closed trades
- WHEN normalization is attempted
- THEN it is refused with status `InsufficientSamples`, the profile is null, and no risk value defaults to $200

#### Scenario: A refused run yields nothing to iterate

- GIVEN a run refused for either `InsufficientSamples` or `Inconsistent`
- WHEN the caller inspects the result
- THEN there is no per-trade collection to read, while the estimate still reports the measured fractions that caused the refusal

### Requirement: Resizing Floors And Reports What The Grid Actually Achieves

The resizer MUST floor the computed lot count — never round to nearest —
and MUST report the achieved risk rather than assume the target was hit,
including when the grid pins at the minimum lot or caps at `Maximum Lots`.

The floor MUST be evaluated as a single quotient, `⌊size × target / (Â × step)⌋ × step`.
Computing the scale `target / Â` first rounds it before multiplying, and the
second rounding floors a step low whenever the exact lot count is integral and
the quotient does not terminate.

A row whose `Size` is zero or negative MUST be reported as its own outcome and
MUST NOT be given the minimum lot, which would fabricate a size and count the
row as over-risked while its achieved risk is unknown. A non-positive target
MUST be rejected rather than answered.

#### Scenario: Floor reproduces the original sizing exactly

- GIVEN the 90 SL-closed trades of `ListOfTrades_XAUUSD_H1_IST.csv`
- WHEN resized to `Â = 199.98` on the same grid
- THEN every original `Size` is reproduced and no achieved risk exceeds 199.98

#### Scenario: Minimum lot overshoots a target the grid cannot express

- GIVEN a 1-decimal grid population matching `ListOfTrades_XAUUSD_H1_OOST.csv` (`MinLot = 0.1`, `Â = 200.00`) and a target of **$100**
- WHEN trades are resized
- THEN all 114 rows already sitting at the minimum lot (33.8% of 337) are `RaisedToMinimum`, and the 29 of them carrying `Measured` risk over-risk the target — mean $166.52, max $404.60 — reported as achieved, not as $100

> The target must be **below `Â`** for this to occur at all. At `target = Â` the scale is exactly 1,
> every size is already on the grid, and by D7 the resizer is the identity: `RaisedToMinimumCount`
> is 0 and nothing pins as a *resize* outcome. The 33.8% minimum-lot share of this population is a
> property of its **original** sizing, reported by the estimator as `MinLotPinnedFraction`, and must
> not be attributed to the resizer.

#### Scenario: Maximum Lots caps an oversized target

- GIVEN a target whose implied lot count exceeds `Maximum Lots = 10` for a trade
- WHEN that trade is resized
- THEN `Size` caps at 10 and the achieved (lower) risk is disclosed, not silently capped

### Requirement: Already-Sized Output Refuses A Non-Unit Weight

`ResizedTradeSeries` carries its own `TargetRiskPerTrade` and MUST NOT be assignable to
`PortfolioMemberInput.Trades`, and MUST expose no conversion to a shape that is. That structural
guarantee is what slice 2a delivers and what its tests assert.

The first such consumer is the `backtest-net-series-bridge` capability's `BacktestNetSeries.Bridge`,
which owns the full obligation — the refusal rule, its three weight scenarios (`1.5`, `1`, `0`),
and its tests — as a consumer-independent guarantee (it holds for any future consumer of an
already-sized series, not only this slice's). That capability's "Already-Sized Output Refuses A
Non-Unit Weight" requirement is the single source of truth for this obligation; it is not
duplicated here, to avoid the two copies drifting apart.

(Previously: the obligation was recorded but not verifiable — no consumer existed in slice 2a.
Now discharged by `backtest-net-series-bridge` and asserted by its tests.)

#### Scenario: The obligation is discharged by the bridge capability
- GIVEN a `ResizedTradeSeries` and a `PortfolioStrategy.Weight != 1`
- WHEN `backtest-net-series-bridge`'s `Bridge` is asked to combine them
- THEN the combination is refused per that capability's non-unit-weight requirement; `Weight` is never multiplied into an already-sized net
