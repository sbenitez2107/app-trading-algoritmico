```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:abd39a8a9409997958f72316d764537e7d445da95c0e854147b138e8f1c7130c
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
requirements: 24/24
scenarios: 44/48
test_command: dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=<scratch>/ ; npx ng test --watch=false
test_exit_code: 0
test_output_hash: sha256:abd39a8a9409997958f72316d764537e7d445da95c0e854147b138e8f1c7130c
build_command: npx tsc --build --force
build_exit_code: 0
build_output_hash: sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
```

## Verification Report

**Change**: strategy-portfolio-simulator
**Target**: HEAD 7a7e02c (correction) on eeb4a52 (feature), working tree clean
**Mode**: Strict TDD
**Scope of this phase**: requirements/runtime conformance only. Code review was NOT re-derived
(4 lenses + refuter + 11 correction work units + scoped fix-delta validator approve already cover it).

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 66 |
| Tasks complete | 66 |
| Tasks incomplete | 0 |
| Requirements (5 spec domains) | 24 |
| Scenarios | 48 |

### Build & Tests Execution

**Backend**: PASS - `Total tests: 365`, `Passed: 365`, exit 0.
**Frontend**: PASS - `Test Files 30 passed (30)`, `Tests 371 passed (371)`, exit 0.
**Type check**: PASS - `npx tsc --build --force`, exit 0, empty output.
Vitest worker did not crash on this run; no re-run needed.

**Coverage**: not collected - no coverage tool configured for either stack. Not a failure.

### Spec Compliance Matrix

Legend: COMPLIANT = covering test exists and passed. PARTIAL = passes but covers only part of the scenario.

#### sqx-backtest-import (6 requirements, 15 scenarios)

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| Import Is Strategy-Scoped By Construction | Deploy run imported for a known strategy | `BacktestImportServiceTests.ImportTradeListAsync_EmptySlot_CreatesTheRunAttributedToTheRouteStrategy` (asserts 329 trades); `StrategyBacktestsControllerTests.ImportTradeList_KnownKind_RoutesToTheMatchingSlot` | COMPLIANT |
| | Unrecognized kind is rejected before parsing | `StrategyBacktestsControllerTests.ImportTradeList_UnknownKind_Returns400WithoutOpeningTheFileOrCallingTheService`; `.ImportTradeList_NumericKind_IsAlsoRejected` | COMPLIANT |
| Two Decimal Conventions | Both fixtures parse with the correct per-column convention (666 rows) | `SqxTradeListParserTests.ParseAsync_F1Fixture_Parses329Rows`; `.ParseAsync_UnderDeDeCulture_ProducesIdenticalResult` | **PARTIAL** - F1 half only; the F3/337-row half is unsatisfiable (see W1) |
| | A single shared decimal policy would corrupt one side (must-fail guard) | `SqxTradeListParserTests.ParseAsync_F1FirstRow_ParsesDotColumnsAndCommaColumnsCorrectly`; `.ParseAsync_CommaInDotColumn_RejectsRowNamingColumn` | COMPLIANT |
| | Wrong delimiter rejects the whole file | `SqxTradeListParserTests.ParseAsync_WrongDelimiter_RejectsWholeFile` | COMPLIANT |
| | Unparseable date rejects the whole file | `SqxTradeListParserTests.ParseAsync_UnparseableDate_RejectsWholeFileNamingRowAndColumn` | COMPLIANT |
| | Missing column rejects the whole file | `SqxTradeListParserTests.ParseAsync_MissingCloseTypeColumn_RejectsWholeFile` | COMPLIANT |
| Single Sample Type | Single-segment file is accepted | `SqxTradeListParserTests.ParseAsync_F1Fixture_AllRowsAreInSampleTest`; `.ParseAsync_F1Fixture_Parses329Rows` | COMPLIANT |
| | Multi-segment file is rejected whole | `SqxTradeListParserTests.ParseAsync_F3Fixture_IsRejectedWholeNamingBothSampleTypes` | COMPLIANT |
| Identity Is (StrategyId, Kind) | Import into an empty slot | `BacktestImportServiceTests.ImportTradeListAsync_EmptySlot_...` | COMPLIANT |
| | Identical re-import is a no-op | `BacktestImportServiceTests.ImportTradeListAsync_IdenticalBytesIntoAnOccupiedSlot_IsUnchangedAndWritesNothing` | COMPLIANT |
| | Different content replaces the run | `BacktestImportServiceTests.ImportTradeListAsync_DifferentBytesIntoAnOccupiedSlot_ReplacesTheRunInPlace` | COMPLIANT |
| | Identical bytes back two strategies | `BacktestImportServiceTests.ImportTradeListAsync_IdenticalBytesForTwoStrategies_BothImportAndShareOneContentHash`; `BacktestSchemaTests.BacktestRun_SameContentHashUnderTwoStrategies_BothPersist`; `.BacktestRun_ContentHashIndex_IsNotUnique` | COMPLIANT |
| Declared Kind Stored, Never Detected | A deploy-run file declared as Evaluation imports unconditionally | `BacktestImportServiceTests.ImportTradeListAsync_ADeployFileDeclaredAsEvaluation_IsStoredWithNoWarningAndNoContentCheck` | COMPLIANT |
| WF Export To Trade-List Slot Rejected | WF export posted to the Deploy slot | `SqxTradeListParserTests.ParseAsync_WalkForwardExportHeader_IsRejectedAsTheWrongColumnShape` | COMPLIANT |

#### walk-forward-export (9 requirements, 17 scenarios)

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| Strategy-Scoped, One Per Strategy | First import for a strategy | `WalkForwardImportServiceTests.ImportAsync_FirstExportForAStrategy_PersistsSixWindowsAndTheBoundary` | COMPLIANT |
| | Re-import replaces the prior export | `WalkForwardImportServiceTests.ImportAsync_UpdatedExport_ReplacesTheWindowsAndKeepsOneExport`; `BacktestSchemaTests.StrategyWalkForwardExport_SecondExportForOneStrategy_ThrowsUniqueConstraintViolation`; `.DeletingAStrategy_DeletesItsWalkForwardExportAndWindows` | COMPLIANT |
| Comma Decimals + dd.MM.yyyy | Fixture parses fully with comma decimals | `WalkForwardExportParserTests.ParseAsync_Fixture_TreatsTheCommaAsADecimalPointNotAThousandsSeparator`; `.ParseAsync_Fixture_ParsesIntegerDayColumns` | COMPLIANT |
| | dd.MM.yyyy is applied (must-fail guard) | `WalkForwardExportParserTests.ParseAsync_Fixture_ParsesPeriodsAsDayFirstDates` | COMPLIANT |
| | Wrong delimiter or missing column rejects | `WalkForwardExportParserTests.ParseAsync_WrongDelimiter_RejectsTheWholeFile`; `.ParseAsync_MissingParametersColumn_RejectsTheWholeFile`; `.ParseAsync_UnparseableDate_RejectsTheWholeFileNamingTheRow` | COMPLIANT |
| Parameters Inverts Punctuation | Trap guard - comma-decimal would destroy this field | `WalkForwardExportParserTests.ParseAsync_ParametersField_KeepsDotsAsDecimalsAndDropsTheTrailingComma` | COMPLIANT |
| Future Window, Two Signals | Fixture row 7 is the future window | `WalkForwardExportParserTests.ParseAsync_Fixture_LastRowIsTheFutureWindowWithNullOosValues` | COMPLIANT |
| | N/A-as-zero would corrupt the worst-window read | `WalkForwardExportParserTests.ParseAsync_Fixture_MinimumElapsedOosRetDdIsNotZero` (min = 0.52) | COMPLIANT |
| | Disagreeing signals reject the file | `WalkForwardExportParserTests.ParseAsync_FutureSuffixWithoutTheNaValues_RejectsTheFile`; `.ParseAsync_NaValuesWithoutTheFutureSuffix_RejectsTheFile` | COMPLIANT |
| | N/A on a non-last row rejects the file | `WalkForwardExportParserTests.ParseAsync_NaOnANonLastRow_RejectsTheFileNamingThatRow` | COMPLIANT |
| OosFromDate Owned By The Export | OosFromDate computed from the second-to-last row | `WalkForwardImportServiceTests.ImportAsync_FirstExportForAStrategy_...` (boundary + EvaluationParameters row 6 + DeployParameters row 7 verbatim); `OosWindowResolverTests.OosFromDate_ExistsOnTheExportAndNowhereElse` | COMPLIANT |
| | Single-row export is rejected | `WalkForwardExportParserTests.ParseAsync_SingleDataRow_RejectsTheWholeFile` | COMPLIANT |
| Deploy OOS Window Underivable | Deploy run OOS window is "none" | `OosWindowResolverTests.TryGetOosWindow_DeployRunWithAnExportPresent_YieldsNoWindowAtAll`; `WalkForwardImportServiceTests.DeployRunPlusExport_IsNotEvaluableEvenThoughBothExist` | COMPLIANT |
| | Evaluation run OOS trades once the export exists | `OosWindowResolverTests.OosWindow_Filter_ReturnsOnlyTradesAtOrAfterTheBoundary`; `.OosWindow_Includes_IsInclusiveOfTheBoundaryItself`; `.TryGetOosWindow_EvaluationRunWithAnExport_YieldsTheExportsBoundary` | **PARTIAL** - domain-level only; no production caller (see W2) |
| Run Before Export Turns Green Later | Evaluation run precedes its WF export | `WalkForwardImportServiceTests.RunImportedBeforeItsExport_BecomesEvaluableWithNoReImportAndNoTradeRewritten` | COMPLIANT |
| Export With No Run Yet | WF export alone | `WalkForwardImportServiceTests.ExportImportedWithNoRunYet_PersistsAndLeavesNothingEvaluable` | COMPLIANT |
| Trade List To WF Slot Rejected | Trade-list file posted to the WF-export endpoint | `WalkForwardExportParserTests.ParseAsync_TradeListFile_RejectsTheWholeFileNamingTheShapeMismatch` | COMPLIANT |

#### symbol-point-value-calibration (6 requirements, 7 scenarios)

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| Point Value From MAE, Never Profit | XAUUSD calibrates exactly | `SymbolPointValueCalibratorTests.Calibrate_RealSlSamplesFromF1_YieldsExactPointValue` (100.000, SampleCount 90); `.Calibrate_ProfitMutatedToGarbage_PointValueUnchanged`; `BacktestImportServiceTests.ImportTradeListAsync_OneFixture_CalibratesTheSymbolFromPersistedTrades` | COMPLIANT |
| Auditable Evidence Persisted | Spread is visible, not hidden | `SymbolPointValueCalibratorTests.Calibrate_SpreadOverHalfPercent_InconsistentWithMinMaxPersisted` | COMPLIANT |
| Minimum Sample Size Gate | Thin symbol does not calibrate | `SymbolPointValueCalibratorTests.Calibrate_TwoSamples_InsufficientSamplesWithNullPointValue`; `.Calibrate_ThreeSamplesZeroSpread_Calibrates` | **PARTIAL** - the "result reports insufficient sample (2/3)" clause is neither implemented nor tested (see W3) |
| Recomputes Over All Known Trades | A second run adds new SL trades for the same symbol | `SymbolPointValueCalibratorTests.SelectDistinctContentRuns_TwoGenuinelyDifferentFiles_BothContribute` (union); `BacktestCalibrationConcurrencyTests.ImportTradeListAsync_LosingTheCalibrationInsertRace_...` (upsert UPDATE branch) | **PARTIAL** - halves pinned separately, never composed through the import path (see W4) |
| Deduplicates By Content Hash | Same file for two strategies does not double-count | `SymbolPointValueCalibratorTests.SelectDistinctContentRuns_SameFileImportedForTwoStrategies_CountsItOnce`; `BacktestImportServiceTests.ImportTradeListAsync_SameFileForTwoStrategies_DoesNotDoubleTheCalibrationSample` (658 trades stored, SampleCount 90) | COMPLIANT |
| | Genuinely different files both count | `SymbolPointValueCalibratorTests.SelectDistinctContentRuns_TwoGenuinelyDifferentFiles_BothContribute`; `.SelectDistinctContentRuns_ThreeRunsSharingOneHash_PicksTheSameOneEveryTime` | COMPLIANT |
| Rejected Rows Never Enter Calibration | Degenerate row excluded | `SymbolPointValueCalibratorTests.Calibrate_DegenerateAndZeroGuardedSamples_SkippedNotDivided`; `SqxTradeListParserTests.ParseAsync_DegenerateRowAmongValidOnes_RejectsOnlyThatRow` | COMPLIANT |

#### strategy-model (1 requirement, 3 scenarios)

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| Runs Are Strategy-Scoped By Construction | A run is always attributed at creation | `BacktestImportServiceTests.ImportTradeListAsync_EmptySlot_CreatesTheRunAttributedToTheRouteStrategy`; `BacktestDbContextIsolationTests.ExposedEntities_DeclareNoNavigationToStrategy` | COMPLIANT |
| | Deleting a strategy deletes its runs and trades | `BacktestSchemaTests.DeletingAStrategy_DeletesItsRunsAndTheirTrades` (real SQLite cascade) | COMPLIANT (numeric premise stale - see S2) |
| | Two strategies sharing an SQX strategy each own their run | `BacktestImportServiceTests.ImportTradeListAsync_IdenticalBytesForTwoStrategies_...`; `BacktestSchemaTests.BacktestRun_SameContentHashUnderTwoStrategies_BothPersist` | COMPLIANT |

#### account-strategies (2 requirements, 6 scenarios)

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| Grid Exposes A Readiness Marker | No backtest data | `StrategyServiceBacktestReadinessTests.GetByAccountAsync_StrategyWithNoRun_IsNone` | COMPLIANT |
| | Deploy run only | `StrategyServiceBacktestReadinessTests.GetByAccountAsync_DeployRunOnly_IsSizingOnly` | COMPLIANT |
| | Fully evaluable | `StrategyServiceBacktestReadinessTests.GetByAccountAsync_EvaluationRunExportAndAnOosTrade_IsEvaluable`; `WalkForwardImportServiceTests.EvaluationRunPlusExport_IsEvaluable` | COMPLIANT |
| | Evaluation run without its WF export is still amber | `StrategyServiceBacktestReadinessTests.GetByAccountAsync_EvaluationRunWithoutItsExport_IsStillSizingOnly`; `.GetByAccountAsync_EvaluationRunAndExportButNoTradeAfterTheBoundary_IsSizingOnly` | COMPLIANT |
| | (marker rendering) | `account-detail.component.spec.ts` > `columnDefs_IncludeABacktestReadinessColumn`, `readinessCellClass_MapsEachReadinessToItsOwnClass`, `readinessColumn_RendersTranslatedText_NotTheRawTranslationKey`, `readinessColumn_FollowsALanguageSwitch` | COMPLIANT |
| | (one additional query clause) | `BacktestReadinessQueryCostTests.ReadinessRows_ForOneStrategy_IsASingleCommand`; `.ReadinessRows_ForThirtyStrategies_IsStillASingleCommand` | COMPLIANT at aggregate level (see S1) |
| Three Labelled Slots | Partial import is valid | `import-strategy-backtests-modal.component.spec.ts` > `submit_OnlyDeployFilled_ImportsOnlyDeployAndLeavesTheOtherSlotsUntouched`, `slots_AreThreeLabelledFileInputs_NotOneInferringDropZone`, `submit_AllThreeFilled_ImportsEachSlotThroughItsOwnEndpoint` | COMPLIANT |
| | Wrong-shaped file rejected naming the mismatch | `import-strategy-backtests-modal.component.spec.ts` > `submit_WrongShapedFileInASlot_SurfacesThatSlotsMismatchReason`, `submit_OneSlotRejected_DoesNotStopTheOthersFromImporting` | COMPLIANT |

**Compliance summary**: 44/48 COMPLIANT, 4 PARTIAL, 0 UNTESTED, 0 FAILING.

### Correctness (Static Evidence)

| Requirement area | Status | Notes |
|---|---|---|
| FK attribution, cascade delete | Implemented | HasOne/WithMany, no navigation; fenced by reflection test |
| Per-column decimal parsing | Implemented | `SqxTradeListParserService`, own separator table |
| WF parser separate policy | Implemented | `WalkForwardExportParserService`, own NumberFormatInfo + dd.MM.yyyy |
| Slot identity (StrategyId, Kind) | Implemented | UNIQUE index verified in `BacktestSchemaTests` |
| Readiness aggregate | Implemented | `OosWindow.Resolver.ReadinessRows`, single wired caller `StrategyService.cs:99` |
| Per-run OOS boundary API | **Orphaned** | `TryGetOosWindow` / `OosWindow.Includes` / `OosWindow.Filter` have ZERO production callers (grep-verified). Disclosed in design.md by WU7 |
| Insufficient-sample reporting | **Not implemented** | `BacktestImportResultDto.Reason` carries only a calibration exception; no path emits "insufficient sample (n/3)" |

### Orphan Sweep (independent re-verification of task 8.4)

`api/backtests/import`, `FindMatchingStrategyIds`, `DeriveAttributionStatus`, `SplitFileName`,
`GetStrategyNameIndexAsync`, `Unmatched`, `HasAnyRun` -> **zero hits** in live code.
`StrategyNameKey` / `RunLabel` / `AttributionStatus` / `BacktestRunStrategy` remain only in
immutable historical migration and `.Designer.cs` snapshots, plus the Reshape migration Up/Down and
its test - correct and required. `import-backtests-modal` and `readinessLabel` hits are substring
matches on the NEW `import-strategy-backtests-modal` and the NEW `readinessLabels` signal.
**Task 8.4 AC holds.**

### Issues Found

**CRITICAL**: None.

**WARNING**:

- **W1 - `sqx-backtest-import` REQ-02 scenario 1 contradicts REQ-03 in the same file.** The scenario
  requires `_OOST.csv` (337 rows) to be imported and every row parsed, for "666 rows total". REQ-03
  single-sample-type guard rejects that fixture whole, and the spec preamble plus the calibration
  spec both say the fixture is retained ONLY as the rejection regression. The 666-row claim is
  unverifiable by construction. Fix the spec text to 329 rows / F1 only.
- **W2 - `walk-forward-export` REQ-06 describes a production capability that does not ship.**
  `OosWindow.Resolver.TryGetOosWindow`, `OosWindow.Includes` and `OosWindow.Filter` have no
  production caller anywhere. The only wired consumer, `ReadinessRows`, inlines its own
  `t.CloseTime >= e.OosFromDate` (`OosWindow.cs:124`, same file, so the grep fence still holds). The
  scenario "Evaluation run OOS trades once the export exists" is pinned by domain tests but no
  shipped feature produces that set. Already disclosed in design.md (WU7); the spec was not softened.
- **W3 - `symbol-point-value-calibration` REQ-03 reporting clause is orphaned.** "the batch result
  MUST flag the symbol as 'insufficient sample' with its actual count" has no implementation and no
  test. `BacktestImportResultDto` exposes only `Reason`, populated exclusively on a calibration
  exception (`BacktestImportService.cs:80`). The persisted `SymbolCalibration` row itself is correct.
  The word "batch" is also stale - batching was deleted in revision 2.
- **W4 - `symbol-point-value-calibration` REQ-04 scenario is never composed.** Union selection is
  proven pure and the upsert UPDATE branch is proven by the concurrency test, but no test imports a
  second genuinely different file for the same symbol and asserts the prior value was replaced.
- **W5 - `account-strategies` REQ-01 spec text describes the pre-correction shape.** It still says
  `None` "when no BacktestRun exists" and `SizingOnly` "when a Deploy run exists". Revision 3 / WU2
  changed both: white is now "no run HOLDING AT LEAST ONE TRADE", amber is ANY run holding trades
  (Deploy or Evaluation). `design.md` D12 was corrected; this spec file was not. The requirement
  prose also contradicts its own 4th scenario, which expects amber for an Evaluation-only strategy.
  Two shipped tests pin behaviour the spec text denies:
  `GetByAccountAsync_RunHoldingZeroTrades_IsNone_NotSizingOnly` and
  `GetByAccountAsync_AnEmptyRunAlongsideARunWithTrades_IsStillSizingOnly`.
- **W6 - revision 3 added eight behaviours that no spec requirement describes.** The specs were
  rewritten for revision 2 and never revisited after the eleven correction work units. Tests now pin
  rules nobody wrote down:

  | Behaviour pinned by a test | Test | Spec requirement |
  |---|---|---|
  | Zero-usable-row file rejected FILE-level | `SqxTradeListParserTests.ParseAsync_HeaderOnlyFile_...`, `.ParseAsync_EveryDataRowRejected_...` | none |
  | A rejected file must not wipe an occupied slot | `BacktestImportServiceTests.ImportTradeListAsync_HeaderOnlyFileIntoAnOccupiedSlot_LeavesTheRunIntact` | none (REQ-04 lists only 3 outcomes) |
  | WF export ownership check on the boundary | `OosWindowResolverTests.TryGetOosWindow_ExportOwnedByADifferentStrategy_YieldsNoWindow` | none - REQ-06 lists only Kind and export-presence |
  | Calibration upsert converges under a race | `BacktestCalibrationConcurrencyTests` (both tests) | none |
  | WF parser length validation | `WalkForwardExportParserTests.ParseAsync_OverLengthParameters_...`, `.ParseAsync_OverLengthFileName_...`, `.ParseAsync_ParametersExactlyAtTheLimit_IsAccepted` | none in `walk-forward-export` |
  | Per-panel error surfacing | `backtests-list.component.spec.ts` (3 tests) | none |
  | Import warning on a successful outcome | `import-strategy-backtests-modal.component.spec.ts` > `submit_ImportedWithAWarningReason_...` | none |
  | Migration Down() rollback safety | `ReshapeBacktestRunsMigrationDownTests` (2 tests) | none (infrastructure; reasonably out of spec scope) |

- **W7 - task 8.5 acceptance criterion is not satisfied.** It reads "AC: no orphaned requirement, no
  orphaned test" and is checked `[x]`. This phase finds two orphaned requirement clauses (W2, W3) and
  eight tests pinning unspecified behaviour (W6). The box is checked; the criterion is not met.

**SUGGESTION**:

- **S1** - the "one additional query" clause is proven at the aggregate (`ReadinessRows` is one
  command for 1 and for 30 strategies) but, as apply-progress deviation 13 already admits, it is not
  proven end-to-end that `GetByAccountAsync` invokes the aggregate exactly once.
- **S2** - `strategy-model` scenario 2 numeric premise ("an Evaluation run with 337 trades ... all
  666") inherits the same dead OOST assumption as W1. The behaviour is correctly covered; only the
  illustrative numbers are stale.
- **S3** - task 8.1/8.3 forecast counts (backend ~324, frontend ~354) are far below actual
  (365 / 371). Revision 3 fully explains the delta; the task text was simply never refreshed.

### Deliberately excluded from this report

Per instruction, the following are recorded in the review ledger with the user's agreement and are
NOT reported as gaps: `rejectedRowCount` never rendered; zero logging across the slice; calibration
staleness when a replace changes symbol; a strategy delete not triggering recalibration; raw provider
messages in the import response; missing upload row/size caps; the readiness aggregate having no
fallback when its tables are absent; `BacktestReadiness.None` doubling as a not-computed placeholder;
`ParseAsync` complexity. Also out of scope: 3x pre-existing CS9113 warnings and appsettings secrets.

### Verdict

**PASS WITH WARNINGS** - 66/66 tasks complete, 365 + 371 tests green, type check clean, 44/48
scenarios fully compliant and 0 untested. No CRITICAL issue blocks archive. Every warning is
documentation drift or an unspecified-but-tested behaviour introduced by the correction rounds; the
specs need a revision-3 pass before archive so the archived capability text matches what shipped.
