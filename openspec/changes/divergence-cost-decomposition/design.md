# Design: Divergence cost decomposition (slice B of layer 0)

Diagnostic only. No filter, score, pass/fail, threshold, cutoff or "acceptable band" exists anywhere
below. Composes the shipped `demo-backtest-comparability` capability (slice A) unchanged.

## Technical Approach

One read service in `Infrastructure/Services/` resolves the `(StrategyId, Kind)` run slot, projects
both sides **once**, and feeds those in-memory lists to three pure `internal static` computations:
slice A's existing `DemoBacktestComparabilityCalculator.Measure` (re-used verbatim, same assembly),
a new coverage calculator, and a new composing calculator that takes slice A's DTO as a mandatory
first parameter. Output is one sealed Application DTO. No entity, migration, write path, cache or
RNG.

## Architecture Decisions

### D1 — Composition, not extension: CONFIRMED against the code

`PriceOffsetComparabilityDto` is `public sealed record` with a 15-parameter positional constructor
and two computed `…Basis` properties. Extending it would (a) require a positional-arity change that
slice A's byte-identical-output tests and `ComparabilityContractTests` guard, and (b) drag slice A's
reflection tripwires across slice B's member surface. Its own XML doc names
`demo-backtest-cost-decomposition` as the *consumer*. New `CostDecompositionDto` composing it as a
member. Rejected: extension (proposal R4).

### D2 — Slice B declares its own projection type; slice A's `OpenObservation` cannot carry swap

**Correction to the proposal/explore, which assume slice A's projections are reusable as-is.**
`DemoOpensQuery` projects `Profit + Commission + Swap + Taxes` — swap is *already netted away*, and
`OpenObservation` has no field for it. Embedded cost additionally needs `ClosePrice` and `Size`,
also absent. Adding fields to `OpenObservation` would modify a slice A file the proposal declares
untouched.

Slice B therefore declares `internal readonly record struct CostObservation(DateTime OpenTime,
decimal OpenPrice, decimal? ClosePrice, decimal Size, string Type, decimal? NetPl, decimal? Swap)`
in its own file, with its own queries, and maps in memory to `OpenObservation` before calling slice
A's calculator. One demo query and one backtest query serve all components.

**Correction to an earlier draft of this design, verified against the entities:** `Size` is
non-nullable `decimal` on both `StrategyTrade.Size` and `BacktestTrade.Size` — there is no case where
either side lacks a size — so `CostObservation.Size` MUST be `decimal`, not `decimal?`. An earlier
draft of this record marked it nullable without checking the entities; that was wrong and is corrected
here. Every other field is verified directly against the entities: `OpenTime` (`DateTime`, non-null on
both), `OpenPrice` (`decimal`, non-null on both), `ClosePrice` (`decimal?` on `StrategyTrade` — null
while a trade is open — mapped to `decimal?` uniformly, with the backtest side's non-nullable
`ClosePrice` cast to a non-null value), `Type` (`string`, non-null on both, required on
`BacktestTrade`), `Swap` (`decimal`, present only on `StrategyTrade` — `BacktestTrade` has no `Swap`
column at all — hence nullable on `CostObservation`, `null` for every backtest-side instance), and
`NetPl` (a computed figure, not a raw entity field, carried nullable to match slice A's own
`OpenObservation.NetPl` convention).

### D3 — Re-use slice A's calculator in-assembly, not `IDemoBacktestComparabilityReadService`

Injecting the slice A service would repeat the run lookup and re-project both trade sides: 6 DB
commands where 4 suffice (run lookup, demo opens, backtest opens, calibration row) and two
independently-projected copies of the same rows that could disagree. `DemoBacktestComparabilityCalculator`
is `internal` in the *same* assembly, so the direct call costs nothing. Rejected: service-on-service
injection (query-cost duplication; slice A's own `QueryCostTests` establish command budgets as a
pinned concern).

### D4 — Two calculators, not one and not four

| Option | Tradeoff | Decision |
|---|---|---|
| One calculator | B1 would ship a class whose 3/4 surface is unimplemented; coverage tests would drag money fixtures they never read | Rejected |
| Four (coverage, swap, embedded, residual) | Residual is arithmetically *defined by* swap and embedded cost; splitting forces partial results across file boundaries and duplicates fixtures four ways | Rejected |
| **Two** — `DemoBacktestCoverageCalculator` (timestamps only, no money) + `CostDecompositionCalculator` (swap → embedded → residual, one pass) | Matches the only real seam: coverage is input-disjoint from the other three | **Chosen** |

### D5 — The comparability gate is structural ordering, enforced by signature and by reflection

```csharp
internal static class CostDecompositionCalculator
{
    public static CostDecompositionDto Decompose(
        PriceOffsetComparabilityDto comparability,   // mandatory, non-optional, never recomputed
        CoverageComponentDto coverage,
        IReadOnlyList<CostObservation> demo,
        IReadOnlyList<CostObservation> backtest,
        SymbolCalibrationSnapshot? calibration);     // null == no row at all
}
```

`comparability` is non-nullable, non-optional, and never constructed inside the calculator. No
member named `IsComparableEnoughToDecompose` or anything matching `IsComparable*` may exist.

**`SymbolCalibrationSnapshot` (referenced above, defined here — verified against
`Domain/Entities/SymbolCalibration.cs`):**

```csharp
internal readonly record struct SymbolCalibrationSnapshot(
    string Symbol,
    CalibrationStatus Status,
    decimal? PointValue,
    int SampleCount,
    decimal? MinObserved,
    decimal? MaxObserved,
    DateTime CalibratedAt);
```

A 1:1 projection of `SymbolCalibration`'s own fields (`Symbol`, `Status`, `PointValue`, `SampleCount`,
`MinObserved`, `MaxObserved`, `CalibratedAt`), minus `BaseEntity`'s audit columns (`Id` and whatever it
carries), which the calculator has no use for. `CalibratedAt` is carried verbatim — the same
`DateTime` value read off the entity, not re-derived. The snapshot MUST NOT carry any derived or
compared value (no staleness flag, no "is current" boolean, no age computation) — `CalibratedAt` is
compared to nothing anywhere in this capability (design D8), and a snapshot field that pre-computed a
comparison would smuggle exactly the invented staleness rule D8 forbids back in through the projection
type instead of the DTO.

### D6 — Placement follows slice A, including its deviation

New `internal static` calculators and a `public sealed` read service beside
`DemoBacktestComparabilityCalculator.cs`; DTOs in `Application/DTOs/Divergence/`; enums in
`Domain/Enums/`. Two notes against `backend-core.md`: (1) `SymbolPointValueCalibrator` is
`public static`, not `internal static` as `explore.md` §F states — the pattern to copy is the
comparability calculator; (2) the convention lists `[HttpGet]` on a REST controller as a CQRS
anti-pattern, yet slice A shipped `[HttpGet("comparability")]` on `StrategyBacktestsController`.
**Follow slice A** (rule: existing pattern wins); noted, not silently adopted.

### D7 — Embedded cost keying: RE-VERIFIED

`BacktestImportService.UpsertCalibrationAsync` filters `BacktestRuns.Where(r => r.Symbol == symbol)`
and `BacktestTrades.Where(t => t.Symbol == symbol …)`, then writes `SymbolCalibration.Symbol =
symbol` — the same string, no lookup table, no broker or underlying mapping. `SymbolCalibration.cs:14`
states it: *"Verbatim SQX symbol. Unique. Contract-level identity — no underlying/broker mapping."*
Slice B keys on `BacktestRun.Symbol` verbatim. Confirmed; proposal A2 stands.

### D8 — `EmbeddedCostAvailability`, and no fallback

New `Domain/Enums/EmbeddedCostAvailability.cs`: `NoCalibrationRow = 0`, `InsufficientSamples`,
`Inconsistent`, `Calibrated`. `CalibrationStatus` is not reused: it has three members and
`Calibrated = 0`, so it structurally cannot say "no row" and its zero is the *unsafe* one here.
`PointValue`, `SampleCount`, the estimate and the residual are null unless `Calibrated`. A degenerate
backtest row (`Size == 0` or `ClosePrice == OpenPrice`) makes the estimate null for the whole subset
rather than a total silently missing a term — slice A's `NetPlAccumulator` rule. `CalibratedAt` is
echoed verbatim and compared to nothing.

### D9 — `CostDecompositionStatus` is the partialness disclosure (the proposal listed it, never defined it)

`CoverageComponentOnly = 0`, `Decomposed`, `NoDemoTrades`, `NoRunForKind`. Zero is the member that
asserts least, mirroring `ComparabilityReadoutStatus.NoPairedOpens = 0`. This is what makes B1
shippable: B1 emits `CoverageComponentOnly` and its DTO **does not declare** swap, embedded-cost or
residual members at all — a field that does not exist cannot silently report an incomplete residual.
B2 adds the members and the `Decomposed` state purely additively.

### D10 — Residual over the paired subset only

Demo-only and backtest-only figures travel beside it, never summed in. Non-nullable computed
`ResidualBasis.PairedSubsetAfterSwapAndEmbeddedCost`. **Rejected: residual over the full disjoint
partition** (as `SIMULATOR_ROADMAP.md` specifies) — it mixes *"the same signal produced a different
result"* with *"this trade never had a counterpart"*, and the second is exactly the coverage
component isolated first. This design corrects the roadmap.

### D11 — Coverage is a presumption, at month grain

Non-nullable computed `CoverageBasis.PresumedFromBacktestTradeAbsence` (single member);
per-month computed `PeriodCoverage` (`NoTradesEitherSide = 0`, `BothSidesTraded`,
`DemoOnlyNoBacktestTrades`, `BacktestOnlyNoDemoTrades`) derived from two zero/non-zero counts — no
magnitude rule. Each month exposes `DemoOpenTimes` / `BacktestOpenTimes` verbatim, `DateTimeKind`
untouched (slice A D1). Rejected: day grain; rejected: presenting the label as a confirmed gap (SQX
Data Manager figures are GUI-only, no ingestion path).

**Window derivation (resolves the gap the tasks phase surfaced — the proposal/spec used "the window"
without ever defining it):** the window is the DENSE calendar-month span from the earliest to the
latest open timestamp across the UNION of demo and backtest trades for the strategy and run kind.
`DemoBacktestCoverageCalculator` derives `(firstYearMonth, lastYearMonth)` from that union and MUST
emit one `CoverageMonthDto` row for every calendar month in the inclusive range, not only for months a
`Dictionary<(int,int), …>` keyed by observed trades would happen to produce. Endpoints are read off the
trade data only — no configured or defaulted lookback exists, and none may be added; a field or setting
named anything like `LookbackMonths` or `MaxWindow` would violate the diagnostic-only requirement the
same way a numeric cutoff would.

**Rejected: "observed months only" (slice A's own precedent).**
`DemoBacktestComparabilityCalculator` accumulates into `Dictionary<(int Year, int Month),
MonthAccumulator>`, so a month materializes only when a trade lands in it; under that rule
`PeriodCoverage.NoTradesEitherSide` is unreachable by construction; no month can have zero trades on
both sides if months are only born from trades. This shape is deliberately NOT reused here: an interior
month with zero trades on either side is exactly the signal slice B exists to surface (measured: a DAX
case where a whole month of source data was silently absent). Under observed-months-only, that month
would never render, reproducing the silent-omission failure this capability is built to catch. Do not
reintroduce this shape later as a "consistency with slice A" cleanup — the two capabilities have
opposite goals here: slice A only ever needs to talk about months that have data; slice B needs to talk
about months that conspicuously do not.

**Ambiguity of `NoTradesEitherSide`, disclosed, not resolved:** a month with zero trades on both sides
cannot be distinguished from "the strategy did not signal that month" — the same disclosure boundary
`CoverageBasis` already states for the one-sided cases. `CoverageBasis` compounds this ambiguity rather
than replacing its existing `PresumedFromBacktestTradeAbsence` member — no second enum member is added;
the single existing member's XML remarks are worded to cover both the one-sided and the
zero-trades-both-sides cases.

**Edge cases, specified:**
- One trade overall → a window of exactly one month.
- Zero trades on both sides (the whole strategy/run-kind pair, not one interior month) → no window and
  no month rows at all; this is `CostDecompositionStatus.NoDemoTrades` or `NoRunForKind` (see below),
  checked before window derivation is even attempted, not an empty `CoverageComponentDto`.
- One side entirely empty → the window still derives from the non-empty side's own span; the union of
  an empty set and a non-empty set is the non-empty set's span.
- No timezone conversion is applied when deriving the span; `DateTimeKind` stays untouched on every
  timestamp used, consistent with D1 and this decision.

## Data Flow

```
Controller ──kind (required, no default)──► CostDecompositionReadService
                                                │ 4 DB commands, no entity materialization
                                                │ 1 run lookup · demo CostObservation[] ·
                                                │ backtest CostObservation[] · calibration row?
                                                ▼
                    ┌───────────────────────────┼───────────────────────────┐
     map→OpenObservation                 CoverageCalculator                 │
                    ▼                           ▼                           ▼
   DemoBacktestComparabilityCalculator   CoverageComponentDto      SymbolCalibrationSnapshot?
            (slice A, unchanged)                │                           │
                    └────► PriceOffsetComparabilityDto ──┐                  │
                                                         ▼                  ▼
                                       CostDecompositionCalculator.Decompose(...)
                                                         ▼
                                              CostDecompositionDto
```

## File Changes

| File | Action | PR |
|---|---|---|
| `Domain/Enums/CoverageBasis.cs`, `PeriodCoverage.cs`, `CostDecompositionStatus.cs` | Create | B1 |
| `Application/DTOs/Divergence/CostDecompositionDto.cs` (root + `CoverageComponentDto`, `CoverageMonthDto`) | Create | B1 |
| `Application/Interfaces/ICostDecompositionReadService.cs` | Create — `BacktestRunKind` required (slice A D3) | B1 |
| `Infrastructure/Services/CostObservation.cs` | Create — slice B's projection type (D2) | B1 |
| `Infrastructure/Services/DemoBacktestCoverageCalculator.cs` | Create — `internal static`, pure | B1 |
| `Infrastructure/Services/CostDecompositionReadService.cs` | Create — `public sealed`, narrow `.Select(...)` | B1 |
| `Infrastructure/DependencyInjection.cs` | Modify — one `AddScoped` beside line 104 | B1 |
| `WebAPI/Controllers/StrategyBacktestsController.cs` | Modify — one `[HttpGet("cost-decomposition")]`; existing actions byte-identical | B1 |
| `Domain/Enums/EmbeddedCostAvailability.cs`, `ResidualBasis.cs` | Create | B2 |
| `Application/DTOs/…` — `SwapComponentDto`, `EmbeddedCostComponentDto`, `ExecutionResidualDto` + root members | Modify/Create | B2 |
| `Infrastructure/Services/CostDecompositionCalculator.cs` | Create — takes slice A's DTO mandatorily (D5) | B2 |

## Interfaces / Contracts

Embedded cost, when and only when `Calibrated`: per backtest trade, signed gross =
`(Type is buy ? Close − Open : Open − Close) × Size × PointValue`; estimate = `Σ gross − Σ Profit`
(SQX's `Profit` already contains commission and spread and excludes swap — slice A's
`BacktestNetPlBasis`). Residual = `PairedDemoNetPl − PairedBacktestNetPl − PairedSwap +
EmbeddedCostEstimate`, over the paired subset only, null if any term is null.

## Testing Strategy (strict TDD — one RED test per unit, in this order)

| Layer | What to pin |
|---|---|
| Coverage calculator (B1) | four `PeriodCoverage` cases; month grain; open timestamps verbatim, `DateTimeKind` untouched |
| Read service (B1) | 4-command budget via the `IQueryable<T>`-parameter pattern slice A uses for SQLite; no entity materialization |
| Endpoint (B1) | missing `kind` → 400; valid → 200 carrying `CoverageBasis` and `CostDecompositionStatus.CoverageComponentOnly` |
| Swap (B2) | isolated `Σ Swap`; no-swap strategy reports `0`, not null; null only on an empty subset |
| Embedded cost (B2) | one case per state — `Calibrated`, `InsufficientSamples`, `Inconsistent`, **no row** — each asserting null except when `Calibrated`; `CalibratedAt` echoed verbatim; degenerate row → null |
| Residual (B2) | **R1 regression guard**: a fixture with non-empty demo-only *and* backtest-only subsets asserts those figures are reported and are provably not summands (mutate them; the residual must not move) |
| Determinism | shuffled input → byte-identical output |

### Tripwire test — `CostDecompositionContractTests`

Mirrors `ComparabilityContractTests`, with its **own** `SliceFiles` array (slice A's is a hardcoded
list and does not see these files). Asserts, over comment-stripped source text:

1. no `\bRandom\b`, `\bShuffle\b`, `\b[Ss]eed\s*[:=(]`;
2. no identifier matching `\b(Threshold|Cutoff|Acceptable|Tolerance|Band|MinTrades|MinPaired|MinMonths|MinSamples)\b`
   — a *named list*, not a generic `Min…` regex, because the embedded-cost DTO legitimately echoes
   `MinObserved` / `MaxObserved` from `SymbolCalibration` and a generic pattern would false-positive;
3. no instrument literal: `XAUUSD|GDAXI|USATECH|NDX|"NQ"|"DAX"`;
4. reflection over every slice B DTO: no public property name containing
   `IsComparable`, `Score`, `Grade`, `Rank`, `Pass`, `Fail`, `Threshold`, `Acceptable`;
5. reflection: `CoverageBasis`, `ResidualBasis` are non-nullable, setter-less, and absent from every
   constructor parameter list;
6. reflection: `CostDecompositionCalculator.Decompose` has a parameter of exact type
   `PriceOffsetComparabilityDto` that is **not** optional and **not** nullable — the ordering gate
   enforced as an executable assertion, not a comment.

Must not regress: backend 638/638, 0 warnings; every slice A test in
`tests/AppTradingAlgoritmico.UnitTests/Divergence/` byte-identical.

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Additive read path over existing tables.

## Migration / Rollout

No migration. Rollback = delete new files, revert `DependencyInjection.cs` and the controller. No
consumer exists until a UI lands (deferred), so rollback is inert.

**PR seam validated as coherent.** B1 (~350–420 lines) ships coverage behind a status that names its
own partialness (D9) and a DTO that physically lacks the un-computed members — it discloses
partialness rather than reporting an incomplete residual. B2 (~300–380) is additive. `400-line
budget risk: High`; treat 750 total as a floor.

## Open Questions

- [ ] Residual magnitude on gold is unmeasured — this slice computes it for the first time.
- [ ] Short trades do not exist in the current data, so the buy/sell branch of the signed-gross
      formula (D2/Interfaces) is unit-tested only, never validated against a real short.
- [ ] Slice A's SQX `Type` vocabulary question remains open upstream; slice B consumes that pairing.
