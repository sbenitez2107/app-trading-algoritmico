# Tasks: Divergence cost decomposition (slice B of layer 0)

Strict TDD. Every RED test task precedes its GREEN implementation task. Two chained PRs at the
coverage seam (design.md, "PR seam validated as coherent"): **PR B1 — coverage**, **PR B2 — swap,
embedded cost, residual**. Each unit cites the spec requirement/scenario it satisfies.

Pattern to copy throughout: `DemoBacktestComparabilityCalculator.cs`,
`DemoBacktestComparabilityReadService.cs`, `PriceOffsetComparabilityDto.cs`,
`ComparabilityContractTests.cs` (all in `Divergence/`). Read `.claude/conventions/backend-core.md`,
`backend-data.md`, `backend-api.md`, `backend-testing.md` before writing any file below.

---

## PR B1 — Coverage

### Phase 1 — Domain enums

- [ ] **1.1** RED: `CoverageBasisTests` — a test asserting `CoverageBasis` is an enum with exactly one
  member, `PresumedFromBacktestTradeAbsence = 0`.
  _Satisfies: "Data Coverage Discloses Itself As A Presumption From Absence, Never As A Proof" —
  design D2, D11._
- [ ] **1.2** GREEN: create `Domain/Enums/CoverageBasis.cs` — `PresumedFromBacktestTradeAbsence = 0`,
  single member, with XML remarks stating it is a presumption, not a proof.
- [ ] **1.3** RED: `PeriodCoverageTests` — a test asserting `PeriodCoverage` has exactly four members in
  this order: `NoTradesEitherSide = 0`, `BothSidesTraded`, `DemoOnlyNoBacktestTrades`,
  `BacktestOnlyNoDemoTrades`.
  _Satisfies: "Data Coverage Discloses Itself As A Presumption..." — coverage classification list;
  design D2._
- [ ] **1.4** GREEN: create `Domain/Enums/PeriodCoverage.cs` matching 1.3's member order exactly.
- [ ] **1.5** RED: `CostDecompositionStatusTests` — a test asserting `CostDecompositionStatus` has
  `CoverageComponentOnly = 0` as its first member (plus `Decomposed`, `NoDemoTrades`, `NoRunForKind`
  declared but not yet produced by any code in this PR).
  _Satisfies: design D9 (partialness disclosure); no direct spec scenario — flagged below._
- [ ] **1.6** GREEN: create `Domain/Enums/CostDecompositionStatus.cs` — `CoverageComponentOnly = 0`,
  `Decomposed`, `NoDemoTrades`, `NoRunForKind`.

### Phase 2 — Slice B's own projection type (design D2 — unbudgeted in proposal/explore)

- [ ] **2.1** RED: a test on the eventual `CostObservation` shape — assert it is an `internal readonly
  record struct` with members `DateTime OpenTime, decimal OpenPrice, decimal? ClosePrice, decimal
  Size, string Type, decimal? NetPl, decimal? Swap` (`Size` is non-nullable — verified against
  `StrategyTrade.Size` and `BacktestTrade.Size`, both non-nullable `decimal`; corrected from an earlier
  design draft that marked it `decimal?`) (compile-time/reflection assertion; this is new plumbing
  slice A's `OpenObservation` cannot carry — design D2 explicitly rejects extending `OpenObservation`).
  _Satisfies: no single spec scenario directly — this is the projection prerequisite every later
  component (coverage, swap, embedded cost, residual) depends on. Flagged below._
- [ ] **2.2** GREEN: create `Infrastructure/Services/CostObservation.cs` with exactly that shape.
- [ ] **2.3** RED: `CostObservationMappingTests` — a test asserting an in-memory mapping helper from
  `CostObservation` to slice A's `OpenObservation` (OpenTime, OpenPrice, Type, NetPl) preserves those
  four fields verbatim, dropping `ClosePrice`/`Size`/`Swap` only for that call, never mutating them.
  _Satisfies: prerequisite for reusing `DemoBacktestComparabilityCalculator.Measure` unchanged
  (design D3)._
- [ ] **2.4** GREEN: implement the mapping helper (a private/internal static method beside the
  coverage calculator or the read service — do not modify `OpenObservation` or slice A's calculator).

### Phase 3 — Coverage calculator (`DemoBacktestCoverageCalculator`, `internal static`, pure)

- [ ] **3.1** RED: `DemoBacktestCoverageCalculatorTests.BothSidesTraded_WhenMonthHasDemoAndBacktestTrades`
  — a month with ≥1 demo trade and ≥1 backtest trade classifies `BothSidesTraded`.
  _Satisfies: "Data Coverage Discloses Itself..." — classification list._
- [ ] **3.2** RED: `..._DemoOnlyNoBacktestTrades_WhenMonthHasDemoTradesAndNoBacktestTrades` — asserts
  the label AND that the readout's `CoverageBasis` is `PresumedFromBacktestTradeAbsence`.
  _Satisfies: Scenario "A month with demo trades and no backtest trades is labelled, not proven"._
- [ ] **3.3** RED: `..._BacktestOnlyNoDemoTrades_WhenMonthHasBacktestTradesAndNoDemoTrades`.
  _Satisfies: classification list (symmetric case)._
- [ ] **3.4** RED: `..._NoTradesEitherSide_WhenMonthHasNeitherSideTrading` — a fixture with a trading
  month, a gap month with zero trades on both sides, and a later trading month (the window's dense span
  derives from the union of demo/backtest trade months per design D11); asserts the gap month appears
  as its own row classified `NoTradesEitherSide` rather than being omitted from the output.
  _Satisfies: spec "Data Coverage..." Scenario "An interior month with no trades on either side is
  classified, not omitted"; design D11 window derivation._
- [ ] **3.4a** RED: `..._Window_SpansFromEarliestToLatestOpenTimestampAcrossTheUnionOfBothSides` —
  asserts the calculator derives `(firstYearMonth, lastYearMonth)` from the union of demo and backtest
  open timestamps, not from a `Dictionary` keyed only by months that contain a trade (the "observed
  months only" shape design D11 explicitly rejects, citing slice A's own precedent).
  _Satisfies: design D11 window derivation; spec Definitions section._
- [ ] **3.4b** RED: `..._Window_WithExactlyOneTradeOverall_ProducesASingleMonthRow` — a fixture with
  exactly one trade across the union of both sides produces exactly one month row, for the calendar
  month containing that trade's open timestamp.
  _Satisfies: spec Definitions edge case "Exactly one trade overall"; Scenario "Exactly one trade
  overall produces a single-month window"._
- [ ] **3.4c** RED: `..._Window_WithOneSideEntirelyEmpty_StillDerivesFromTheNonEmptySide` — zero
  backtest trades, non-empty demo trade set spanning several months; asserts the window spans the demo
  set's own earliest-to-latest month, each classified `DemoOnlyNoBacktestTrades`, with no row outside
  that span.
  _Satisfies: spec Definitions edge case "One side entirely empty"; Scenario "One side entirely empty
  still derives the window from the non-empty side"._
- [ ] **3.4d** RED: `..._EmptyMonthRow_ReportsEmptyTimestampListsNotNull` — a `NoTradesEitherSide` month
  row exposes empty (not `null`) `DemoOpenTimes`/`BacktestOpenTimes` lists.
  _Satisfies: spec "Data Coverage..." requirement text ("reports empty timestamp lists ... not `null`
  lists")._
- [ ] **3.5** RED: `..._MonthRows_ExposeOpenTimestampsVerbatimWithDateTimeKindUntouched` — asserts
  `DemoOpenTimes`/`BacktestOpenTimes` on a month row carry the exact input `DateTime` values including
  `DateTimeKind`, with no conversion applied.
  _Satisfies: Scenario "Open timestamps are exposed verbatim without timezone conversion"; design D3,
  D11._
- [ ] **3.6** RED: `..._MonthRows_AreOrderedAscendingByYearThenMonthRegardlessOfInputOrder` — shuffled
  input trade order still produces ascending `(Year, Month)` rows.
  _Satisfies: Scenario "Month rows are ordered by set membership, not input order"._
- [ ] **3.7** RED: `..._CoverageBasis_IsNonNullOnEveryReadoutIncludingNoGapMonths` — a fixture with only
  `BothSidesTraded` months still returns a non-null `CoverageBasis`.
  _Satisfies: Scenario "CoverageBasis is present and non-null on every readout"._
- [ ] **3.8** GREEN: implement `Infrastructure/Services/DemoBacktestCoverageCalculator.cs` —
  `internal static`, pure, no I/O — satisfying 3.1–3.7 and 3.4a–3.4d. Derives `(firstYearMonth,
  lastYearMonth)` from the union of demo/backtest open timestamps (design D11 — dense span, not a
  `Dictionary` keyed only by months with trades), emits one row per calendar month in that inclusive
  range, classifies each per D2/D11, and returns a `CoverageComponentDto`.

### Phase 4 — Application DTOs (B1 slice: coverage only — swap/embedded/residual members MUST NOT exist yet)

- [ ] **4.1** RED: `CostDecompositionDtoShapeTests.RootDto_DoesNotDeclareSwapEmbeddedOrResidualMembers`
  — reflection test asserting the B1 `CostDecompositionDto`/`CoverageComponentDto` public surface has
  no property named or typed as swap, embedded cost, or residual (this is the seam contract, not a
  null check — design D9: "a field that does not exist cannot silently report an incomplete
  residual").
  _Satisfies: design D9 directly; no spec scenario names this — flagged below as the seam guard._
- [ ] **4.2** RED: `..._RootDto_ExposesStatusAsCoverageComponentOnlyWhenOnlyCoverageIsProduced`.
  _Satisfies: design D9 (B1 emits `CoverageComponentOnly`)._
- [ ] **4.3** GREEN: create `Application/DTOs/Divergence/CostDecompositionDto.cs` containing, for this
  PR: the root `CostDecompositionDto` (StrategyId, Kind, `CostDecompositionStatus Status`,
  `PriceOffsetComparabilityDto Comparability`, `CoverageComponentDto Coverage` — no swap/embedded/
  residual members), `CoverageComponentDto` (CoverageBasis, IReadOnlyList<CoverageMonthDto>), and
  `CoverageMonthDto` (Year, Month, PeriodCoverage, DemoOpenTimes, BacktestOpenTimes, DemoTradeCount,
  BacktestTradeCount).
- [ ] **4.4** RED: `..._Comparability_CarriesSliceAFiguresVerbatim` — asserts the root DTO's
  `Comparability` member is exactly the `PriceOffsetComparabilityDto` instance/values passed in,
  unmodified.
  _Satisfies: Scenario "Slice A's comparability figures travel with the decomposition"; design D1._
- [ ] **4.5** GREEN: no additional production code expected beyond 4.3 (constructor already carries
  the parameter) — confirm 4.4 passes; note in the task if it does not.

### Phase 5 — Read service and interface

- [ ] **5.1** RED: `ICostDecompositionReadServiceTests` (or a compile-time contract test) — asserts the
  interface's method requires `BacktestRunKind kind` as a non-optional, non-defaulted parameter.
  _Satisfies: design "BacktestRunKind required (slice A D3)"._
- [ ] **5.2** GREEN: create `Application/Interfaces/ICostDecompositionReadService.cs` — one method,
  `Task<CostDecompositionDto> GetAsync(Guid strategyId, BacktestRunKind kind, CancellationToken ct)`
  (or equivalent), `kind` required.
- [ ] **5.3** RED: `CostDecompositionReadServiceTests.GetAsync_WhenNoRunExistsForKind_ReturnsNoRunForKindStatus`
  (SQLite-backed, matching slice A's test infra) — asserts `Status == NoRunForKind` and that no
  window, coverage row, or cost figure is produced, and that this check runs before any window
  derivation is attempted.
  _Satisfies: spec "The Decomposition Status Discloses Which Component Was Producible At All" —
  Scenario "No backtest run of the requested kind reports NoRunForKind with no figures"; design D9._
- [ ] **5.4** RED: `..._GetAsync_WhenRunExistsButNoDemoTrades_ReturnsNoDemoTradesStatus` — asserts
  `Status == NoDemoTrades` and that no window, coverage row, or cost figure is produced.
  _Satisfies: spec "The Decomposition Status Discloses Which Component Was Producible At All" —
  Scenario "Zero demo trades reports NoDemoTrades with no figures"; design D9._
- [ ] **5.5** RED: `..._GetAsync_WhenDataPresent_ReturnsCoverageComponentOnlyStatusWithComparabilityAndCoverage`
  — end-to-end fixture producing a populated `CostDecompositionDto` with `Status ==
  CoverageComponentOnly`.
  _Satisfies: composition of the above scenarios; design "Technical Approach"._
- [ ] **5.6** RED: `CostDecompositionQueryCostTests` — pins the DB command budget for this PR (run
  lookup + demo `CostObservation` query + backtest `CostObservation` query; no calibration query is
  needed for coverage alone, but design D2 says "one demo query and one backtest query serve all
  components" — pin whatever count B1 actually issues, and assert it does not grow with strategy
  trade count), mirroring `DemoBacktestComparabilityQueryCostTests`.
  _Satisfies: design D3 ("6 DB commands where 4 suffice")._
- [ ] **5.7** GREEN: create `Infrastructure/Services/CostDecompositionReadService.cs` — `public
  sealed`, narrow `.Select(...)` projections into `CostObservation` (own queries per design D2, do not
  reuse `DemoBacktestComparabilityReadService.DemoOpensQuery`), maps to `OpenObservation` for the
  slice A calculator call, invokes `DemoBacktestCoverageCalculator`, composes the root DTO with
  `Status = CoverageComponentOnly` (or `NoRunForKind`/`NoDemoTrades` on the early-exit paths).

### Phase 6 — DI and controller wiring

- [ ] **6.1** RED: `DependencyInjectionTests` (or extend the existing one) — asserts
  `ICostDecompositionReadService` resolves to `CostDecompositionReadService` from the container.
  _Satisfies: proposal "DI registration"; design File Changes table._
- [ ] **6.2** GREEN: add one `AddScoped<ICostDecompositionReadService, CostDecompositionReadService>()`
  registration in `Infrastructure/DependencyInjection.cs` beside the existing comparability
  registration (~line 104 per design).
- [ ] **6.3** RED: `StrategyBacktestsControllerTests.GetCostDecomposition_WhenKindMissing_Returns400`
  — mirrors slice A's D3 "never defaulted" rule.
  _Satisfies: spec's endpoint requirement inherited from slice A pattern (proposal Testing approach:
  "missing `kind` → 400")._
- [ ] **6.4** RED: `..._GetCostDecomposition_WhenValid_Returns200WithCoverageComponentOnlyStatus`.
  _Satisfies: proposal Testing approach: "valid request → 200 carrying both bases" (B1 portion:
  `CoverageBasis` + `CostDecompositionStatus.CoverageComponentOnly`)._
- [ ] **6.5** GREEN: add `[HttpGet("cost-decomposition")]` action to
  `WebAPI/Controllers/StrategyBacktestsController.cs` beside the existing comparability action;
  existing actions byte-identical (verify by re-running `StrategyBacktestsControllerTests`'
  pre-existing cases, not editing them).

### Phase 7 — Contract / tripwire tests (B1's own file list — separate from slice A's)

- [ ] **7.1** RED: `CostDecompositionContractTests.Dto_CoverageBasis_IsComputedNonNullableAndHasNoSetter`
  — mirrors `ComparabilityContractTests.Dto_ComparabilityBasis_...`: `CoverageBasis` property is
  non-nullable, setter-less, and absent from every constructor parameter list.
  _Satisfies: Scenario "CoverageBasis is present and non-null on every readout"; spec's structural
  intent that it "cannot be dropped at any call site"._
- [ ] **7.2** RED: `..._Dto_ExposesNoThresholdScoreGradeOrRecommendationMember` — reflection over every
  B1 DTO type; asserts no public property name contains `IsComparable`, `Score`, `Grade`, `Rank`,
  `Pass`, `Fail`, `Threshold`, `Acceptable`.
  _Satisfies: "The Decomposition Is Diagnostic Only..." — all three scenarios; "The Comparability
  Gate Is A Structural Ordering Dependency..." — "No comparability boolean is ever computed"._
- [ ] **7.3** RED: `..._Tripwire_NoSliceFileUsesARandomNumberGeneratorOrSeed` — B1's own `SliceFiles`
  array (its own hardcoded list — do NOT extend slice A's `ComparabilityContractTests.SliceFiles`,
  per design "Tripwire test" note: "slice A's is a hardcoded list and does not see these files").
  List for B1: `Domain/Enums/CoverageBasis.cs`, `PeriodCoverage.cs`, `CostDecompositionStatus.cs`,
  `Application/DTOs/Divergence/CostDecompositionDto.cs`, `Application/Interfaces/
  ICostDecompositionReadService.cs`, `Infrastructure/Services/CostObservation.cs`,
  `Infrastructure/Services/DemoBacktestCoverageCalculator.cs`, `Infrastructure/Services/
  CostDecompositionReadService.cs`.
  _Satisfies: "The Calculator Is Deterministic" — no direct RNG scenario but the required standard;
  design D7._
- [ ] **7.4** RED: `..._Tripwire_NoSliceFileContainsANumericThresholdOrCutoff` — uses the **named
  identifier list** `\b(Threshold|Cutoff|Acceptable|Tolerance|Band|MinTrades|MinPaired|MinMonths|
  MinSamples)\b`, never a generic `Min…` regex (design "Tripwire test" item 2 — a generic pattern
  would false-positive on B2's `MinObserved`/`MaxObserved`; B1 has no such legitimate echo yet, but
  use the same named list now so B2 only adds files, never changes the pattern).
  _Satisfies: "The Decomposition Is Diagnostic Only..." — "No threshold or cutoff exists anywhere"._
- [ ] **7.5** RED: `..._Tripwire_NoSliceFileContainsAnInstrumentLiteral` — greps B1's `SliceFiles` for
  `XAUUSD|GDAXI|USATECH|NDX|"NQ"|"DAX"`.
  _Satisfies: "No Instrument Is Named In Code" — "No instrument literal appears in any production code
  path"._
- [ ] **7.6** GREEN: create `Infrastructure/Services/CostDecompositionContractTests.cs` (or
  `tests/.../Divergence/CostDecompositionContractTests.cs`) satisfying 7.1–7.5. No production code
  change expected from this phase — these are pure tripwires; if any fails, fix the production file
  it targets, not the test.

### Phase 8 — Determinism (B1 scope)

- [ ] **8.1** RED: `DemoBacktestCoverageCalculatorTests.RepeatedCalls_AreByteIdentical` — same demo and
  backtest `CostObservation` lists supplied twice produce a byte-identical `CoverageComponentDto`.
  _Satisfies: Scenario "Repeated calls are byte-identical" (coverage slice)._
- [ ] **8.2** GREEN: no production change expected if 3.8 is already pure; confirm and close.

### Phase 9 — Static gates and full suite (B1)

- [ ] **9.1** Run `dotnet build app.trading.algoritmico.api -warnaserror` (per
  `openspec/config.yaml`: `linter_backend: warnings-as-errors`) — zero warnings.
- [ ] **9.2** Run `dotnet format app.trading.algoritmico.api --verify-no-changes` (per
  `formatter_backend: dotnet format`) — no formatting diffs.
- [ ] **9.3** Run `dotnet test app.trading.algoritmico.api` for the full backend suite. Confirm
  **638/638 pre-existing tests still pass**, plus every new B1 test from Phases 1–8, with **0
  warnings**. Confirm every slice A test in `tests/AppTradingAlgoritmico.UnitTests/Divergence/`
  (`DemoBacktestComparabilityCalculatorTests`, `ComparabilityContractTests`,
  `DemoBacktestComparabilityReadServiceTests`, `DemoBacktestComparabilityQueryCostTests`,
  `DisjointSubsetNetPlTests`) is byte-identical, and `StrategyBacktestsControllerTests`' pre-existing
  cases are byte-identical (it gains cases from 6.3/6.4, nothing else changes).
- [ ] **9.4** If any pre-existing test fails or a warning appears, stop: investigate before patching —
  this PR touches no existing production file's logic (only `DependencyInjection.cs` and the
  controller gain additive lines).

---

## PR B2 — Swap, embedded cost, residual

Depends on B1 shipped. Adds members purely additively to the B1 DTOs (design D9: "B2 adds the members
and the `Decomposed` state purely additively").

### Phase 10 — Domain enums

- [ ] **10.1** RED: `EmbeddedCostAvailabilityTests` — asserts exactly four members in this order:
  `NoCalibrationRow = 0`, `InsufficientSamples`, `Inconsistent`, `Calibrated`.
  _Satisfies: "Embedded Backtest Cost Surfaces Four Calibration States Distinctly..." — states list;
  design D4, D8._
- [ ] **10.2** GREEN: create `Domain/Enums/EmbeddedCostAvailability.cs` matching 10.1's order exactly,
  with XML remarks explaining why `CalibrationStatus` is not reused (design D8: it has three members
  and `Calibrated = 0` is the unsafe zero here).
- [ ] **10.3** RED: `ResidualBasisTests` — asserts `ResidualBasis` has a single member,
  `PairedSubsetAfterSwapAndEmbeddedCost = 0`.
  _Satisfies: "The Execution Residual Is Computed Over The Paired Subset Only" — "ResidualBasis states
  the claim boundary on the type"; design D6, D10._
- [ ] **10.4** GREEN: create `Domain/Enums/ResidualBasis.cs` — single member, with XML remarks stating
  what the residual may and may not claim (not slippage, not a strategy-quality score, does not
  account for the intrabar path assumption).

### Phase 11 — Swap component

- [ ] **11.1** RED: `CostDecompositionCalculatorTests.Swap_ReportsSumOfDemoSwapAcrossTheWindow` — a
  non-empty demo trade set with mixed swap values reports `Σ Swap` as a figure distinct from any net
  P/L figure, plus swap-paying trade count and total trade count.
  _Satisfies: Scenario "Swap is reported as its own figure"._
- [ ] **11.2** RED: `..._Swap_WhenNoTradePaysSwap_ReportsZeroSumAndZeroCount` — not null.
  _Satisfies: Scenario "Zero swap-paying trades reports a zero sum, not null"._
- [ ] **11.3** RED: `..._Swap_WhenDemoTradeSetIsEmpty_ReportsNullSum` — distinguishing this from 11.2.
  _Satisfies: Scenario "An empty trade set reports null, not zero"._
- [ ] **11.4** RED: `CostDecompositionDtoShapeTests.SwapComponentDto_MemberShape` — asserts
  `SwapComponentDto` has `decimal? TotalSwap`, `int SwapPayingTradeCount`, `int TotalTradeCount` (or
  equivalent), and is additive to the B1 root DTO (root DTO gains a `SwapComponentDto? Swap` member,
  non-null only when `Status == Decomposed`).
  _Satisfies: design D9 (additive B2 shape)._
- [ ] **11.5** GREEN: create `SwapComponentDto` in `CostDecompositionDto.cs` and implement the swap
  aggregation inside `CostDecompositionCalculator` (new file, Phase 13) satisfying 11.1–11.4.

### Phase 12 — Embedded cost component

- [ ] **12.1** RED: `..._EmbeddedCost_WhenNoCalibrationRowExists_ReportsNoCalibrationRowWithNullFigures`
  — `PointValue`, estimate, and dependent residual figure all `null`.
  _Satisfies: Scenario "No calibration row reports NoCalibrationRow, not a default point value"._
- [ ] **12.2** RED: `..._EmbeddedCost_WhenCalibrationStatusIsInsufficientSamples_ReportsThatStateWithNullFigures`.
  _Satisfies: Scenario "A calibration row with insufficient samples reports its own state"._
- [ ] **12.3** RED: `..._EmbeddedCost_WhenCalibrationStatusIsInconsistent_ReportsThatStateWithNullFigures`.
  _Satisfies: Scenario "An inconsistent calibration reports its own state"._
- [ ] **12.4** RED: `..._EmbeddedCost_WhenCalibrated_PublishesPointValueEstimateAndCalibratedAtVerbatim`
  — fixture: `XAUUSD_M1_UTC02`, `PointValue` 100.0, 776 samples, a recorded `CalibratedAt`; asserts the
  estimate formula (`Σ gross − Σ Profit`, signed gross = `(Type is buy ? Close − Open : Open − Close)
  × Size × PointValue`) and `CalibratedAt` echoed verbatim, compared to nothing.
  _Satisfies: Scenario "A calibrated symbol publishes its figure and CalibratedAt verbatim";
  Interfaces / Contracts formula._
- [ ] **12.5** RED: `..._EmbeddedCost_ForEveryNonCalibratedState_NeverSubstitutesAnAssumedPointValue` —
  parameterized over all three non-`Calibrated` states, asserting no fallback value appears.
  _Satisfies: Scenario "No state ever falls back to an assumed point value"._
- [ ] **12.6** RED: `..._EmbeddedCost_WhenBacktestRowIsDegenerate_ReportsNullEstimateNotAPartialTotal`
  — `Size == 0` or `ClosePrice == OpenPrice` on a backtest row makes the estimate null for the whole
  subset (slice A's `NetPlAccumulator` rule, design D8).
  _Satisfies: design D8 (no direct spec scenario for the degenerate-row rule — flagged below)._
- [ ] **12.7** RED: `..._EmbeddedCost_KeysOnVerbatimSqxSymbolNoMapping` — two calibration rows with
  different symbols; asserts the lookup matches the `BacktestRun.Symbol` string exactly, no
  normalization or mapping applied.
  _Satisfies: "Embedded Backtest Cost Surfaces Four Calibration States Distinctly..." — keying clause;
  design D7._
- [ ] **12.8** RED: `..._EmbeddedCostComponentDto_MemberShape` — reflection: `EmbeddedCostAvailability
  State`, `decimal? PointValue`, `int? SampleCount`, `decimal? EmbeddedCostEstimate`, `DateTime?
  CalibratedAt` — all nullable except `State`.
  _Satisfies: design D9 (additive shape); "no fallback" scenarios above._
- [ ] **12.9** GREEN: create `EmbeddedCostComponentDto`; implement embedded-cost logic inside
  `CostDecompositionCalculator` satisfying 12.1–12.8. Create `SymbolCalibrationSnapshot` exactly per
  design D5's now-defined shape (`Symbol`, `Status`, `PointValue`, `SampleCount`, `MinObserved`,
  `MaxObserved`, `CalibratedAt` — a 1:1 projection of `SymbolCalibration`, `CalibratedAt` carried
  verbatim, no derived or compared value on the type).

### Phase 13 — Composing calculator and the comparability-gate signature (design D5)

- [ ] **13.1** RED: `..._Decompose_RequiresComparabilityDtoAsAMandatoryNonOptionalParameter` — a
  compile-time/reflection test asserting `CostDecompositionCalculator.Decompose`'s
  `PriceOffsetComparabilityDto comparability` parameter is not optional and not nullable.
  _Satisfies: Scenario "The calculator refuses to run without slice A's comparability DTO"; spec
  requirement "The Comparability Gate Is A Structural Ordering Dependency..."; design D5 signature._
- [ ] **13.2** RED: `..._Decompose_NeverRecomputesComparabilityInternally` — asserts no internal call
  to `DemoBacktestComparabilityCalculator.Measure` exists inside `CostDecompositionCalculator`
  (source-text or dependency inspection) — `comparability` must come from the caller only.
  _Satisfies: same requirement, "never-internally-recomputed" clause._
- [ ] **13.3** RED: `..._RootDto_CarriesComparabilityFiguresVerbatimAlongsideSwapEmbeddedAndResidual` —
  the fully composed B2 `CostDecompositionDto.Comparability` still equals the input DTO verbatim
  (extends 4.4 to the composed path).
  _Satisfies: Scenario "Slice A's comparability figures travel with the decomposition" (composed
  path)._
- [ ] **13.4** GREEN: create `Infrastructure/Services/CostDecompositionCalculator.cs` — `internal
  static`, signature exactly per design D5 (`comparability`, `coverage`, `demo`, `backtest`,
  `calibration`), one pass computing swap then embedded cost then residual, returning the composed
  `CostDecompositionDto` with `Status = Decomposed`.
- [ ] **13.5** GREEN: update `CostDecompositionReadService` to call the new calculator (in addition to
  the coverage calculator from B1) and project the calibration row and swap-bearing fields via
  `CostObservation` (Phase 2's type already carries `Swap`, `ClosePrice`, `Size`).

### Phase 14 — Execution residual

- [ ] **14.1** RED: `..._Residual_IsComputedOnlyOverThePairedSubset` — a fixture with non-empty paired,
  demo-only, and backtest-only subsets; asserts the residual value derives only from the paired
  subset's demo-versus-backtest difference after swap and embedded cost are removed.
  _Satisfies: Scenario "The residual is computed only over paired trades"._
- [ ] **14.2** RED: `..._Residual_UnpairedSubsetsAreReportedButAreNotSummands` — **R1 regression
  guard**: mutate the demo-only and backtest-only P/L figures in the fixture; assert the residual does
  not move.
  _Satisfies: Scenario "Unpaired subsets are reported separately and are not summands"; proposal R1;
  design D10._
- [ ] **14.3** RED: `..._ResidualBasis_IsNonNullableAndStatesTheClaimBoundary` — reflection: the
  `ResidualBasis` property on `ExecutionResidualDto` is non-nullable, setter-less; XML remarks (or a
  documented constant) assert the residual is not slippage, not a strategy-quality score, and does
  not account for the intrabar path assumption.
  _Satisfies: Scenario "ResidualBasis states the claim boundary on the type"._
- [ ] **14.4** RED: `..._ExecutionResidualDto_MemberShape` — `ResidualBasis Basis`, `decimal?
  Residual`, `decimal? DemoOnlyNetPl`, `decimal? BacktestOnlyNetPl` (carried from/alongside slice A's
  disjoint-partition figures per the spec), `decimal? PairedDemoNetPl`, `decimal? PairedBacktestNetPl`.
  _Satisfies: design D9 (additive shape); Interfaces / Contracts formula._
- [ ] **14.5** RED: `..._Residual_IsNullIfAnyTermIsNull` — swap or embedded-cost estimate null (e.g.
  `NoCalibrationRow`) propagates to a null residual, per the Interfaces / Contracts formula.
  _Satisfies: Interfaces / Contracts ("null if any term is null"); consistent with D8's
  no-partial-total rule extended to the residual._
- [ ] **14.6** GREEN: create `ExecutionResidualDto`; implement the residual formula inside
  `CostDecompositionCalculator` (`PairedDemoNetPl − PairedBacktestNetPl − PairedSwap +
  EmbeddedCostEstimate`, over the paired subset only, null if any term is null) satisfying 14.1–14.5.

### Phase 15 — Contract / tripwire tests extended for B2

- [ ] **15.1** RED: extend `CostDecompositionContractTests.SliceFiles` (B1's own array — add, do not
  duplicate the array) with `Domain/Enums/EmbeddedCostAvailability.cs`, `ResidualBasis.cs`,
  `Infrastructure/Services/CostDecompositionCalculator.cs`; re-run
  `Tripwire_NoSliceFileUsesARandomNumberGeneratorOrSeed`,
  `Tripwire_NoSliceFileContainsANumericThresholdOrCutoff` (still the named identifier list — this is
  the moment `MinObserved`/`MaxObserved` on `EmbeddedCostComponentDto` must NOT false-positive; confirm
  the named list from 7.4 does not match `MinObserved`/`MaxObserved`),
  `Tripwire_NoSliceFileContainsAnInstrumentLiteral` against the full B2 file set.
  _Satisfies: same requirements as Phase 7, now covering B2's files; design "Tripwire test" item 2
  explicitly._
- [ ] **15.2** RED: `..._Dto_ExposesNoThresholdScoreGradeOrRecommendationMember_ExtendedToB2Dtos` —
  reflection over `SwapComponentDto`, `EmbeddedCostComponentDto`, `ExecutionResidualDto` for
  `IsComparable`, `Score`, `Grade`, `Rank`, `Pass`, `Fail`, `Threshold`, `Acceptable`.
  _Satisfies: "The Decomposition Is Diagnostic Only..." — all scenarios, now over B2's surface._
- [ ] **15.3** RED: `..._ResidualBasis_And_EmbeddedCostFields_AreNeverConstructorParameters` — extends
  7.1's pattern: `ResidualBasis` non-nullable/setter-less/absent-from-constructors (parallel to
  `CoverageBasis` in 7.1).
  _Satisfies: same structural pattern as "CoverageBasis Is A Non-Nullable, Non-Droppable Disclosure"
  applied to `ResidualBasis`._
- [ ] **15.4** GREEN: extend `CostDecompositionContractTests.cs` for 15.1–15.3. No production change
  expected unless a tripwire fails.

### Phase 16 — Determinism (full B2 composition)

- [ ] **16.1** RED: `CostDecompositionCalculatorTests.Decompose_RepeatedCalls_AreByteIdentical` — same
  demo trades, backtest trades, calibration row, and comparability DTO supplied twice produce a
  byte-identical `CostDecompositionDto`.
  _Satisfies: Scenario "Repeated calls are byte-identical" (full composition)._
- [ ] **16.2** RED: `..._Decompose_MonthAndSubsetOrdering_IsUnaffectedByShuffledInput` — extends 8.1 to
  the full B2 path (swap/embedded/residual figures are aggregate sums, not month rows, so this mainly
  re-confirms 3.6/8.1 still hold once B2 wraps the coverage calculator).
  _Satisfies: Scenario "Month rows are ordered by set membership, not input order" (composed path)._
- [ ] **16.3** GREEN: no production change expected if calculators are pure; confirm and close.

### Phase 17 — Endpoint completion

- [ ] **17.1** RED: `StrategyBacktestsControllerTests.GetCostDecomposition_WhenValid_Returns200CarryingBothBases`
  — extends 6.4: response now carries `CoverageBasis` AND `ResidualBasis`, with
  `CostDecompositionStatus.Decomposed`.
  _Satisfies: proposal Testing approach: "valid request → 200 carrying both bases" (completed)._
- [ ] **17.2** GREEN: no controller change expected (6.5's action already delegates to the read
  service, which now returns the composed DTO) — confirm 17.1 passes; if the controller needs a
  response-shape update, make it here.

### Phase 18 — Static gates and full suite (B2)

- [ ] **18.1** Run `dotnet build app.trading.algoritmico.api -warnaserror` — zero warnings.
- [ ] **18.2** Run `dotnet format app.trading.algoritmico.api --verify-no-changes` — no formatting
  diffs.
- [ ] **18.3** Run `dotnet test app.trading.algoritmico.api` for the full backend suite. Confirm the
  B1 total (from 9.3) plus every new B2 test from Phases 10–17 pass, with **0 warnings**. Confirm
  every slice A test and every B1 test stays byte-identical.
- [ ] **18.4** If any pre-existing (slice A or B1) test fails or a warning appears, stop and
  investigate before patching — B2 only adds members/files, per D9's additive contract.
- [ ] **18.5** Confirm the final success-criteria checklist in `proposal.md` (Success criteria
  section) against the shipped B1+B2 surface; do not check any box the tests above did not
  demonstrate.

---

## Flagged: spec/design gaps too vague to check off directly

These items have no single spec scenario naming them, or leave a construction/wiring decision
unresolved. Surfacing them now so `sdd-apply` does not have to invent an interpretation mid-implementation:

1. ~~`CostDecompositionStatus.NoRunForKind` / `NoDemoTrades` have no spec scenario at all.`~~
   **RESOLVED.** Spec now has "The Decomposition Status Discloses Which Component Was Producible At
   All" with a Given/When/Then scenario for each; tasks 5.3/5.4 cite them directly and state the
   ordering (checked before window derivation).
2. ~~`CostObservation`'s exact shape (Phase 2) is a design-only artifact.`~~ **RESOLVED.** Verified
   against `Domain/Entities/StrategyTrade.cs` and `BacktestTrade.cs`: `Size` is non-nullable `decimal`
   on both entities — design D2 previously marked it `decimal?` in error; corrected to `decimal`
   (task 2.1). Every other field (`OpenTime`, `OpenPrice`, `ClosePrice`, `Type`, `Swap`, `NetPl`)
   matches what design D2 already claimed.
3. ~~The coverage window's boundaries are unspecified.~~ **RESOLVED.** Spec now has a Definitions
   section: the window is the dense calendar-month span from the earliest to the latest open
   timestamp across the union of demo and backtest trades. Design D11 records the same rule and
   explicitly rejects reusing slice A's "observed months only" shape. Tasks 3.4/3.4a–3.4d cover the
   derivation and its edge cases (single trade, one side empty, zero trades overall routed to
   `NoRunForKind`/`NoDemoTrades` instead of an empty window).
4. **The 4-DB-command budget in design D3 is stated as an aspiration ("6 commands where 4 suffice")
   without enumerating which 4.** Task 5.6 pins whatever count the implementation actually issues,
   but the exact command list (run lookup, demo query, backtest query, calibration query — does
   coverage alone in B1 need the calibration query at all, or does B1 issue only 3?) is not stated in
   either artifact. Flagging so the query-cost test's expected number is set from the real
   implementation, not guessed from the PR line. (Left open — acceptable as a manual check.)
5. ~~`SymbolCalibrationSnapshot` (referenced in design D5's signature) is never defined anywhere in
   spec or design.~~ **RESOLVED.** Design D5 now defines it as a 1:1 projection of `SymbolCalibration`
   (`Symbol`, `Status`, `PointValue`, `SampleCount`, `MinObserved`, `MaxObserved`, `CalibratedAt`),
   verified against the entity; `CalibratedAt` is carried verbatim and the type carries no derived or
   compared value. Task 12.9 updated to build it exactly to that shape.
6. **Success criteria in `proposal.md` are unchecked boxes with no owning test named per box** —
   Phase 18.5 requires cross-referencing them against the test suite by hand; there is no 1:1 mapping
   table in the source artifacts. (Left open — acceptable as a manual check.)
