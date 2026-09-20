# Proposal: Divergence cost decomposition (slice B of layer 0)

> Gated behind the shipped `demo-backtest-comparability` capability (slice A). Backend only.
> Built on `explore.md` in this folder. **This proposal corrects `SIMULATOR_ROADMAP.md`** — see R1.

## Intent

A gold backtest reporting **$6,101.08 gross** corresponds to **$4,591.73 net** on the live demo
account. **24.74% of gross is cost**, swap alone is **16.7%**, and **334 of 661 trades (50.5%)** pay
swap — none of which the backtest models. Against NQ (cost 4.2% of gross, swap on 6 of 47 trades)
this is a different cost regime entirely, so no single correction factor exists.

Slice A can say *whether* two series are comparable. It cannot say *why* they differ. Its
`DemoOnlyCount` is consistent with two opposite diagnoses — the strategy diverged, or the backtest
source data was absent and no trade could have opened. Today an operator reading a divergence has no
way to separate "our data is missing" from "our costs are unmodelled" from "execution is worse than
modelled". Slice B separates them into four named components so the residual that is left is the one
worth acting on.

**Why now**: the arithmetic is confirmed available on gold — `XAUUSD_M1_UTC02` is `Calibrated` at
PointValue 100.0 over 776 samples (band 99.995–100.005), implied point value **100.000** demo vs
**100.170** backtest. Layer 0 blocks layer 1 and every later series crossing.

## Scope

### In Scope

1. **Data coverage** (isolated first) — per month, demo vs backtest trade presence, with an explicit
   non-nullable disclosure that the label is a **presumption from absence, not a proof**.
2. **Swap** — isolated `Σ StrategyTrade.Swap`, which slice A only ever netted into `DemoNetPlBasis`.
3. **Embedded backtest cost** — via `SymbolCalibration.PointValue`, keyed by the **verbatim SQX
   symbol**, with **four** calibration states reported distinctly and **no fallback**.
4. **Execution residual** — computed over the **exact-minute-paired subset only**, with the unpaired
   subsets reported separately and never folded in.
5. A new sealed `CostDecompositionDto` that **composes slice A's `PriceOffsetComparabilityDto` as a
   mandatory input**, carrying its figures forward so a human judges comparability.
6. One read endpoint beside the slice A sibling, plus DI registration.

### Out of Scope

- **Any comparability threshold or cutoff.** See D1 — structurally forbidden, not merely deferred.
- **Naming any instrument in code.** Gold qualifies today; the indices qualify without a code change
  once Darwinex data replaces the Dukascopy source.
- **Ingesting SQX Data Manager gap percentages** — GUI-only, no ingestion path exists (verified).
- **Any staleness / TTL / re-certification rule on `CalibratedAt`** — `NOT FOUND` as a domain concept.
- **Day-grain coverage** — see D3.
- **Frontend readout.** No Angular component, service, or i18n key.
- **The M1-versus-tick intrabar ambiguity** — an independent modelling gap (slice A Non-Goals, KB §4).
- **`PlatformType? SourcePlatform` on `BacktestRun`** — slice C.

## Capabilities

### New Capabilities
- `demo-backtest-cost-decomposition`: decomposes the demo-vs-backtest P/L difference into data
  coverage, swap, embedded backtest cost and an execution residual, each with its own disclosure of
  what it may and may not claim. Named as a follow-on in slice A's Non-Goals.

### Modified Capabilities
- None. `demo-backtest-comparability` is consumed unchanged; `PriceOffsetComparabilityDto` stays
  sealed and is not extended.

## Approach

A second `internal static`, pure calculator in `Infrastructure/Services/`, sibling to
`DemoBacktestComparabilityCalculator` and `SymbolPointValueCalibrator` (slice A D6 deliberately
placed its calculator *beside* `PortfolioAnalyticsCalculator`, not inside it). It receives slice A's
DTO plus trade projections and a calibration row, and returns one sealed Application DTO. No
migration, no write path, no cache, no threshold, no RNG.

### Architecture decisions

**D1 — The comparability gate is a structural ordering constraint, never a computed threshold.**
The calculator takes `PriceOffsetComparabilityDto` as a **mandatory, non-optional, never-recomputed**
parameter. It expresses *"you cannot decompose cost without having already measured comparability for
this strategy and run kind"* — an ordering gate, not a magnitude gate. The decomposition DTO carries
slice A's figures forward so a human reads *"gold: 35 pairs, 0.0023% offset, no monthly structure —
trust it"* against *"NQ: +25.4, 159 of 160 positive, structural — do not"*.

> **A field named anything like `IsComparableEnoughToDecompose` MUST NOT exist.** The magnitude that
> separates gold from the indices — offset as a *percentage of price* — is deliberately not computed
> by slice A (D7 refused the units conversion; hence the `…PriceUnits` naming). Any cutoff on it is
> the fabricated threshold `INDEX.md` §5 forbids, and slice A's tripwire test exists to catch it.

**D2 — Coverage discloses itself as a presumption.** New `Domain/Enums/CoverageBasis.cs` with a
single member `PresumedFromBacktestTradeAbsence = 0`, surfaced as a **computed, non-nullable**
property `CoverageBasis` — the `BreachBasis` / `ComparabilityBasis` mechanism (D6), one member
because there is exactly one basis and no second one can be produced. Per-month classification is a
**computed** property `Coverage` of type `PeriodCoverage` — `NoTradesEitherSide = 0`, `BothSidesTraded`,
`DemoOnlyNoBacktestTrades`, `BacktestOnlyNoDemoTrades` — derived from two zero/non-zero counts, so it
applies no magnitude rule and, being computed, has no stored-default hazard (the codebase has twice
been bitten by an optimistic enum zero: `PlatformType.MT4 = 0`, `FundingService.Other = 0`).

**D3 — Coverage grain is the calendar month, matching slice A.** Agreeing with the exploration. A day
grain would invent a bucket nothing else in the codebase uses, and at day resolution "no backtest
trade" is overwhelmingly "no signal" rather than "no data", which would drown the signal. The
measured evidence is month-block shaped (DAX: a solid gap block across August 2026; NQ: 1–27 August
empty on the backtest side). Each month row exposes the underlying `DemoOpenTimes` /
`BacktestOpenTimes` verbatim — **no timezone conversion, `DateTimeKind` untouched** (slice A D1) — so
a later capability can drill deeper without a second aggregate type existing now.

**D4 — Four calibration states, reported distinctly, no fallback.** New
`Domain/Enums/EmbeddedCostAvailability.cs`: `NoCalibrationRow = 0`, `InsufficientSamples`,
`Inconsistent`, `Calibrated`. `CalibrationStatus` is **not reused directly** because it has only
three members and structurally cannot express "no row at all" — already documented in the codebase
itself at `SymbolCalibration.cs:10`: *"a missing row cannot express 'tried, not enough' vs 'never
tried'"*. The zero member is the state that asserts nothing usable. `PointValue`, `SampleCount`,
`EmbeddedCostEstimate` and the residual are **null unless `Calibrated`** — undefined, never withheld,
never an assumed point value. `CalibratedAt` is reported **verbatim beside the figure**, compared to
nothing; no TTL, recency rule or staleness label is computed anywhere.

**D5 — The residual is computed over the paired subset only.** Unpaired subsets travel beside it as
separate figures, never summed into it. See R1.

**D6 — The residual states its claim boundary on the type.** Even on gold with matching prices it
still carries the M1-versus-tick intrabar ambiguity (KB §4) and unpaired-trade effects. A
non-nullable computed `ResidualBasis` (`PairedSubsetAfterSwapAndEmbeddedCost = 0`) plus XML remarks
state what it may claim (*the same signal produced a different result after swap and embedded cost
are removed*) and what it may not (*it is not slippage; it is not a strategy quality score; it does
not account for the intrabar path assumption*).

**D7 — Determinism.** No RNG, no seed. Month rows ascending by `(Year, Month)`; set membership, not
ordering, decides every bucket. Byte-identical output for identical inputs, pinned by test and by a
file-scoped tripwire, mirroring slice A.

### Rejected alternatives

| # | Rejected | Reason |
|---|---|---|
| **R1** | **Residual over the whole disjoint partition** — as `SIMULATOR_ROADMAP.md` currently specifies | **This proposal corrects the roadmap.** Folding demo-only and backtest-only P/L into one residual mixes *"the same signal produced a different result"* with *"this trade never had a counterpart at all"* — two different questions, and the second is exactly the coverage component that must be isolated *first*. Recorded here so the roadmap's own wording cannot reintroduce it later. |
| R2 | A numeric comparability cutoff (`IsComparableEnoughToDecompose`, offset-% threshold) | No such number exists in any measured or vendor source. `INDEX.md` §5 forbids inventing one; slice A's tripwire test exists to catch exactly this. Replaced by D1's ordering gate. |
| R3 | Hardcoding `XAUUSD` (or an instrument allow-list) | Freezes a measurement into code. The indices qualify without a code change the day Darwinex data replaces the Dukascopy source. |
| R4 | Extending the sealed `PriceOffsetComparabilityDto` | Slice A's spec names this as a *separate* capability in its Non-Goals; extending blurs "comparability diagnostic" into "cost readout" and would drag slice A's tripwires over slice B's surface. |
| R5 | Folding the calculators into `PortfolioAnalyticsCalculator` | Slice A D6 deliberately placed its calculator beside it, not inside it. |
| R6 | Falling back to an assumed point value when calibration is absent or not `Calibrated` | Publishes a fabricated number that nothing flags. Null means the arithmetic is undefined (slice A D5). |
| R7 | A staleness/TTL rule on `CalibratedAt` | `NOT FOUND` as a domain concept — verified: all six references are writes, storage config or projections; nothing compares it. Inventing a cutoff is `INDEX.md` §5 again. |
| R8 | Day-grain coverage | See D3. |
| R9 | Presenting the coverage label as a confirmed data gap | The only independent corroboration is SQX's Data Manager gap percentage, which has **no ingestion path** (verified: GUI-only). Presumption from absence, disclosed as such (D2). |

## Affected modules by layer

| Layer | Path | Impact |
|---|---|---|
| Domain | `Domain/Enums/CoverageBasis.cs` | New — `PresumedFromBacktestTradeAbsence = 0` |
| Domain | `Domain/Enums/PeriodCoverage.cs` | New — 4 members, `NoTradesEitherSide = 0` |
| Domain | `Domain/Enums/EmbeddedCostAvailability.cs` | New — 4 states, `NoCalibrationRow = 0` |
| Domain | `Domain/Enums/ResidualBasis.cs` | New — `PairedSubsetAfterSwapAndEmbeddedCost = 0` |
| Domain | `Domain/Enums/CostDecompositionStatus.cs` | New — default asserts nothing |
| Application | `Application/DTOs/Divergence/CostDecompositionDto.cs` | New — root + `CoverageMonthDto`, `SwapComponentDto`, `EmbeddedCostComponentDto`, `ExecutionResidualDto` |
| Application | `Application/Interfaces/ICostDecompositionReadService.cs` | New — one method, `BacktestRunKind` required (slice A D3) |
| Infrastructure | `Infrastructure/Services/DemoBacktestCoverageCalculator.cs` | New — `internal static`, pure |
| Infrastructure | `Infrastructure/Services/CostDecompositionCalculator.cs` | New — `internal static`, pure; takes slice A's DTO as a mandatory parameter |
| Infrastructure | `Infrastructure/Services/CostDecompositionReadService.cs` | New — narrow projections, no entity materialization |
| Infrastructure | `Infrastructure/DependencyInjection.cs` | Modified — one registration |
| WebAPI | `WebAPI/Controllers/StrategyBacktestsController.cs` | Modified — one `[HttpGet("cost-decomposition")]`; existing actions untouched |
| Angular | — | **None.** Backend-only slice. |

**No entity, DbContext, configuration or migration change.** `SymbolCalibration`, `StrategyTrade` and
`BacktestTrade` are read as-is.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| A later phase reads "gate on comparability" as licence to add a numeric cutoff | **High** | D1 + R2 stated structurally; a file-scoped "no numeric threshold" tripwire test mirroring slice A's; a reflection test asserting no member name matches `IsComparable*` / `*Score` / `*Grade` |
| The coverage label read as a confirmed data gap | **High** | D2's non-nullable `CoverageBasis` + XML remarks; the presumption cannot be dropped at a call site |
| An assumed point value creeps in as a fallback | Medium | D4 + R6; test pinning null output for each of the three non-`Calibrated` states and for the absent row |
| Sizing overrun (last four slices overran by ≈2×) | **High** | Chained PRs at the coverage seam; `size:exception` already accepted by the maintainer |
| A residual read as "slippage" or as strategy quality | Medium | D6's `ResidualBasis` + explicit claim boundary in remarks |
| Gold-only validation (n = 8 strategies, one instrument, one account) | Certain | Stated as an honesty note on the capability; no instrument named in code (R3) |

## Migration and rollback

**No migration is needed.** Pure additive read path over existing tables — no schema change, no
write path, no backfill, no feature flag.

**Rollback**: delete the new files and revert the two modified files
(`Infrastructure/DependencyInjection.cs`, `WebAPI/Controllers/StrategyBacktestsController.cs`). The
endpoint is additive and nothing calls it until a UI lands (deferred), so rollback is inert for every
existing consumer. If chained PRs are used, each slice rolls back independently at its own seam.

## Testing approach (strict TDD — every case RED first)

`dotnet test` from `app.trading.algoritmico.api`. Integration tooling is not installed
(`config.yaml: integration_tool_installed: false`), so controller and read-service cases are unit
tests over a mocked/SQLite-backed context, matching the existing slice A tests.

| Group | Pins |
|---|---|
| Coverage calculator | month grain; `DemoOnlyNoBacktestTrades` vs `BacktestOnlyNoDemoTrades` vs `BothSidesTraded` vs `NoTradesEitherSide`; open timestamps exposed verbatim with `DateTimeKind` untouched |
| Swap component | isolated `Σ Swap`; a strategy where **no** trade pays swap reports `0`, not null; null only where the subset is empty |
| Embedded cost | one case per calibration state — `Calibrated` (PointValue 100.0, 776 samples), `InsufficientSamples`, `Inconsistent`, **no row at all** — each asserting `PointValue`/estimate/residual are null except when `Calibrated`, and that `CalibratedAt` is echoed verbatim |
| Residual | computed over the paired subset only; a fixture with non-empty demo-only and backtest-only subsets asserts those figures are reported separately and are **not** summands of the residual (**R1 regression guard**) |
| Contract / reflection | `CoverageBasis`, `ResidualBasis` are computed, non-nullable, setter-less; **no member matching `IsComparable*`, `*Score`, `*Grade`, `*Threshold` exists**; slice A's DTO is a required constructor parameter of the calculator |
| Tripwires (file-scoped) | no RNG or seed in any slice file; no numeric threshold constant in any slice file; no instrument literal (`XAUUSD`, `NQ`, `DAX`, `GDAXI`, `USATECH`) in any slice file |
| Determinism | shuffled input returns byte-identical output |
| Endpoint | missing `kind` → 400 (slice A D3, never defaulted); valid request → 200 carrying both bases |

### Must not regress

Backend **638/638, 0 warnings**. Nothing on an existing path is modified, so every existing case must
stay **byte-identical**, specifically the slice A suite —
`tests/AppTradingAlgoritmico.UnitTests/Divergence/`: `DemoBacktestComparabilityCalculatorTests`,
`ComparabilityContractTests`, `DemoBacktestComparabilityReadServiceTests`,
`DemoBacktestComparabilityQueryCostTests`, `DisjointSubsetNetPlTests` — plus
`BacktestPortfolioRiskTripwireTests` (its file-scoped greps target that slice's file list and do not
see these files; slice B adds its own equivalents). `StrategyBacktestsControllerTests` **gains** cases;
its existing cases stay byte-identical.

## Sizing and slice recommendation

**~600–750 production plus test lines.** `400-line budget risk: High`. The estimate already discounts
the last four slices overrunning by roughly 2× — treat 750 as a floor, not a ceiling.

**Recommendation: two chained PRs, not four.** The maintainer has `size:exception` accepted so this
will not gate, but the honest recommendation is to split.

| PR | Contents | Est. |
|---|---|---|
| **B1 — coverage** | `CoverageBasis`, `PeriodCoverage`, `CostDecompositionStatus`, `CoverageMonthDto`, coverage calculator, read service, DI + controller wiring, disclosure and tripwire tests | ~350–420 |
| **B2 — swap, embedded cost, residual** | `EmbeddedCostAvailability`, `ResidualBasis`, the three component DTOs, the composing calculator, residual and calibration-state tests | ~300–380 |

**The seam is exactly after coverage**, for two reasons. Coverage is the only genuinely autonomous
unit — the roadmap isolates it first, and it is readable on its own without any other component. And
swap, embedded cost and the residual cannot be split further without shipping PRs whose numbers
nobody can read: a residual is arithmetically undefined until swap and embedded cost exist, and
either of those alone answers no question. A four-way split would also duplicate the shared plumbing
(DI, controller, disclosure and tripwire tests) across four reviews.

## Dependencies

- `demo-backtest-comparability` (slice A) — **shipped**, hard prerequisite (D1).
- `SymbolCalibrations` rows populated by `BacktestImportService.UpsertCalibrationAsync` — present for
  `XAUUSD_M1_UTC02`, absent for any never-imported symbol (handled by D4, not blocking).
- **No database access is performed during this change's design or spec phases.** Every measured
  figure already lives in `explore.md` and the knowledge base.

## Success criteria

- [ ] An operator can read, for one strategy and one run kind, how much of the demo-vs-backtest
      difference is coverage, how much is swap, how much is embedded backtest cost, and what residual
      is left — with each component's claim boundary stated on the type.
- [ ] The residual is computed over the paired subset only; demo-only and backtest-only figures are
      reported beside it and are provably not summands (test-pinned).
- [ ] All four calibration states surface distinctly, with no fallback point value anywhere.
- [ ] `CoverageBasis` and `ResidualBasis` are non-nullable and cannot be dropped at any call site.
- [ ] No numeric threshold, no instrument literal, no RNG in any slice file (tripwire-pinned).
- [ ] `CalibratedAt` is reported verbatim and compared to nothing.
- [ ] Backend 638/638 → ≥638, 0 warnings; every slice A test byte-identical.

## Proposal question round (blocked — needs a human)

The executor could not ask interactively. These shape the proposal and should be answered before
`sdd-spec`:

1. **Who reads this, and when?** Is the decomposition a per-strategy diagnostic an operator opens
   when a strategy looks wrong, or the input to an automated strategy filter (as layer 0 in
   `SIMULATOR_ROADMAP.md` suggests)? **Assumed: diagnostic only.** An automated filter would need a
   cutoff, which D1/R2 forbid — so if a filter is intended, that tension must be resolved at the
   product level, not invented in code.
2. **Should the coverage component publish a figure at all when the instrument has never been
   externally checked in the Data Manager?** **Assumed yes, with `CoverageBasis` disclosing the
   presumption** — consistent with slice A D5 ("undefined, never withheld"). The alternative is
   withholding the label until an external check exists, which would make the component unusable
   today.
3. **Is a cross-strategy ranked decomposition wanted in this slice, or per-strategy only?**
   **Assumed per-strategy only**, mirroring slice A, which shipped the per-strategy endpoint and
   deferred the ranked list. A cross-strategy readout carries the "two queries total, not one per
   strategy" constraint recorded in slice A's design.
4. **Two chained PRs at the coverage seam, or one PR under the accepted `size:exception`?**
   **Assumed two**, per the sizing section.
5. **Does the swap component need a per-month breakdown, or is a whole-window total enough?**
   **Assumed whole-window total plus the trades-paying-swap count** (the measured shape: 334 of 661).
   Monthly swap would triple the DTO surface for a component with no known time structure.

## Open questions carried forward

- The residual's magnitude on gold is **unmeasured** — this slice computes it for the first time.
  Nothing here predicts what it will show.
- Slice A's open question about the SQX `Type` vocabulary remains open upstream; it affects pairing,
  which slice B consumes rather than performs.
- Whether the indices become decomposable once the data symbol is rebound to a Darwinex-sourced
  series is a data question, not a code question (R3 keeps the code ready either way).

## Assumptions (flagged)

- **A1** — Gold's `Calibrated` status and PointValue 100.0 hold at implementation time. If a re-import
  changes it, D4 handles every other state without a code change.
- **A2** — `BacktestRun`/`BacktestTrade` carry the verbatim SQX symbol with no broker or instrument
  mapping in between (stated in `explore.md`, confirmed against
  `BacktestImportService.UpsertCalibrationAsync`). If a mapping layer is ever inserted, the
  calibration lookup key must be revisited.
- **A3** — Slice A's exact-minute, same-direction pairing is the correct paired subset for the
  residual. This inherits every caveat slice A records, including that no short trade exists in the
  current data to exercise the direction rule.
- **A4** — The estimate assumes no Angular work and no new entity. If a UI readout is pulled into
  this slice, both the sizing and the PR seam are invalid.
