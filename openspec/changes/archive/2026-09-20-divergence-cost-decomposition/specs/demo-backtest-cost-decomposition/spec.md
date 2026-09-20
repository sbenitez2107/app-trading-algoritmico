# Demo-Backtest Cost Decomposition Specification (Slice B)

## Purpose

Slice A (`demo-backtest-comparability`) can say *whether* two series are comparable. It cannot say
*why* a demo account and its backtest diverge in net result. This capability decomposes that
difference into four named components — data coverage, swap, embedded backtest cost, and an
execution residual — for one strategy and one run kind, so a human reading a divergence can separate
"our data is missing" from "our costs are unmodelled" from "execution is worse than modelled" instead
of guessing at one undifferentiated number.

This is a diagnostic a human reads and judges. It is not, and must never become, an automated filter,
gate, score, or pass/fail classification (measured: no comparability threshold, cost cutoff, or
"acceptable" band exists in any measured or vendor source —
`.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md`, `.agents/knowledge/imox/INDEX.md`
§5).

This capability is gated behind the shipped `demo-backtest-comparability` capability (slice A), named
there as a follow-on in its Non-Goals.

## Definitions

**The window** is the DENSE calendar-month span from the earliest to the latest trade open timestamp
across the UNION of the demo and backtest trade sets for the strategy and run kind being decomposed.
Every calendar month inside that inclusive span produces a row — including an interior month with no
trades on either side — because the endpoints are OBSERVED from the trade data itself, never configured
or defaulted; no "lookback period" setting exists and none may be added. This is a deliberate departure
from `demo-backtest-comparability`'s own coverage-window precedent (slice A's calculator only ever
accumulates a month once a trade lands in it, so a month with zero trades on either side cannot exist
in its output). That "observed months only" shape is REJECTED here: an interior month with no trades on
either side is exactly the signal this capability exists to surface (measured: a DAX case where an
entire month of source data was silently absent). Under an observed-months-only rule that month would
simply never render, reproducing the exact silent-omission failure this capability is built to prevent.
The cost of the dense span is disclosed honestly, not hidden: a month with no trades on either side is
AMBIGUOUS — the strategy may not have signalled that month, or source data may have been absent on both
sides — and that ambiguity is disclosed via `CoverageBasis`, never presented as a confirmed gap (see the
coverage requirement below, which compounds this ambiguity with the existing
`PresumedFromBacktestTradeAbsence` presumption rather than replacing it).

Edge cases:
- **Exactly one trade overall** (across both sides) produces a window of exactly one calendar month.
- **Zero trades on both sides** produces no window and no month rows at all; the capability reports
  `CostDecompositionStatus.NoDemoTrades` or `NoRunForKind` (see below) rather than emitting an empty
  span.
- **One side entirely empty** (e.g. zero backtest trades for the whole run) still derives the window
  from the non-empty side's contribution to the union — the union of an empty set and a non-empty set is
  the non-empty set's own span.
- Deriving the span applies no timezone conversion and leaves `DateTimeKind` untouched on every
  timestamp used to compute it, consistent with design D11 and slice A's D1.

## Requirements

### Requirement: The Decomposition Is Diagnostic Only, Never A Filter, Gate, Score, Or Recommendation

The capability MUST NOT compute, expose, or accept a threshold, cutoff, configurable limit,
"acceptable" band, pass/fail label, grade, or recommendation of any kind, on any component or on the
decomposition as a whole. No field, method, or configuration value may express a limit against which
a component is compared to produce a boolean or ordinal verdict. This is stated positively here
because no such number exists in any measured or vendor source (`INDEX.md` §5), and because slice A's
own tripwire test exists to catch exactly this kind of invention if it is later reintroduced.

> A later phase reading "gate on comparability" as licence to add a numeric cutoff is the specific,
> anticipated failure this requirement exists to catch — see the comparability-gate requirement below.
> There is no such number in any measured or vendor source.

#### Scenario: No threshold or cutoff exists anywhere in the decomposition
- GIVEN the cost decomposition DTO and any component within it
- WHEN it is inspected
- THEN no field, method, or configuration value represents a threshold, cutoff, "acceptable" band, or
  configurable limit, and no code-constant numeric boundary is applied to any component

#### Scenario: No pass/fail, grade, or recommendation is exposed
- GIVEN a produced cost decomposition for any strategy and run kind
- WHEN it is inspected
- THEN no field asserts pass/fail, a grade, a score, or a recommendation; the decomposition exposes
  only the four components and their figures for a human to read and judge

#### Scenario: Adding a cutoff would fail this requirement
- GIVEN a hypothetical future change that adds a numeric cutoff (e.g. "residual < $X is acceptable")
  anywhere in this capability
- WHEN this requirement is checked
- THEN the change violates it, because no such cutoff exists in any measured or vendor source and none
  may be invented here

### Requirement: The Comparability Gate Is A Structural Ordering Dependency, Never A Computed Threshold

The decomposition calculator MUST take slice A's `PriceOffsetComparabilityDto` as a mandatory,
non-optional, never-internally-recomputed input parameter. This expresses "you cannot decompose cost
without having already measured comparability for this exact strategy and run kind" — an ordering
gate, not a magnitude gate. The decomposition output MUST carry slice A's comparability figures
forward verbatim (paired count, per-month offset figures, sign consistency, `ComparabilityBasis`) so a
human reads the decomposition beside the comparability readout it depends on, rather than the
capability silently deciding comparability on the caller's behalf.

> **A field named anything like `IsComparableEnoughToDecompose` MUST NOT exist.** The magnitude that
> would separate a well-behaved instrument from a poorly-behaved one — offset as a percentage of
> price, plus monthly sign structure — is deliberately not computed by slice A (its design decision D7
> refused a units conversion outright, naming its fields `…PriceUnits` precisely because no tick-size
> conversion is performed). Any cutoff derived from that magnitude, inside this capability or any
> caller of it, is exactly the fabricated threshold `INDEX.md` §5 forbids and that slice A's own
> tripwire test exists to prevent. This requirement names the forbidden shape explicitly so a later
> reader who wants to "just add a boolean gate" finds this paragraph first.

#### Scenario: The calculator refuses to run without slice A's comparability DTO
- GIVEN a caller attempting to produce a cost decomposition for a strategy and run kind
- WHEN no `PriceOffsetComparabilityDto` is supplied
- THEN the calculator cannot execute — the parameter is mandatory and non-optional, not defaulted or
  recomputed internally

#### Scenario: Slice A's comparability figures travel with the decomposition
- GIVEN a produced cost decomposition for a strategy and run kind
- WHEN it is inspected
- THEN it carries slice A's `PriceOffsetComparabilityDto` figures (paired count, offset statistics,
  sign consistency, `ComparabilityBasis`) verbatim, unmodified from what slice A computed

#### Scenario: No comparability boolean is ever computed
- GIVEN the cost decomposition DTO and every type it references
- WHEN inspected by name (reflection or manual review)
- THEN no member named `IsComparableEnoughToDecompose`, or matching `IsComparable*`, exists anywhere
  in the decomposition surface

### Requirement: Data Coverage Discloses Itself As A Presumption From Absence, Never As A Proof

For each calendar month in the window (see Definitions above), the capability MUST classify coverage
as one of: `NoTradesEitherSide`, `BothSidesTraded`, `DemoOnlyNoBacktestTrades`, or
`BacktestOnlyNoDemoTrades`, derived purely from the zero/non-zero demo and backtest trade counts for
that month — no magnitude rule, no minimum-count rule. `NoTradesEitherSide` is reachable precisely
because the window is the dense calendar-month span, not the set of months that happen to contain a
trade — an interior month inside the span with zero trades on either side classifies
`NoTradesEitherSide` rather than being omitted. Every readout MUST carry a non-nullable, computed
`CoverageBasis` property with a single member, `PresumedFromBacktestTradeAbsence`, disclosing that a
month labelled as a coverage gap is a presumption drawn from the absence of backtest trades, not a
confirmed finding; for a `NoTradesEitherSide` month, that same disclosure additionally covers the
ambiguity that neither side trading could mean the strategy did not signal that month, or that source
data was absent on one or both sides — `CoverageBasis` MUST NOT be read as applying only to the
one-sided cases. The capability cannot, and MUST NOT claim to, distinguish "the source data was
absent" from "the strategy did not signal" — the only independent corroboration, SQX Data Manager's
gap percentage, has no ingestion path anywhere in the codebase (verified: GUI-only) and is out of
scope. Coverage grain MUST be the calendar month, matching slice A's existing grain; each month row
MUST also expose the underlying demo and backtest open timestamps verbatim (no timezone conversion,
`DateTimeKind` untouched, per slice A's precedent) so a later capability can drill deeper without a
second aggregate type existing now. An empty month (`NoTradesEitherSide`) reports empty timestamp
lists for both sides, not `null` lists.

#### Scenario: A month with demo trades and no backtest trades is labelled, not proven
- GIVEN a calendar month with one or more demo trades and zero backtest trades for the strategy and
  run kind
- WHEN the coverage component is produced
- THEN that month is classified `DemoOnlyNoBacktestTrades`, and the readout's `CoverageBasis` discloses
  `PresumedFromBacktestTradeAbsence`, never asserting the gap as confirmed

#### Scenario: CoverageBasis is present and non-null on every readout
- GIVEN any cost decomposition readout, including one with no coverage gap months
- WHEN it is inspected
- THEN `CoverageBasis` is non-null and cannot be omitted at any call site

#### Scenario: Open timestamps are exposed verbatim without timezone conversion
- GIVEN a month row in the coverage component
- WHEN it is inspected
- THEN the underlying demo and backtest open timestamps for that month are exposed with their
  `DateTimeKind` untouched, with no conversion applied

#### Scenario: No external corroboration is claimed
- GIVEN a coverage readout for any instrument, including one never checked in SQX Data Manager
- WHEN it is inspected
- THEN nothing in the readout claims or implies that the coverage gap was externally confirmed

#### Scenario: An interior month with no trades on either side is classified, not omitted
- GIVEN a strategy and run kind whose earliest and latest open timestamps (across the union of demo
  and backtest trades) span three calendar months, and whose middle month has zero demo trades and
  zero backtest trades
- WHEN the coverage component is produced
- THEN the middle month appears as its own row classified `NoTradesEitherSide`, with `CoverageBasis`
  disclosing the coverage-gap presumption, rather than being absent from the output

#### Scenario: Exactly one trade overall produces a single-month window
- GIVEN a strategy and run kind with exactly one trade across the union of the demo and backtest sets
- WHEN the coverage component is produced
- THEN exactly one month row is produced, for the calendar month containing that trade's open
  timestamp

#### Scenario: One side entirely empty still derives the window from the non-empty side
- GIVEN a strategy and run kind with zero backtest trades and a non-empty demo trade set spanning
  several calendar months
- WHEN the coverage component is produced
- THEN the window spans the demo trade set's earliest to latest open month, each such month is
  classified `DemoOnlyNoBacktestTrades`, and no row is produced outside that span

### Requirement: Swap Is Reported As An Isolated Component, Never Netted Silently

The capability MUST report the sum of `StrategyTrade.Swap` across the demo trade set for the window as
an isolated figure, distinct from slice A's `DemoNetPlBasis` netting (which folds swap into net P/L
without isolating it). The capability MUST also report the count of trades that paid non-zero swap,
alongside the total trade count in that set. A strategy where no trade pays swap MUST report the sum
as `0`, not `null`; `null` is reserved for the case where the underlying trade set itself is empty.

#### Scenario: Swap is reported as its own figure
- GIVEN a strategy's demo trade set for a window
- WHEN the swap component is produced
- THEN it reports `Σ Swap` across that set as a figure distinct from any net P/L figure, plus the count
  of trades paying non-zero swap and the total trade count

#### Scenario: Zero swap-paying trades reports a zero sum, not null
- GIVEN a strategy's demo trade set where no trade paid swap
- WHEN the swap component is produced
- THEN the reported swap sum is `0`, and the swap-paying trade count is `0`

#### Scenario: An empty trade set reports null, not zero
- GIVEN a strategy and window with zero demo trades
- WHEN the swap component is produced
- THEN the reported swap sum is `null`, because the underlying arithmetic is undefined over an empty
  set

### Requirement: Embedded Backtest Cost Surfaces Four Calibration States Distinctly, With No Fallback

The capability MUST classify the embedded-cost readiness of the backtest's symbol calibration as
exactly one of four states — `NoCalibrationRow`, `InsufficientSamples`, `Inconsistent`, or
`Calibrated` — keyed by the verbatim SQX symbol on the `BacktestRun`/`BacktestTrade` records, with no
broker or instrument mapping applied. `NoCalibrationRow` MUST be the state used when no
`SymbolCalibration` row exists for that symbol at all, distinct from a row existing with a status other
than `Calibrated`; the pre-existing three-member `CalibrationStatus` enum cannot express this
distinction and MUST NOT be reused directly for this purpose. `PointValue`, the derived embedded-cost
estimate, and any residual arithmetic that depends on them MUST be `null` in every state except
`Calibrated`. No path may substitute, assume, or fall back to any point value when calibration is
absent or not `Calibrated`. `CalibratedAt` MUST be reported verbatim beside the embedded-cost figure
whenever a calibration row exists, compared to nothing — no staleness, TTL, or recency rule exists as a
domain concept (verified: all references to `CalibratedAt` in the codebase are writes, storage
configuration, or projections; nothing compares it), and none MUST be invented here.

> The new state-carrying type's zero member MUST be `NoCalibrationRow` — the state that asserts
> nothing usable — not `Calibrated`. This codebase has twice been bitten by an optimistic enum zero
> (`PlatformType.MT4 = 0`, `FundingService.Other = 0`), where the CLR default silently asserted a
> specific meaning nobody chose. `Calibrated = 0` being the CLR default is not itself a correctness
> hazard in `BacktestImportService` — `Status` is assigned explicitly on both the insert and update
> paths there (verified) — but placing the safe, non-committal state at zero is the defensive choice
> for any new consumer of the calibration row that has not yet been audited the way
> `BacktestImportService` has.

#### Scenario: No calibration row reports NoCalibrationRow, not a default point value
- GIVEN a backtest symbol with no `SymbolCalibration` row at all
- WHEN the embedded-cost component is produced
- THEN the readiness state is `NoCalibrationRow`, and `PointValue`, the embedded-cost estimate, and any
  dependent residual figure are all `null`

#### Scenario: A calibration row with insufficient samples reports its own state
- GIVEN a `SymbolCalibration` row whose underlying `CalibrationStatus` is `InsufficientSamples`
- WHEN the embedded-cost component is produced
- THEN the readiness state is `InsufficientSamples`, and `PointValue` and the embedded-cost estimate
  are `null`

#### Scenario: An inconsistent calibration reports its own state
- GIVEN a `SymbolCalibration` row whose underlying `CalibrationStatus` is `Inconsistent`
- WHEN the embedded-cost component is produced
- THEN the readiness state is `Inconsistent`, and `PointValue` and the embedded-cost estimate are
  `null`

#### Scenario: A calibrated symbol publishes its figure and CalibratedAt verbatim
- GIVEN a `SymbolCalibration` row for `XAUUSD_M1_UTC02` with `CalibrationStatus.Calibrated`,
  `PointValue` 100.0, and a recorded `CalibratedAt`
- WHEN the embedded-cost component is produced
- THEN the readiness state is `Calibrated`, `PointValue` and the derived embedded-cost estimate are
  published, and `CalibratedAt` is reported verbatim beside them, compared to nothing

#### Scenario: No state ever falls back to an assumed point value
- GIVEN any of the three non-`Calibrated` states
- WHEN the embedded-cost component is produced
- THEN no point value, estimate, or dependent residual figure is substituted, assumed, or defaulted;
  each is reported `null`

### Requirement: The Execution Residual Is Computed Over The Paired Subset Only

The execution residual MUST be computed only over the exact-minute-paired subset produced by slice A's
pairing (the same trades slice A already restricts its offset computation to). Unpaired demo-only and
backtest-only trades and their P/L MUST be reported as separate figures, carried forward from or
alongside slice A's disjoint-partition figures, and MUST NOT be folded, summed, or otherwise combined
into the residual figure under any circumstance. The residual DTO MUST carry a non-nullable computed
`ResidualBasis` property (with a single member for this slice, asserting the residual is the paired
subset's demo-versus-backtest difference after swap and embedded cost are removed) stating on the type
both what the residual may claim and what it may not: it is not slippage, it is not a strategy-quality
score, and it does not account for the M1-versus-tick intrabar path assumption (an independent
modelling gap recorded in slice A's Non-Goals).

> **Rejected: computing the residual over the full disjoint partition (paired + demo-only +
> backtest-only).** `SIMULATOR_ROADMAP.md`'s component table originally specified the residual this
> way. That is rejected here: folding demo-only and backtest-only P/L into one residual figure mixes
> two different questions — "the same signal produced a different result" (what the paired-subset
> residual measures) and "this trade never had a counterpart at all" (what the coverage component
> exists to isolate). Recording this rejection here is necessary because the roadmap's own prior
> wording could otherwise reintroduce it; this document, not the roadmap, is now authoritative on the
> residual's scope.

#### Scenario: The residual is computed only over paired trades
- GIVEN a strategy and window with a non-empty paired subset, plus non-empty demo-only and
  backtest-only subsets
- WHEN the execution residual is computed
- THEN its value derives only from the paired subset's demo-versus-backtest difference after swap and
  embedded cost are removed

#### Scenario: Unpaired subsets are reported separately and are not summands
- GIVEN a fixture with non-empty demo-only and backtest-only subsets alongside a non-empty paired
  subset
- WHEN the cost decomposition is produced
- THEN the demo-only and backtest-only P/L figures are exposed as distinct fields beside the residual,
  and are provably not included in the residual's computed value

#### Scenario: ResidualBasis states the claim boundary on the type
- GIVEN any produced execution residual
- WHEN its `ResidualBasis` and accompanying documentation are inspected
- THEN they state that the residual reflects the paired subset only, after swap and embedded cost are
  removed, and that it is not slippage, not a strategy-quality score, and does not account for the
  intrabar path assumption

### Requirement: No Instrument Is Named In Code

No calculator, DTO, enum, controller, or test in this capability MAY reference a specific instrument
symbol (e.g. `XAUUSD`, `NQ`, `DAX`, `GDAXI`, `USATECH`) as a literal that gates, filters, or special-
cases behavior. Every component MUST operate identically regardless of which instrument's data is
supplied, so that an instrument qualifies for decomposition purely by the shape of its trade and
calibration data, and any instrument's data can flow through the capability the day its underlying
price source changes, with no code change required.

#### Scenario: No instrument literal appears in any production code path
- GIVEN every source file in this capability's calculators, DTOs, enums, and controllers
- WHEN searched for instrument literals
- THEN no such literal is found gating, filtering, or special-casing any computation

#### Scenario: Behavior is identical across instruments
- GIVEN two data sets for two different instruments with the same shape (trade counts, calibration
  state, coverage pattern)
- WHEN each is run through the decomposition
- THEN both produce structurally identical output, differing only in the resulting figures, never in
  which fields are computed or which branches execute

### Requirement: The Calculator Is Deterministic

No computation in this capability MAY use a random number generator or a seed. Identical inputs (the
same demo trades, backtest trades, calibration row, and slice A comparability DTO) MUST produce
byte-identical readouts across repeated runs, matching the standard already required of
`demo-backtest-comparability` and `backtest-portfolio-analytics`.

#### Scenario: Repeated calls are byte-identical
- GIVEN the same demo trades, backtest trades, calibration row, and comparability DTO supplied twice
- WHEN the cost decomposition calculator runs both times
- THEN the returned readout is byte-identical between the two runs

#### Scenario: Month rows are ordered by set membership, not input order
- GIVEN coverage or residual input trades supplied in shuffled order
- WHEN the decomposition is produced
- THEN month rows appear ascending by `(Year, Month)` regardless of the input order, and no ambiguous
  bucket is resolved by ticket order, row order, or nearest-price matching

### Requirement: The Decomposition Status Discloses Which Component Was Producible At All

Before any window, coverage, or cost component can be computed, the capability MUST first establish
that a backtest run of the requested kind exists for the strategy, and that the demo side has at least
one trade. When no run of the requested kind exists, the capability MUST report
`CostDecompositionStatus.NoRunForKind` and produce no window, no coverage rows, and no cost figures —
there is no trade data on the backtest side from which a window could even be derived. When a run of
the requested kind exists but the strategy has zero demo trades, the capability MUST report
`CostDecompositionStatus.NoDemoTrades` and likewise produce no window or coverage rows — deriving a
window from the union of an empty demo set and a non-empty backtest set is legitimate arithmetically
(per the Definitions section's one-side-empty case), but a strategy with literally zero demo trades has
nothing to decompose cost between and MUST NOT be presented as if it had a coverage readout at all.
These two states are checked, and MUST be checked, before the window is derived; `NoTradesEitherSide`
month rows presuppose that a window could be derived from at least one trade on at least one side, so
`NoRunForKind`/`NoDemoTrades` are structurally distinct from, and take priority over, the interior-month
zero-trade case.

#### Scenario: No backtest run of the requested kind reports NoRunForKind with no figures
- GIVEN a strategy and run kind for which no `BacktestRun` exists at all
- WHEN the cost decomposition is produced
- THEN `Status` is `NoRunForKind`, and no window, coverage row, or cost figure is produced

#### Scenario: Zero demo trades reports NoDemoTrades with no figures
- GIVEN a strategy with a `BacktestRun` of the requested kind but zero `StrategyTrade` rows
- WHEN the cost decomposition is produced
- THEN `Status` is `NoDemoTrades`, and no window, coverage row, or cost figure is produced

## Non-Goals (recorded, not designed for)

- **Any comparability threshold or cutoff.** Structurally forbidden by this specification, not merely
  deferred — see the diagnostic-only and comparability-gate requirements above.
- **Naming any instrument in code.** Gold is the first qualifying instrument, not a hardcoded one.
- **Ingesting SQX Data Manager gap percentages.** GUI-only; no ingestion path exists anywhere in the
  codebase (verified).
- **Any staleness, TTL, or re-certification rule on `CalibratedAt`.** Not found as a domain concept;
  none is invented here.
- **Day-grain coverage.** Month grain matches slice A and the measured evidence's shape (gap blocks
  spanning whole months); a day grain would invent a bucket nothing else in the codebase uses.
- **Frontend readout.** This slice is backend-only; no Angular component, service, or i18n key is
  specified here.
- **The M1-versus-tick intrabar ambiguity.** An independent modelling gap recorded in slice A's
  Non-Goals; the residual's `ResidualBasis` discloses it but does not resolve it.
- **`PlatformType? SourcePlatform` on `BacktestRun`.** A separate future slice, unrelated to this
  capability.
- **A cross-strategy ranked decomposition.** This slice specifies a per-strategy readout only,
  mirroring slice A's shipped scope.
