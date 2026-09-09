# Tasks: Cost reconciliation and divergence — SLICE A (price-series comparability)

> Backend only. No frontend, no cost decomposition (slice B), no `SourcePlatform` (slice C).
> Strict TDD: every behavioural task is test-first. An implementation task never precedes its test.

## 1. Domain enums (no test required — plain type declarations, pinned indirectly by Phase 2/6 tests)

- [x] **1.1** Create `Domain/Enums/ComparabilityBasis.cs` — `PairedOpensOnly = 0`. Mirrors `BreachBasis`.
  _Satisfies: spec Requirement "ComparabilityBasis Is A Non-Nullable, Non-Droppable Disclosure"._
- [x] **1.2** Create `Domain/Enums/DstTransitionRisk.cs` — `None = 0`, `TransitionMonth = 1`.
  _Satisfies: design D2 (DST disclosure)._
- [x] **1.3** Create `Domain/Enums/ComparabilityReadoutStatus.cs` — `NoPairedOpens = 0`, `Measured`,
  `NoDemoTrades`, `NoRunForKind`. Default (`0`) must assert nothing observed, per D3/D5.
  _Satisfies: design D3, D5._

Parallelizable: 1.1, 1.2, 1.3 are independent of each other (different files, no shared symbol).

## 2. Calculator — core pairing and offset algorithm (TDD, sequential within this phase)

Each row is test-first: write the failing test, confirm RED, then write only the production code
needed to pass it. Do not batch multiple tests before the first GREEN.

- [x] **2.1** RED: `Measure_MeasuredFixture_ReproducesTwentyFourPairsAndMonthlyMeans` — fixture built
  from the measured `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` figures (24 pairs; 2026-04 n=5 mean +22.06
  [16.2–25.5]; 2026-05 n=14 mean +22.55 [11.4–31.9]; 2026-06 n=5 mean +40.90 [38.5–44.3]).
  GREEN: create `Infrastructure/Services/DemoBacktestComparabilityCalculator.cs` (`internal static`)
  with `OpenObservation` record struct and minimal `Measure(...)` — minute-truncated key (no
  timezone conversion, `DateTimeKind` untouched), per-month aggregation, `MeanOffsetPriceUnits` /
  `MinOffsetPriceUnits` / `MaxOffsetPriceUnits` as `decimal?` named per D7 (not `…Points`).
  _Satisfies: spec Requirement "Exact-Minute Pairing…" (base case), "Offset Is Reported Per
  Month…" (fixture scenario); design D1, D7._
- [x] **2.2** RED: `Measure_OpenTimesDifferingInSeconds_PairsOnTheTruncatedMinute`.
  GREEN: confirm/adjust truncation (`OpenTime.Ticks - (Ticks % TimeSpan.TicksPerMinute)`), not
  exact `DateTime` equality.
  _Satisfies: spec scenario "Trades opening at the exact same minute…are paired"; design D1._
- [x] **2.3** RED: `Measure_OpenTimesOneHourApart_DoNotPair`.
  GREEN: verify the minute key naturally excludes an hour-shifted pair — no new production code
  expected beyond 2.1/2.2's key logic; if it already passes, keep the test as a pinning tripwire.
  _Satisfies: spec scenario "Trades opening at different minutes are never paired"; design D2
  rationale (DST breaks pairing, does not corrupt an offset)._
- [x] **2.4** RED: `Measure_SameMinuteOpposingDirections_DoesNotPair` — demo `Type = "buy"`, backtest
  `Type = "Sell"`, same minute. **This is a structural contract test**: every trade in the measured
  database is a buy (demo 1,698 rows "buy", backtest 1,406 rows "Buy"), so no production fixture
  exercises this path — the test constructs the opposing-direction case synthetically to pin the
  rule, not to reproduce measured data.
  GREEN: extend the pairing key to include `Type`, compared with
  `StringComparer.OrdinalIgnoreCase` (or `.Equals(..., StringComparison.OrdinalIgnoreCase)`),
  alongside the truncated minute.
  _Satisfies: spec Requirement "Exact-Minute Pairing Detects Price-Series Comparability, Not Trade
  Correspondence" — direction-in-key clause and its "Same-minute trades with opposing directions"
  scenario; resolves design Open Question 1._
- [x] **2.5** RED: `Measure_MarchOctoberNovemberMonths_ReportTransitionRisk` (and a non-transition month
  reports `None`).
  GREEN: add `DstTransitionRisk` computed property on the monthly row —
  `Month is 3 or 10 or 11 ? TransitionMonth : None`.
  _Satisfies: design D2; no spec requirement text names this field directly, but it is the
  mechanism behind D2's disclosure obligation reflected in the DTO doc-comment requirement below._
- [x] **2.6** RED: `Measure_TwoDemoOpensInOneMinute_ExcludesThatMinuteAndCountsItAmbiguous`.
  GREEN: detect ≥2 demo opens sharing a minute key, exclude that minute from pairing, and count both
  trades in `AmbiguousMinuteDemoCount` — zero same-minute duplicates exist in production data, so
  this is defensive/synthetic like 2.4.
  _Satisfies: spec Requirement "Same-Minute Duplicates On Either Side Are Refused And Counted,
  Never Resolved" — demo-side scenario; design D4._
- [x] **2.7** RED: `Measure_TwoBacktestOpensInOneMinute_ExcludesThatMinuteAndCountsItAmbiguous` (mirror
  of 2.6 on the backtest side).
  GREEN: symmetric handling for `AmbiguousMinuteBacktestCount`.
  _Satisfies: same requirement, backtest-side scenario; design D4._
- [x] **2.8** RED: `Measure_AnyInput_BucketsArePartitionsOfBothSides` — property-style assertion:
  `PairedCount + DemoOnlyCount + AmbiguousMinuteDemoCount == demoOpens.Count`, and the analogous
  identity for the backtest side, across several generated inputs.
  GREEN: fix any bucket-counting gap surfaced by the invariant (expected to already hold from
  2.1–2.7; this test pins it, not introduces new logic).
  _Satisfies: spec Requirement "Trade-Set Difference Is Reported As A Set Relationship…" — the
  disjoint-partition guarantee; design's "Set invariants" note._
- [x] **2.9** RED: `Measure_NoPairedOpens_ReturnsNullFiguresAndZeroCountNotAScore`.
  GREEN: ensure `PairedCount == 0` yields `null` for `MeanOffsetPriceUnits` /
  `MinOffsetPriceUnits` / `MaxOffsetPriceUnits` / `SignConsistency`, and `Status =
  ComparabilityReadoutStatus.NoPairedOpens` — never a withheld/omitted field.
  _Satisfies: spec Requirement "Figures Publish At Any Paired Count Above Zero…" — zero-count
  scenario; design D5._
- [x] **2.10** RED: `Measure_SinglePairedTrade_StillPublishesTheFigureBesideTheCount` — exactly one
  paired trade must publish `MeanOffsetPriceUnits` (equal to that pair's offset), min, max, and
  `SignConsistency`, alongside `PairedTradeCount = 1`.
  GREEN: confirm no minimum-count gate exists anywhere in the aggregation path (there must be
  none to write — this test is the tripwire against ever adding one).
  _Satisfies: same requirement — single-pair scenario; explicitly forbids an invented minimum per
  design D5._
- [x] **2.11** RED: `Measure_MixedSigns_ReportsSignConsistencyBelowOne`.
  GREEN: add `SignConsistency` computed property —
  `PairedCount == 0 ? null : (decimal)Math.Max(PositiveCount, NegativeCount) / PairedCount`, fed by
  `PositiveCount` / `NegativeCount` / `ZeroCount` tallied during aggregation.
  _Satisfies: spec Requirement "Sign Consistency Is Reported, And No Directional Performance Claim
  Is Made"._
- [x] **2.12** RED: `Measure_CalledTwiceWithShuffledInput_ReturnsByteIdenticalOutput` — same trade sets
  in a different input order, called twice.
  GREEN: ensure aggregation sorts by `(Year, Month)` and does not depend on input iteration order
  (e.g. use ordered dictionary keys or explicit `OrderBy` before emitting `Months`).
  _Satisfies: spec Requirement "The Calculator Is Deterministic"._

## 3. DTO contract and reflection tests (TDD)

- [x] **3.1** RED: `Dto_ComparabilityBasis_IsComputedNonNullableAndHasNoSetter` (reflection test
  mirroring the `BacktestNetSeries` pattern referenced in the spec).
  GREEN: create `Application/DTOs/Divergence/PriceOffsetComparabilityDto.cs` — sealed record with
  the get-only computed `Basis => ComparabilityBasis.PairedOpensOnly` property (no constructor
  parameter, no setter), and the sibling `MonthlyPriceOffsetDto` sealed record with the computed
  `DstRisk` and `SignConsistency` properties from Phase 2.
  _Satisfies: spec Requirement "ComparabilityBasis Is A Non-Nullable, Non-Droppable Disclosure" —
  both scenarios; design D6._
- [x] **3.2** RED: `Dto_ExposesNoAggregatedScoreOrGradeMember` — reflection test asserting no member
  named/typed as a cross-month aggregate, 0–100 score, or pass/fail grade exists on either DTO.
  GREEN: no production change expected (the DTO from 3.1 has no such member); this test is a
  tripwire against future additions.
  _Satisfies: spec Requirement "Offset Is Reported Per Month, Never As A Single Aggregated Score"
  — "No aggregated score is ever exposed" scenario; and Requirement "Ranking Across Strategies…"
  — "No threshold is applied" scenario, jointly with 5.2._
- [x] **3.3** RED: `Tripwire_NoSliceFileUsesARandomNumberGeneratorOrSeed` — file-scoped grep test over
  the slice's file list (mirrors the existing `BacktestPortfolioRiskTripwireTests` precedent).
  GREEN: no production change expected; passes because Phase 2's calculator has no RNG.
  _Satisfies: spec Requirement "The Calculator Is Deterministic" (defence-in-depth)._
- [x] **3.4** RED: `Tripwire_NoSliceFileContainsANumericThreshold` — same grep-tripwire style, scoped
  to this slice's files, asserting no code-constant cutoff/threshold appears.
  GREEN: no production change expected.
  _Satisfies: spec Requirement "Ranking Across Strategies Orders By Absolute Mean Offset, And Never
  Grades" — "No threshold is applied anywhere" scenario; design's "no invented minimum" (D5) read
  together with D8._

Note: add the XML doc-comment text required by spec Requirement "The Offset Readout Is Explicitly
Transitional, Not A Permanent Verdict" (states the offset reflects the current data-symbol binding
and may change after re-binding) directly on `PriceOffsetComparabilityDto` in task 3.1 — this is a
documentation obligation with no dedicated test; call it out explicitly during 3.1's implementation
step and verify by inspection in Phase 6.

## 4. Application interface (no test — pure declaration, exercised by Phase 5 tests)

- [x] **4.1** Create `Application/Interfaces/IDemoBacktestComparabilityReadService.cs` — one method,
  `Task<PriceOffsetComparabilityDto> GetAsync(Guid strategyId, BacktestRunKind kind,
  CancellationToken ct)`. `kind` has no default value.
  _Satisfies: design D3 (required parameter, no fallback) and the Data Flow diagram's service
  boundary._

## 5. Read service — query shape and status handling (TDD, depends on Phase 1, 3, 4)

- [x] **5.1** RED: `GetAsync_StrategyWithNoRunOfThatKind_ReturnsNoRunForKindAndNoFigures`.
  GREEN: create `Infrastructure/Services/DemoBacktestComparabilityReadService.cs` implementing
  `IDemoBacktestComparabilityReadService` — first query resolves `BacktestRuns.Where(StrategyId,
  Kind)`; if none, return `Status = NoRunForKind` with no figures, no further queries issued.
  _Satisfies: design D3 — no fallback to the other slot._
- [x] **5.2** RED: `GetAsync_AnyStrategy_IssuesAtMostThreeQueriesAndMaterializesNoEntities` (assert
  via a query-counting/logging interceptor or an in-memory/SQLite-backed `DbContext`, per the
  existing test pattern for sibling services).
  GREEN: complete the happy path — project `StrategyTrade` and `BacktestTrade(runId)` to
  `(OpenTime, OpenPrice, Type)` via `IQueryable` `.Select(...)` (no entity materialization), pass
  both projections into `DemoBacktestComparabilityCalculator.Measure(...)`, return the resulting
  DTO with `Status = Measured` (or `NoDemoTrades` when the demo projection is empty).
  _Satisfies: design "Cost of evaluation" constraint (two/three queries total, no per-row queries);
  Data Flow sequence diagram._

## 6. WebAPI endpoint (TDD, depends on Phase 4, 5)

- [x] **6.1** RED: `GetComparability_MissingKind_Returns400` on
  `StrategyBacktestsControllerTests` (modify, not create).
  GREEN: add `[HttpGet("comparability")]` action on `StrategyBacktestsController` under
  `api/strategies/{strategyId:guid}`, with `kind` as a required query parameter (no default) that
  model-binding rejects with 400 when absent.
  _Satisfies: design D3 — kind is required, never defaulted; design D9 — REST sibling, not GraphQL._
- [x] **6.2** RED: `GetComparability_ValidRequest_Returns200WithBasis`.
  GREEN: wire the action to call `IDemoBacktestComparabilityReadService.GetAsync`, return
  `200 OK` with the DTO (whose `Basis` is always present per Phase 3).
  _Satisfies: design's read-path sequence diagram; spec Requirement "ComparabilityBasis…" —
  presence on every readout, exercised end-to-end._
- [x] **6.3** Register the new service in `Infrastructure/DependencyInjection.cs`
  (`IDemoBacktestComparabilityReadService` → `DemoBacktestComparabilityReadService`).
  No dedicated test; covered indirectly by 6.1/6.2 requiring DI resolution to succeed.
  _Satisfies: design File Changes table — DI registration._

Existing controller test file is modified, not just added-to, only in the sense that this phase's
new test methods live in it; no existing test case's body changes — this satisfies "existing cases
byte-identical" from the design's File Changes table.

## 7. Full-suite verification (sequential, last)

- [x] **7.1** Run `dotnet test` from `app.trading.algoritmico.api` for the full backend suite. Confirm
  **607 pre-existing tests still pass, plus all 19 new tests from Phases 2, 3, 5, 6 (28 new
  assertions across ~19 named cases)**, for a total of **626/626**, and **0 warnings**
  (`-warnaserror`, dash form, per environment gotchas).
  _Satisfies: "Must not regress" constraint — no existing path is modified in this design, so no
  existing test file's assertions change; this task is the proof, not an assumption._
- [x] **7.2** If any existing test fails or a warning appears, treat it as a stop condition: this
  design commits to touching nothing on an existing path, so a failure here means either an
  unintended edit occurred or the design's "nothing existing is modified" claim was wrong for this
  codebase state — do not silently patch the existing test to make it pass; investigate the cause
  first.

---

## Sequencing summary

- **Sequential spine**: 1 → 2 → 3 → 4 → 5 → 6 → 7 (each phase's tests need the previous phase's
  production types to compile).
- **Parallelizable within a phase**: 1.1/1.2/1.3 (independent files); 2.6/2.7 (symmetric, no shared
  state); 3.3/3.4 (independent tripwire tests) — all other tasks are ordered by TDD dependency
  (test N's production code is a prerequisite for test N+1 compiling against it) or by
  layer-dependency (Application before Infrastructure before WebAPI).
- Phase 7 cannot start until every task in 1–6 is complete and committed to the working tree.

## Review Workload Forecast

- **Estimated size**: 3 enums + 2 DTOs/records + 1 interface + 1 calculator + 1 read service + 1
  modified controller + 1 modified DI registration ≈ **220–260 production lines**; ~19 named test
  cases (several with multiple asserted scenarios, e.g. 2.4/2.6/2.7 needing synthetic fixtures
  since production data has no exercising case) ≈ **260–320 test lines**. **Total estimate:
  480–580 lines.**
- **Agreement with design's ~400–480 estimate**: I do not fully agree — I expect the actual total
  to land **above** the design's stated floor, for two reasons the design itself flags: (a) the
  per-field XML doc-comment style this design mandates (D1–D10's rationale text, the transitional-
  framing requirement, the `ComparabilityBasis`/`DstTransitionRisk` disclosures) is exactly what
  drove the two cited precedents from 330→731 and 670→1,709 lines; (b) two of the required
  scenarios (opposing-direction pairing, same-minute duplicates) have **zero exercising production
  data**, so their tests need hand-built synthetic fixtures rather than slices of the measured
  24-pair dataset, which is more test-authoring effort than a fixture-reuse case.
- **Where I would split it if not `exception-ok`**: the seam is between Phase 2 (pure calculator,
  ~9 TDD rows, no I/O, no DI) and Phases 4–6 (interface + read service + controller, which need a
  test double or SQLite-backed context and DI wiring). A first PR shipping only the calculator
  (Phases 1–3) plus its reflection/tripwire tests would be reviewable in isolation and is the
  larger, riskier half of the logic; a second PR would add the read service and endpoint (Phases
  4–6) plus the full-suite verification (Phase 7). Since `delivery_strategy: exception-ok` is in
  effect, this stays as one PR per instruction, but the seam is real and reviewers should expect to
  read it as two logical halves.
- **Risk this forecast flags for the maintainer**: the two synthetic-fixture tests (2.4 direction,
  2.6/2.7 duplicates) are the parts of this slice with no measured-data anchor at all — they are
  correct by construction against the spec text, not verifiable against `MEASURED_*.md`. A reviewer
  should scrutinize those fixtures' construction more carefully than the fixture-derived tests,
  since a mistake there would not be caught by comparison to any recorded number.
