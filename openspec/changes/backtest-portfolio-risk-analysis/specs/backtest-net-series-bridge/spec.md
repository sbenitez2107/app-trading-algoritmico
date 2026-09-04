# Backtest Net Series Bridge Specification

## Purpose

Own the consumer-independent guarantees of converting an already-sized backtest series
(`ResizedTradeSeries`, slice 2a) into a dated net projection (`BacktestNetSeries`) that a
portfolio-analytics consumer can bind to: `RowIndex`-lookup pairing against the source trades, the
non-unit-weight refusal that discharges `trade-risk-normalization`'s carried-forward D9
obligation, and accounting for rows the resizer could not scale. These guarantees hold for any
future consumer of an already-sized series, independent of what any particular slice publishes;
`backtest-portfolio-analytics` is this slice's only current consumer and owns the gates,
provenance and reporting surface built on top of this bridge.

## Requirements

### Requirement: The Bridge Pairs Source Trades And Resized Rows By RowIndex Lookup, Not Position

The bridge MUST convert a `ResizedTradeSeries` into a dated net projection by pairing each
resized row with the source `BacktestTrade` sharing the same `RowIndex`, via a dictionary lookup —
never a positional or count comparison. `TradeResizer` emits exactly one `ResizedTrade` per source
trade unconditionally (an `Unscalable` outcome is a counter, not an exclusion, at the resizer
stage: `TradeResizer.cs` adds every row regardless of its outcome switch), so every
production `ResizedTradeSeries` today has `resized.Trades.Count == source.Count`. The
lookup-not-position invariant is nonetheless required as a defensive guard: a resized series whose
`RowIndex` values are a strict subset of the source list's — reachable only by a hand-constructed
series today, since no filtering pipeline in this slice produces one — MUST still pair correctly
rather than being refused for its differing count.

#### Scenario: Full-sample pairing
- GIVEN a `ResizedTradeSeries` built from the full held source list, one row per source trade in the same order
- WHEN the bridge converts it
- THEN every dated net is paired to the source trade sharing its `RowIndex`

#### Scenario: A hand-built subset pairs correctly (defensive guard, not a pipeline case)
- GIVEN a hand-constructed `ResizedTradeSeries` with a non-contiguous, strict-subset `RowIndex` set relative to the held source list, built directly for this test rather than derived from any fixture — because no production path in this slice produces a subset today
- WHEN the bridge converts it
- THEN each resized row pairs to the source trade sharing its own `RowIndex`, and the differing row count does not throw or refuse the conversion; a fixture-driven version of this scenario would pass unchanged under a rejected positional zip (every real series has equal counts), so this scenario's guard-correctness is untestable by fixture data and MUST remain labelled defensive

### Requirement: A Pairing Failure Throws; It Is Not A Refusal Status

An unmatched `RowIndex`, or a duplicated `RowIndex` within the held source list (the
concatenated-runs wiring error — a caller that merged rows from two different runs), is a
programming error, not a data condition: the caller constructs both lists itself, exactly as
slice 2a's resizer rejects a non-positive target with `ArgumentOutOfRangeException` rather than a
status. The bridge MUST throw `ArgumentException`, naming the offending `RowIndex`, in exactly
these two cases — never for a differing row count alone (see the pairing requirement above,
including its defensive subset case). This is the opposite contract from the non-unit-weight
refusal below, which MUST remain a status the caller inspects, never a throw — the two MUST NOT be
implemented as the same mechanism.

#### Scenario: Unmatched RowIndex throws
- GIVEN a resized row whose `RowIndex` matches no trade in the held source list
- WHEN the bridge attempts conversion
- THEN it throws `ArgumentException` naming the unmatched `RowIndex`; no dated series and no refusal status is returned

#### Scenario: Duplicated source RowIndex throws
- GIVEN a held source list containing two trades with the same `RowIndex` (e.g. a caller that concatenated rows from two runs)
- WHEN the bridge attempts conversion
- THEN it throws `ArgumentException` naming the duplicated `RowIndex`

#### Scenario: Weight refusal is not a throw
- GIVEN a `ResizedTradeSeries` for a member whose `Weight` is `1.5`
- WHEN the bridge is asked to convert it
- THEN it returns a refusal status naming the member and the weight (see the non-unit-weight requirement below), and does not throw

### Requirement: Already-Sized Output Refuses A Non-Unit Weight

The bridge is the first consumer of an already-sized `ResizedTradeSeries` (discharging
`trade-risk-normalization`'s D9 obligation). It MUST refuse the conversion when the member carries
a `PortfolioStrategy.Weight != 1`, and MUST NOT multiply that weight into the already-sized nets.
The refusal MUST identify the member and the offending weight. It MUST NOT be a silent skip, a
weight coerced to 1, or a flag attached to a series that is nonetheless returned and aggregable —
for the same reason a refused normalization run yields no per-trade output at all rather than a
list of `Unavailable` rows. The refusal is unconditional on the value: `1.5` double-sizes and
`0.5` half-sizes, and both are the same error, because the series' `TargetRiskPerTrade` is the
sizing decision and there is no second one to make. Excluding a strategy from a group means not
passing its series.

#### Scenario: Non-unit weight is refused, not applied
- GIVEN a `ResizedTradeSeries` for a member whose `Weight` is `1.5`
- WHEN the bridge is asked to convert it
- THEN the conversion is refused, naming the member and `1.5`; no dated series is produced and `Weight` is never multiplied into an already-sized net

#### Scenario: Unit weight converts
- GIVEN a `ResizedTradeSeries` for a member whose `Weight` is exactly `1`
- WHEN the bridge is asked to convert it
- THEN the dated series is produced and every net equals the resized trade's own net, unscaled

#### Scenario: A zero weight is an error, not an exclusion
- GIVEN a `ResizedTradeSeries` for a member whose `Weight` is `0`
- WHEN the bridge is asked to convert it
- THEN the conversion is refused; a member is excluded by not being passed, never by a weight

### Requirement: Excluded Unscalable Rows Are Counted, Never Contributed As A Net

A row the resizer marked `Unscalable` (`OriginalSize <= 0`) MUST be excluded from the bridge's
dated net series and MUST NOT contribute a `0` net — a zero net is a breakeven trade, a different
claim. This exclusion changes `Nets.Count`, not `ResizedTradeSeries.Trades.Count` (every trade
still gets a `ResizedTrade`; only the bridge's `Nets` projection drops the unscalable rows). The
excluded count (`ExcludedUnscalableCount`) MUST be reported on `SeriesDensityDto`, the DTO that
accompanies every published figure — not duplicated onto each risk or correlation DTO, and not
left only on the internal series — so a reader can reconcile
`TradeCount - ExcludedUnscalableCount == Nets.Count`.

#### Scenario: Unscalable rows are excluded from Nets and counted, not zeroed
- GIVEN a `ResizedTradeSeries` containing rows marked `Unscalable`, where every row still produced a `ResizedTrade`
- WHEN the bridge builds the dated net series
- THEN those rows contribute no net at all to `Nets` (not a `0`), and `SeriesDensityDto.ExcludedUnscalableCount` reports how many were excluded, satisfying `TradeCount - ExcludedUnscalableCount == Nets.Count`
