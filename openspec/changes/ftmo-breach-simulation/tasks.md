# Tasks: FTMO 2-Step breach simulation (roadmap layer 1)

Strict TDD. Every RED precedes its GREEN, including the migration, the clock, the projector, the
evaluator, and the web label. Load-bearing units (clock, projector, evaluator) get an explicit
falsification step: break it, observe red, restore. Four chained PRs per design.md Decision 8.
Read `.claude/conventions/backend-core.md`, `backend-data.md`, `backend-testing.md` before any
`.cs` file; `frontend-core.md`, `frontend-data.md` before the web file. Every unit cites its spec
requirement/scenario or design decision.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | P1 300 floor (550 realistic) / P2 450 floor (900) / P3 300 floor (550) / P4 450 floor (850) |
| 400-line budget risk | High (P2, P4) |
| Chained PRs recommended | Yes |
| Suggested split | P1 (clock) → P2 (projector+evaluator+enums) → P3 (entity+migration) → P4 (read service+DTOs+endpoint+relabel+web) |
| Delivery strategy | ask-on-risk (default) |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

Floors, not targets. If a PR sits on or over budget, that is a `size:exception` conversation, not
a reason to trim scope (design.md Decision 8: prior slices overran ~2x).

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | `FtmoDayClock` + Windows/Linux DST characterization pins | PR P1 (base: `feature/ftmo-breach-simulation`) | `dotnet test --filter "FullyQualifiedName~FtmoDayClock"` | `dotnet test app.trading.algoritmico.api` (full suite once) | Revert `FtmoDayClock.cs` + its test file; no I/O, pure domain |
| 2 | `FtmoTradeProjector`, `FtmoBreachEvaluator`, new enums | PR P2 (base: P1's branch) | `dotnet test --filter "FullyQualifiedName~FtmoTradeProjector|FullyQualifiedName~FtmoBreachEvaluator"` | `dotnet test app.trading.algoritmico.api` (full suite once) | Revert the two services + enums; no I/O |
| 3 | `FtmoInstrumentSpec` entity, config, migration + seed, schema tests | PR P3 (base: P2's branch) | `dotnet test --filter "FullyQualifiedName~FtmoInstrumentSpec"` | `dotnet test app.trading.algoritmico.api` (full suite once); migration NOT applied to any DB | Revert entity/config/migration files; additive-only table, unused until P4 |
| 4 | Read service, DTOs, endpoint, `BreachBasis` relabel, web label | PR P4 (base: P3's branch) | `dotnet test --filter "FullyQualifiedName~FtmoBreachSimulation"` then `pnpm --dir app.trading.algoritmico.web test` | Full backend suite once + full frontend suite once | Revert service/DTO/controller files + `BreachBasis.cs`/label + web enum member/label; P1-P3 stay valid standalone |

---

## PR P1 — `FtmoDayClock` (pure domain, no I/O)

### Phase 1 — Characterization pins (Windows-measured, Linux unverified)

- [x] 1.1 RED: `FtmoTimeZoneCharacterizationTests.cs` — pin, per design.md Decision 1 and Engram
  #2769: (a) `TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem")` and `("Europe/Berlin")`
  resolve; (b) 2026-01-15 00:30 and 2026-07-15 00:30 Jerusalem both convert to 23:30 of the
  PREVIOUS Berlin day; (c) 2013-10-27 01:30 Jerusalem has `IsAmbiguousTime == true` and
  `ConvertTimeToUtc` picks standard time; (d) 2013-03-29 02:30 Jerusalem has `IsInvalidTime ==
  true` and `ConvertTimeToUtc` THROWS `ArgumentException`; (e) 2012-09-23 01:30 Jerusalem is
  ambiguous under the pre-2013 Israeli rule. Will fail today because no test file exists — that
  is the RED (nothing to implement; this suite only observes the BCL).
  _Satisfies: design.md Decision 1 verification status; spec.md Day-Boundary requirement pins._
- [x] 1.2 Run 1.1 on the target CI/container image (Linux, per `mcr.microsoft.com/dotnet/aspnet:10.0`).
  If any assertion fails, STOP and record the failure — this is the NodaTime decision point
  (design.md Decision 1), not a reason to weaken the pin. Do not proceed to 1.3+ silently on a
  Linux mismatch; escalate to the user/orchestrator.
- [x] 1.3 GREEN (confirmatory, no production code): 1.1 passes on the verified platform. Commit the
  pin as a permanent regression guard.

### Phase 2 — `FtmoDayClock.Attribute` (candidate-set conversion)

- [x] 2.1 RED: `FtmoDayClockTests.Attribute_00_30JerusalemOutsideDstWindow_MapsToPreviousBerlinDay` —
  a non-mismatch-window close at 00:30 Jerusalem attributes to the previous Berlin-day date, one
  candidate day, no flags.
  _Satisfies: spec.md "A close near FTMO midnight is bucketed by the converted day" scenario._
- [x] 2.2 GREEN: create `Infrastructure/Services/FtmoDayClock.cs` (`internal static`) implementing
  `Attribute(DateTime sourceLocal, TimeZoneInfo sourceZone, TimeZoneInfo berlin) ->
  DayAttribution(IReadOnlySet<DateOnly> CandidateDays, DateTime FtmoLocal, AttributionFlags
  Flags)` per design.md Decision 1: ambiguous → two candidates via
  `GetAmbiguousTimeOffsets`; invalid → two candidates (`local - BaseUtcOffset`, `local -
  (BaseUtcOffset + 1h)`); otherwise one candidate via `GetUtcOffset`. Confirm 2.1 passes.
- [x] 2.3 RED: `..._2013_10_27_01_30Jerusalem_FlagsAmbiguousSourceTime`.
  _Satisfies: spec.md "An ambiguous or invalid source timestamp is contingent" scenario (ambiguous half)._
- [x] 2.4 RED: `..._2013_03_29_02_30Jerusalem_FlagsInvalidSourceTime`.
  _Satisfies: same scenario, invalid half._
- [x] 2.5 GREEN: wire ambiguous/invalid detection into `AttributionFlags` (`AmbiguousSourceTime`,
  `InvalidSourceTime`). Confirm 2.3–2.4 pass.
- [x] 2.6 RED: `..._MismatchWindowExactDayDiffersFromNaive_FlagsDstMismatchWindow` — a close where
  `jerusalem.GetUtcOffset(utc) - berlin.GetUtcOffset(utc) != 1h` AND the exact day differs from
  `(sourceLocal - 1h).Date`.
  _Satisfies: spec.md "A close whose exact day differs from the naive day is contingent" scenario._
- [x] 2.7 RED: `..._MismatchWindowExactDayMatchesNaive_NoFlagFromMismatchAlone` — same window, exact
  day equals the naive day, source time neither ambiguous nor invalid → no `DstMismatchWindow` flag.
  _Satisfies: spec.md "A close inside the mismatch window whose exact day matches the naive day is
  not downgraded" scenario._
- [x] 2.8 GREEN: implement the mismatch-window naive-day comparison. Confirm 2.6–2.7 pass.
- [x] 2.9 Falsification (mandatory, load-bearing unit): temporarily replace the offset comparison in
  2.8 with an always-true check (flag every mismatch-window close regardless of naive-day match) —
  confirm 2.7 goes RED. Restore the exact comparison and confirm 2.7 is green again.
- [x] 2.10 RED: `..._UnresolvableZoneId_ReturnsTimeZoneDataUnavailable` — `FindSystemTimeZoneById`
  throws `TimeZoneNotFoundException` and `TryConvertIanaIdToWindowsId` also fails → clock signals
  refusal, never a fallback offset.
  _Satisfies: design.md Decision 1 resolution; spec.md refusal reasons table (`TimeZoneDataUnavailable`)._
- [x] 2.11 GREEN: implement the `FindSystemTimeZoneById` → `TryConvertIanaIdToWindowsId` fallback
  chain, returning a refusal signal (not throwing, not defaulting) on double failure. Confirm 2.10.
- [x] 2.12 RED: `..._FixedPlus02Offset_WouldDisagreeWithExactConversionAtLeastPartOfTheYear` —
  construct a hypothetical fixed-`+02:00` day assignment alongside `FtmoDayClock`'s exact result
  for both a winter and a summer date; assert they differ for at least one.
  _Satisfies: spec.md "A fixed +02:00 offset would fail this requirement" scenario._

### Phase 3 — Static gates (P1)

- [x] 3.1 `dotnet build AppTradingAlgoritmico.slnx -warnaserror` — zero warnings.
- [x] 3.2 `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes` — no diffs.
- [x] 3.3 `dotnet test` full suite — confirm the **723 pre-existing tests** still pass plus every
  new P1 test.

---

## PR P2 — Projector, evaluator, enums (base: P1's branch)

### Phase 4 — New enums

- [x] 4.1 GREEN: create `Domain/Enums/FtmoBreachVerdict.cs` (`Breached`, `BreachContingent`,
  `NoBreachObserved`), `BreachContingencyCause.cs` (`ConcurrentOpenPosition`,
  `AmbiguousSourceTime`, `InvalidSourceTime`, `DstMismatchWindow`, `UnscalableTradeExcluded`,
  `FxRoundingSensitive`), `FtmoSimulationStatus.cs` (`Evaluated`, `Refused`),
  `FtmoSimulationRefusal.cs` (`ProductNotTwoStep`, `LimitsNotConfigured`,
  `DrawdownModelNotStatic`, `InstrumentSpecMissing`, `PointValueNotCalibrated`,
  `FxRateNotDeclared`, `InvalidFxBand`, `RiskNotEstimable`, `RunSegmentsDisagree`,
  `TimeZoneDataUnavailable`, `InvalidRequest`) per design.md Decisions 5/6.
  _Satisfies: spec.md Three-State Finding requirement; refusal-reason scenarios throughout._

### Phase 5 — `FtmoTradeProjector` (money-per-point, never lot count)

- [x] 5.1 RED: `FtmoTradeProjectorTests.Project_PSrcEqualsM_ReproducesTradeResizerAndBridge` — the
  identity from design.md Decision 2: when `P_src == M`, the projector's `net'`/`q'` for a sample
  trade set equal the existing `TradeResizer.Resize` + `BacktestNetSeries.Bridge` result on the
  same inputs.
  _Satisfies: spec.md Rescaling requirement; design.md Decision 2 identity._
- [x] 5.2 GREEN: create `Infrastructure/Services/FtmoTradeProjector.cs` (`internal static`)
  implementing `u = q · target · P_src / (Â · M)`, `q' = clamp(floor(u/step)·step, min, max)`,
  `net' = Profit · (q'·M) / (q·P_src)` per design.md Decision 2. Does NOT call
  `BacktestReadService`'s existing `TryNormalize`/`TradeResizer.Resize` pairing at
  `BacktestReadService.cs:232-246` — takes `Â` from `TryNormalize` on the source grid only, and
  performs its own money-per-point arithmetic. Confirm 5.1 passes.
- [x] 5.3 RED: `..._DaxWorkedExample_Gives013LotsAndNet0234xProfit` — pin design.md's exact numbers:
  q=0.06, Â=200, target=50, P_src=10, C=1, FX=1.08 → q'=0.13, net'=Profit×0.234.
  _Satisfies: spec.md "DAX rescaling uses the declared FTMO point value" scenario._
- [x] 5.4 RED: `..._BtcCappedAtMaxLots_ReturnsMaxNotUnbounded` — a trade whose computed `u` exceeds
  BTC's `MaxLots = 5.00` clamps to 5.00, `Outcome = CappedAtMaximum`.
  _Satisfies: design.md Decision 4 BTC seed row; spec.md Resize Counts requirement._
- [x] 5.5 RED: `..._BelowMinLot_RaisesToMinimumAndCountsIt` — a trade whose computed `u` floors below
  `MinLot` raises to `MinLot`, `Outcome = RaisedToMinimum`.
  _Satisfies: spec.md Resize Counts requirement._
- [x] 5.6 GREEN: implement clamp outcomes (`RaisedToMinimum`, `CappedAtMaximum`, `Unscalable`) on
  `ProjectedTrade`. Confirm 5.3–5.5 pass.
- [x] 5.7 Falsification (mandatory, load-bearing unit): temporarily change 5.2's `net'` formula to
  scale by `q'/q` (the known-defective lot-count path from `BacktestReadService.cs:232-246`) and
  re-run 5.3 — confirm it goes RED with the lot-count path's wrong numbers (~$33 vs $46.80, per
  design.md's worked example). Restore the money-per-point formula and confirm 5.3 is green again.

### Phase 6 — `FtmoBreachEvaluator` (running balance, three-state, day-scoped causes)

- [x] 6.1 RED: `FtmoBreachEvaluatorTests.Evaluate_Minus8PctDayWithVar95At2Pct_GivesBreachedIndependentOfVar95` —
  spec.md's headline scenario: an 8% intraday drop yields `Breached` regardless of segment VaR95.
  _Satisfies: spec.md "A single bad day yields Breached, not a VaR-shaped near-miss" scenario._
- [x] 6.2 GREEN: create `Infrastructure/Services/FtmoBreachEvaluator.cs` (`internal static`)
  implementing `Evaluate(IReadOnlyList<ProjectedTrade>, FtmoDayClock, decimal initialCapital,
  decimal dailyPct, decimal maxPct) -> FtmoBreachEvaluation` per design.md Decision 5/interfaces:
  iterate closes in `(CloseTime, RowIndex)` order, running balance, daily floor =
  previous-midnight balance − dailyPct·initial (day 1 = initial), max floor = static
  initial·(1−maxPct). Confirm 6.1 passes.
- [x] 6.3 RED: `..._ReferenceFloorMovesWithPreviousMidnightBalance_NotFlatInitialMinus5Pct` — day 1
  closes 2% above initial, day 2 loses 5% of initial intraday; assert the day-2 floor equals
  (day-1 closing balance) − 5%·initial, higher than a flat initial−5% floor would be.
  _Satisfies: spec.md "Reference floor moves with the previous day's balance" scenario._
- [x] 6.4 RED: `..._FlatInitialMinus5PctEveryDay_WouldFailThisRequirement` — construct the same
  two-day segment; assert a hypothetical flat-floor calculation differs from 6.3's evaluator floor.
  _Satisfies: spec.md "A flat 5% of initial every day would fail this requirement" scenario._
- [x] 6.5 RED: `..._Day1UsesInitialCapitalAsReferenceBalance`.
  _Satisfies: spec.md "Day 1 uses Initial Capital as the reference balance" scenario._
- [x] 6.6 RED: `..._MaxLossFloorDoesNotMoveAfterProfitableDay` — day 1 closes 5% above initial; day 2's
  max floor is unchanged from day 1.
  _Satisfies: spec.md "Max loss floor does not move after a profitable day" scenario._
- [x] 6.7 RED: `..._ThreeClosesOneDay_MiddleCloseBreachesThirdRecovers_FindingReflectsMiddleBreach` —
  chronological evaluation catches the transient breach, not the recovered end state.
  _Satisfies: spec.md "Every close in chronological order is checked" scenario._
- [x] 6.8 GREEN: implement per-close evaluation order and day-1/floor-movement rules. Confirm 6.3–6.7.
- [x] 6.9 Falsification (mandatory, load-bearing unit): temporarily change 6.2's daily floor to a
  flat `initial·(1−dailyPct)` on every day (no midnight-balance tracking) and re-run 6.3 — confirm
  it goes RED. Restore the moving-floor logic and confirm 6.3 is green again.
- [x] 6.10 RED: `..._NoOverlap_BreachStaysBreached` — no trade's `OpenTime` precedes the previous
  trade's `CloseTime`; a detected breach stays `Breached`.
  _Satisfies: spec.md "No overlap in current data still yields Breached" scenario._
- [x] 6.11 RED: `..._OverlapAtBreachInstant_DowngradesToBreachContingentWithConcurrentOpenPositionCause` —
  a second trade's `OpenTime` precedes and `CloseTime` follows the breaching close (constructed
  fixture — measured today's 0-of-46 runs do not exercise this, per spec.md and Engram #2769).
  _Satisfies: spec.md "A breach coinciding with an open second position is downgraded" scenario;
  reads `BacktestTrade.OpenTime`, not `DatedNet`._
- [x] 6.12 GREEN: implement overlap detection reading `OpenTime`/`CloseTime` pairs, adding
  `ConcurrentOpenPosition` to `Causes` and downgrading to `BreachContingent`. Confirm 6.10–6.11.
- [x] 6.13 RED: `..._UnscalableTradeClosedBeforeBreach_DowngradesWithUnscalableTradeExcludedCause`.
  _Satisfies: design.md Decision 5 causes table (`UnscalableTradeExcluded`)._
- [x] 6.14 RED: `..._LaterCleanBreachAfterContingentOne_YieldsBreachedOnTheCleanClose` — first breach
  has a cause, later breach has none → verdict `Breached`.
  _Satisfies: design.md Decision 5 ("If the first breach was not real, the account lived to the
  clean one")._
- [x] 6.15 GREEN: implement `FirstBreach`/`FirstCleanBreach` tracking and the
  `BreachContingent`-requires-non-empty-`Causes` factory invariant. Confirm 6.13–6.14.
- [x] 6.16 RED: `..._NoBreachingClose_YieldsNoBreachObserved`.
  _Satisfies: spec.md Three-State Finding requirement — the third state._
- [x] 6.17 RED: `..._NoOutputContainsPassSurvivedSafeWording` — inspect every field/label produced by
  the evaluator's disclosure text for "passed", "safe", "survived", "would have passed".
  _Satisfies: spec.md "No pass-style wording appears anywhere in the result" scenario._
- [x] 6.18 RED: `..._NoBreachObservedCarriesDisclosureText_NotASurvivalClaim`.
  _Satisfies: spec.md "NoBreachObserved carries its own disclosure text" scenario._
- [x] 6.19 GREEN: confirm 6.16–6.18 pass with existing disclosure implementation from 6.15; add
  disclosure strings if missing.

### Phase 7 — Static gates (P2)

- [x] 7.1 `dotnet build AppTradingAlgoritmico.slnx -warnaserror` — zero warnings.
- [x] 7.2 `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes` — no diffs.
- [x] 7.3 `dotnet test` full suite — confirm 723 baseline plus P1+P2 tests all pass.

---

## PR P3 — `FtmoInstrumentSpec` entity, migration, seed (base: P2's branch)

### Phase 8 — Entity and configuration

- [x] 8.1 RED: `FtmoInstrumentSpecSchemaTests.cs` — mirror `BacktestSchemaTests.cs`'s EF-contract
  idiom: `SqxSymbol` is unique (`FindProperty`/`FindIndex`), `Provenance` is required (non-nullable
  `string`), `SourceTimeZoneId` is a non-nullable `string`.
  _Satisfies: design.md Decision 4 column table._
- [x] 8.2 GREEN: create `Domain/Entities/FtmoInstrumentSpec.cs` with `SqxSymbol` (unique),
  `FtmoSymbol`, `ContractSize`, `ProfitCurrency` (ISO), `SizeDecimals`, `Step`, `MinLot`,
  `MaxLots`, `SourceTimeZoneId`, `Provenance` (required), `CapturedOn`; add
  `Persistence/Configurations/FtmoInstrumentSpecConfiguration.cs` with the unique index on
  `SqxSymbol`. Confirm 8.1 passes.

### Phase 9 — Migration with seeded provenance (SQL provenance comment, `internal const` precedent)

- [x] 9.1 RED: `FtmoInstrumentSpecMigrationProvenanceTests.cs` — mirror
  `BacktestMigrationProvenanceTests.cs`: assert `AddFtmoInstrumentSpecs.SeedSql` (an `internal
  const string`) contains all four `SqxSymbol` values (`XAUUSD_M1_UTC02`, `DEUIDXEUR_M1_UTC02`,
  `USATECHIDXUSD_M1_UTC02`, `BTCUSD_M1_UTC02`), the phrase "user-supplied", the date
  `2026-09-24`, and the BTC row's `MaxLots = 5.00` (distinct from the others' `1000`). Will not
  compile until 9.2 exists — that is the RED.
  _Satisfies: design.md Decision 4 seed table; Engram #2766/#2769 provenance._
- [x] 9.2 GREEN: run `dotnet ef migrations add AddFtmoInstrumentSpecs`, hand-edit to add the
  `FtmoInstrumentSpecs` table (`Up`), seed the four rows via `internal const string SeedSql` with
  the provenance comment ("user-supplied from the FTMO MT platform 2026-09-24, Engram
  #2766/#2769"), and `Down` drops the table. Confirm 9.1 passes. **Do NOT run `dotnet ef database
  update`** — applying the migration is a separate, user-authorised step naming the target
  connection.
- [x] 9.3 GREEN: update `AppDbContextModelSnapshot.cs` and the migration's `.Designer.cs` via the
  9.2 scaffold; do not hand-edit beyond what the scaffold produced.
- [x] 9.4 RED: `..._SeededRows_MatchTheFourInstrumentSpecs_WithCorrectContractSizeCurrencyAndGrid` —
  pin exact values: `XAUUSD_M1_UTC02`→`XAUUSD` (100, USD), `DEUIDXEUR_M1_UTC02`→`GER40.cash` (1,
  EUR), `USATECHIDXUSD_M1_UTC02`→`US100.cash` (1, USD), `BTCUSD_M1_UTC02`→`BTCUSD` (1, USD, max
  5.00); min 0.01, step 0.01 for all; max 1000 except BTC.
  _Satisfies: design.md Decision 4 seed table verbatim._
- [x] 9.5 GREEN: confirm 9.4 passes against the `SeedSql` from 9.2; no additional production code
  expected if 9.2 is correct.

### Phase 10 — Static gates (P3)

- [x] 10.1 `dotnet build AppTradingAlgoritmico.slnx -warnaserror` — zero warnings.
- [x] 10.2 `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes` — no diffs.
- [x] 10.3 `dotnet test` full suite — confirm 723 baseline plus P1+P2+P3 tests all pass. Confirm no
  test applies the migration to a real database.

---

## PR P4 — Read service, DTOs, endpoint, `BreachBasis` relabel, web label (base: P3's branch)

### Phase 11 — Refusal reasons (one test per reason, per design.md Decision 6)

- [ ] 11.1 RED: `FtmoBreachSimulationReadServiceTests.Simulate_NullOrOneStepProduct_RefusesProductNotTwoStep`.
  _Satisfies: spec.md "A non-TwoStep or missing product refuses evaluation" scenario._
- [ ] 11.2 RED: `..._NoBrokerRiskLimitsRow_RefusesLimitsNotConfigured`.
- [ ] 11.3 RED: `..._DrawdownModelNotStatic_RefusesDrawdownModelNotStatic`.
- [ ] 11.4 RED: `..._NoInstrumentSpecForSymbol_RefusesInstrumentSpecMissing`.
- [ ] 11.5 RED: `..._NqUsatechCalibrationInconsistentNullPointValue_RefusesPointValueNotCalibrated` —
  the live refusal case: `CalibrationStatus.Calibrated == 0` (CLR default), so this test MUST
  assert the null-`PointValue` check fires even when `Status` happens to be `Calibrated`
  (construct that exact combination), not only when `Status != Calibrated`.
  _Satisfies: spec.md "NQ simulation is refused when no FTMO point value is supplied" scenario;
  design.md Decision 6 ("the null check is load-bearing")._
- [ ] 11.6 RED: `..._NonUsdSymbolNoFxBandSupplied_RefusesFxRateNotDeclared`.
  _Satisfies: spec.md "A EUR-settling symbol without a supplied FX band is refused" scenario._
- [ ] 11.7 RED: `..._DegenerateOrInvertedFxBand_RefusesInvalidFxBand`.
- [ ] 11.8 RED: `..._NormalizerRefusesRun_RefusesRiskNotEstimable`.
- [ ] 11.9 RED: `..._RunTradesCarryMultipleSegments_RefusesRunSegmentsDisagree`.
- [ ] 11.10 RED: `..._UnresolvableTimeZoneId_RefusesTimeZoneDataUnavailable`.
- [ ] 11.11 RED: `..._InvalidCapitalOrTargetOrSourceGrid_RefusesInvalidRequest`.
- [ ] 11.12 RED: `..._AnyRefusedRun_CarriesNoFindings` — a refused result's finding collection is empty.
  _Satisfies: design.md Decision 6 ("A refused run carries NO findings")._
- [ ] 11.13 GREEN: create `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs`,
  `Application/Interfaces/IFtmoBreachSimulationReadService.cs`, and
  `Infrastructure/Services/FtmoBreachSimulationReadService.cs` implementing the guard chain from
  design.md's Data Flow section in order (product → limits → drawdown model → instrument spec →
  point-value-null check → FX band → normalizer → segments → timezone → request validity), each
  guard returning `Refused(reason)` before any `FtmoTradeProjector`/`FtmoBreachEvaluator` call.
  Confirm 11.1–11.12 pass.

### Phase 12 — FX band evaluation and merge

- [ ] 12.1 RED: `..._SameCurrencySymbol_NoFxBandRequired_Proceeds` — `XAUUSD` needs no band.
  _Satisfies: spec.md "A same-currency symbol needs no FX band" scenario._
- [ ] 12.2 RED: `..._AgreeingFxBand_ProducesCleanVerdictWithoutFxRoundingSensitive`.
  _Satisfies: spec.md "A EUR-settling symbol with an agreeing band produces a clean verdict" scenario._
- [ ] 12.3 RED: `..._DisagreeingFxBand_YieldsBreachContingentFxRoundingSensitive_AndEchoesTheBand`.
  _Satisfies: spec.md "A EUR-settling symbol with a disagreeing band is FX-contingent" scenario._
- [ ] 12.4 GREEN: implement the fxLow/fxHigh dual-run and merge per design.md's Data Flow ("merge
  the two evaluations -> verdict + causes + resize counts per fx"). Confirm 12.1–12.3.

### Phase 13 — Resize counts, IS/OOS separation, disclosures, symbol/grid declaration

- [ ] 13.1 RED: `..._TwoRaisedZeroCappedZeroUnscalable_NoBreach_CountsStillReported`.
  _Satisfies: spec.md "Resize counts appear even on a NoBreachObserved result" scenario._
- [ ] 13.2 RED: `..._IsAndOosRuns_ProduceTwoSeparateResults_NeverMerged`.
  _Satisfies: spec.md "IS and OOS produce separate results for the same strategy" scenario._
- [ ] 13.3 RED: `..._EverySwapDisclosureAndEmbeddedCommissionDisclosure_AppearOnEveryResult` —
  regardless of finding state.
  _Satisfies: spec.md swap/commission disclosure scenarios._
- [ ] 13.4 RED: `..._NoSymbolMappingSupplied_RefusedWithoutStringSimilarityMatch`.
  _Satisfies: spec.md "Simulation without a declared symbol mapping is refused" scenario._
- [ ] 13.5 RED: `..._NoLotGridSupplied_RefusedWithoutSubstitutingImoxRetester`.
  _Satisfies: spec.md "Simulation without a declared lot grid is refused" scenario — never
  `LotGrid.ImoxRetester` as a silent default._
- [ ] 13.6 RED: `..._TwoStrategiesEachNoBreachObserved_NoCombinedPortfolioFindingProduced`.
  _Satisfies: spec.md "Multiple strategies are evaluated independently" scenario._
- [ ] 13.7 GREEN: implement resize-count propagation, per-segment (never merged) result shaping,
  disclosure fields (`NotModelled = [Swap, FtmoCommission, IntradayEquity]`,
  `EmbeddedSourceCommissionRescaled`), declared-mapping/grid enforcement, and per-strategy-only
  scope. Confirm 13.1–13.6 pass.

### Phase 14 — Endpoint

- [ ] 14.1 RED: `StrategyBacktestsControllerTests` — a new GET endpoint (mirror the existing
  direct-instantiation pattern) returns the DTO for a resolved simulation and forwards refusal
  reasons verbatim for a refused one.
- [ ] 14.2 GREEN: add the query endpoint to `WebAPI/Controllers/StrategyBacktestsController.cs`
  per design.md's REST convention (read side). Confirm 14.1 passes.

### Phase 15 — `DailyBreached`/`BreachBasis` deprecation (design.md Decision 7)

- [ ] 15.1 RED: `PortfolioAnalyticsCalculatorTests` (or nearest existing suite) —
  `..._LossLimits_BreachBasisIsVarQuantileComparison_NotClosedTradeLowerBound`.
  _Satisfies: `funding-guardrails` delta — "Legacy DailyBreached readout is relabeled" scenario._
- [ ] 15.2 RED: `..._StagedLossLimits_BreachBasisStaysClosedTradeLowerBound` (regression pin —
  existing behavior must not change).
  _Satisfies: `funding-guardrails` delta — "StagedLossLimits readout stays labelled a lower
  bound" scenario._
- [ ] 15.3 RED: `..._Minus8PctDayVar95At2PctScenario_DailyBreachedValueUnchanged_OnlyLabelChanges` —
  the −8% day / VaR 2% pin from design.md Testing Strategy: `DailyBreached` boolean and
  `DailyHeadroomPct` are byte-identical before/after; only `BreachBasis` differs.
  _Satisfies: `funding-guardrails` delta — "Relabeling is disclosed as a changelog-visible label
  change" scenario._
- [ ] 15.4 GREEN: add `BreachBasis.VarQuantileComparison = 1` to
  `Domain/Enums/BreachBasis.cs`; update the computed switch in
  `Application/DTOs/Portfolios/PortfolioAnalyticsDto.cs:223-228` so `LossLimits` returns
  `VarQuantileComparison`, `StagedLossLimits` keeps `ClosedTradeLowerBound`, `VarTarget` stays
  null. Do NOT touch `PortfolioService.cs:445-459`'s values, only its comment. Do NOT mark
  `DailyBreached`/`DailyHeadroomPct` `[Obsolete]` (warnings-as-errors would break every
  construction site) — add an XML `DEPRECATED — VaR95 comparison` remark instead. Confirm
  15.1–15.3 pass.

### Phase 16 — Web label (`portfolio-detail.component.ts:693`, falsy-zero-safe)

- [ ] 16.1 RED: `portfolio-detail.component.spec.ts` —
  `breachBasisLabel_VarQuantileComparison_RendersNonEmptyLabel_NotEmptyString` — the current
  `breachBasisLabel` at line 693 returns `''` for any value other than
  `ClosedTradeLowerBound`, so this fails until the new branch exists.
  _Satisfies: `funding-guardrails` delta — "The web Risk-tab card does not silently drop the new
  label" scenario._
- [ ] 16.2 RED: `..._ClosedTradeLowerBoundStillRendersItsExistingLabel` (regression pin — the
  existing branch must survive the edit).
- [ ] 16.3 GREEN: add `VarQuantileComparison = 1` to `BreachBasis` enum in
  `app/core/services/portfolio.service.ts`; update `breachBasisLabel` in
  `portfolio-detail.component.ts:693` to add a branch for `VarQuantileComparison` returning a
  non-empty, translated string via `ngx-translate` (EN: "VaR95 comparison, not a real daily
  loss" / ES: "Comparación VaR95, no una pérdida diaria real" — Engram #2769 exact wording per
  design.md Decision 7). Add both keys to `public/assets/i18n/en.json` and `es.json` in the SAME
  commit (Dual-Entry Protocol, `frontend-data.md`). Confirm 16.1–16.2 pass.
- [ ] 16.4 RED: `..._breachBasisLabel_ClosedTradeLowerBoundIsEnumValueZero_TruthinessCheckWouldBeBuggy` —
  a check like `if (basis)` on `ClosedTradeLowerBound` (value 0) would be falsy; assert the
  implementation switches on the value, not truthiness (regression guard against the falsy-zero
  trap named in design.md/Engram #2769).
- [ ] 16.5 GREEN: confirm 16.4 passes against 16.3's implementation (switch/strict-equality based,
  never `if (basis)`).
- [ ] 16.6 Add a `CHANGELOG.md` entry disclosing the `BreachBasis` relabel as label-only, per
  design.md Decision 7 and the `funding-guardrails` delta.

### Phase 17 — Static gates and full suite (P4)

- [ ] 17.1 `dotnet build AppTradingAlgoritmico.slnx -warnaserror` — zero warnings.
- [ ] 17.2 `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes` — no diffs.
- [ ] 17.3 `dotnet test` full backend suite — confirm **723 pre-existing tests** pass plus every
  new P1–P4 backend test.
- [ ] 17.4 `pnpm --dir app.trading.algoritmico.web exec prettier --check .` on touched files — no
  diffs (or `--write` and re-check).
- [ ] 17.5 `pnpm --dir app.trading.algoritmico.web exec tsc --build` (not a bare `tsc --noEmit`
  from repo root) — zero type errors.
- [ ] 17.6 `npx ng test --watch=false` (or `pnpm --dir app.trading.algoritmico.web test`) — confirm
  **409 pre-existing tests across 33 files** pass plus every new P4 frontend test.
- [ ] 17.7 If any pre-existing test/spec fails or a warning/type error appears, stop and
  investigate before patching — every existing call path must be unchanged.

---

## Flagged: items too vague for a checkable task, or requiring a runtime decision

1. **Runtime decision point, not scriptable**: task 1.2 — the Linux-container DST pin outcome is
   unknown until executed there. If it fails, design.md names NodaTime as the fallback; that
   fallback is not decomposed into tasks here because its scope depends on which pins fail.
2. **Deferred to user authorization**: applying `AddFtmoInstrumentSpecs` (task 9.2) to any real
   database is explicitly excluded from every task above; `sdd-apply` must surface this and name
   the target connection before running `dotnet ef database update`.
3. **Endpoint route/verb left to convention**: task 14.2 does not pin an exact route string —
   `backend-api.md` conventions and the existing `StrategyBacktestsController` route pattern
   should decide it at apply time; the design.md Data Flow only specifies "GET ftmo-breach".
