# Tasks: Strategy Portfolio Simulator — Slice 1, REVISION 2 (Strategy-Scoped Import)

This is a **REVISION**, not a rebuild. The previous checklist (51 tasks + 7 + 4
corrections, all `[x]`) shipped a shape the design has now replaced. Everything
still true is listed under "Already Delivered" and MUST NOT be re-written or
re-tested. The numbered checklist below is only the delta.

Scope: import + evidence model. No engine, no resizing, no selection.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 2400–3000 (`additions + deletions`, authored) |
| 400-line budget risk | High |
| Chained PRs recommended | No — maintainer waived the budget |
| Suggested split | Single PR, `size:exception` already recorded and accepted |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

`size:exception` is RECORDED and ACCEPTED. Do not re-open splitting. The work
units below are commit/review boundaries inside ONE PR, not separate PRs.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Inferred attribution demolished, run model reshaped, one migration | PR 1 | `dotnet test --filter FullyQualifiedName~BacktestSchema` | `dotnet ef migrations script` + empty-table check before applying to `localhost/AppTA` | migration `Down` restores rev-1 shape; 4 tables hold 0 rows |
| 2 | Trade-list import: sample-type guard, header guard, slot idempotency | PR 1 | `dotnet test --filter FullyQualifiedName~BacktestImportService` | covered by SQLite harness | revert import service + parser guards; schema untouched |
| 3 | Calibration counts one run per `ContentHash` | PR 1 | `dotnet test --filter FullyQualifiedName~SymbolPointValueCalibrator` | N/A — pure static, no I/O | revert calibrator run-selection; no schema change |
| 4 | WF-export parser, entities, `OosFromDate`, OOS-window resolver | PR 1 | `dotnet test --filter FullyQualifiedName~WalkForward` | N/A — parser is pure; persistence covered by SQLite harness | delete WF parser/service/entities; nothing else references them |
| 5 | REST surface: new nested controller, old batch endpoint deleted | PR 1 | `dotnet test --filter FullyQualifiedName~StrategyBacktestsController` | `dotnet run` + curl all 3 fixtures with JWT | delete controller; `StrategiesController` untouched |
| 6 | Grid readiness marker, one extra query per page | PR 1 | `dotnet test --filter FullyQualifiedName~StrategyServiceBacktestReadiness` | `dotnet run` + `GET /api/trading-accounts/{id}/strategies` | revert `StrategyDto` field + the grouped query |
| 7 | Angular: 3-slot per-row modal, marker cell, old modal deleted | PR 1 | `npx ng test --watch=false` | `pnpm start` → account detail → row action → upload 3 fixtures | remove modal folder + the 4th action button |

## Environment (verified — ignoring these costs time)

- Dev API may lock DLLs → `dotnet test -p:BaseOutputPath=<scratch>/`. Do **NOT**
  set `BaseIntermediateOutputPath` (`MSB4006`). Do not kill the user's process.
- Solution file is `AppTradingAlgoritmico.slnx`, not `.sln`.
- Git Bash: `-warnaserror`, never `/warnaserror`.
- Pre-existing, NOT ours: 3× `CS9113` (AnalyzerRules/BatchStages/BuildingBlocks
  controllers); whitespace format in `StrategyService.cs` 43–45 and 223–231.
- `npx prettier --write` on every frontend file touched.
- Migrations against `localhost/AppTA` are authorized; a verified backup exists.
  **Print the command and the target before running.**

## Strict TDD

Test before implementation. **A compile error is not RED** — every RED task must
capture a behavioural failure against code that builds, or state explicitly why
the failure can only be structural (schema/index tests on the SQLite harness).
Runners: `dotnet test`, `npx ng test --watch=false`.

## Fixtures (`app.trading.algoritmico.api/tests/Fixtures/`)

- **F1** `ListOfTrades_XAUUSD_H1_IST.csv` — 329 trades, all `IST`. POSITIVE
  trade-list fixture. 90 SL-closed XAUUSD trades feed calibration.
- **F2** `WFParamsExport_XAUUSD_H1.csv` — 7 lines. WF-export fixture: comma
  decimals everywhere, inverted `Parameters` field, `N/A` + ` (future)` last
  row, `dd.MM.yyyy` dates.
- **F3** `ListOfTrades_XAUUSD_H1_OOST.csv` — now a **NEGATIVE** fixture. Mixes
  `IS` and `OOS1` → must be REJECTED by the single-sample-type guard. Still
  carries the 27 colliding tickets.

Spec IDs: `SBI-n` sqx-backtest-import · `WF-n` walk-forward-export · `CAL-n`
symbol-point-value-calibration · `SM-1` strategy-model · `AS-n`
account-strategies. Markers: **[S]** sequential · **[P]** parallel-safe in phase.

---

## Already Delivered — DO NOT REBUILD

Shipped and reviewed in rev 1 (backend 303 green, frontend 350 green). These
survive the revision. Plan only the edits listed in the checklist below.

| Delivered | Status in rev 2 |
|---|---|
| `SqxTradeListParserService` core — per-column separators, culture-invariant parsing, length validation | SURVIVES. Edits: delete filename split; add sample-type + header-shape guards |
| `SymbolPointValueCalibrator` — MAE on SL-closed only, floor 3, `Inconsistent` >0.5% | SURVIVES. Edit: de-dup run selection by `ContentHash` |
| `IBacktestDbContextFactory` + `BacktestDbContextFactory` + per-attempt retry safety (WU1) | SURVIVES UNCHANGED — must not regress |
| `Domain/Constants/BacktestFieldLengths.cs` as single source of width truth | SURVIVES. Edit: add WF text widths |
| Per-file exception boundary in `ImportAsync` (WU2) | SURVIVES UNCHANGED — must not regress |
| `BacktestTrade` + `SymbolCalibration` entities and EF configurations | SURVIVE. Edit: `BacktestTrade` gains `(BacktestRunId, CloseTime)` index |
| Tests: `SqxTradeListParserTests`, `SymbolPointValueCalibratorTests`, `BacktestImportRetrySafetyTests`, `BacktestImportBatchResilienceTests`, most of `BacktestSchemaTests` | KEEP. Adapt call shape only where the API changed |
| 3 read-only `GET /api/backtests/*` endpoints + `backtests-list` page | SURVIVE. Edit: drop the `Unmatched` panel |
| Migrations `AddBacktestRunsAndCalibration`, `DeriveBacktestRunAttributionStatus` | Applied to `localhost/AppTA`. Superseded by one new migration; not reverted |
| Binding corrections C1 (floor 3), C2 (per-column decimals), C4 (symbol verbatim), C5 (no account-agnostic risk aggregate) | STILL BINDING |
| Binding correction C3 (`__` filename split) | MOOT — filename parsing deleted |

## Being Replaced — explicit removal required, no orphans

Filename parsing (`StrategyNameKey`/`RunLabel` and every reader) · name matching
/ `FindMatchingStrategyIdsAsync` / duplicate fan-out · `AttributionStatus` +
`BacktestRun.DeriveAttributionStatus` + `BacktestAttributionRepairTests` (all of
rev-1 Work Unit 3) · `BacktestRunStrategy` entity, its configuration and the join
table · the `Reattributed` and `Conflict` outcomes · the `Unmatched` panel in
`backtests-list` · `import-backtests-modal` (standalone multi-file modal) · the
UNIQUE index on `ContentHash` · `POST /api/backtests/import`.

---

## Ordering rationale

Deletions land **before** the new model in Phase 1 and are committed **with** it,
because neither half is viable alone: `StrategyNameKey`/`RunLabel` are `required`
non-nullable and the join table is the only attribution path today, so deleting
filename parsing first leaves a model that cannot construct a run, and adding
`StrategyId` first leaves two competing attribution sources plus a UNIQUE
`ContentHash` that blocks the very case the FK legitimises. The **migration is
scaffolded last in Phase 1** (task 1.13), after both the drops and the adds exist
in the model, so exactly ONE migration is produced and
`has-pending-model-changes` is clean. All four backtest tables hold ZERO rows, so
this surgery is free. Phases 2–4 then build on a settled schema; Phase 5 (REST)
needs 2 and 4; Phase 6 (marker) needs 4's WF entities; Phase 7 (Angular) needs 5.

---

## Phase 1: Demolish inferred attribution + reshape the run model [S — first]

- [x] 1.1 [S] **RED** `BacktestSchemaTests` (SQLite harness): two `BacktestRun`
      rows with the SAME `ContentHash` under different `StrategyId` both persist;
      UNIQUE `(StrategyId, Kind)` rejects a second row in the same slot. RED
      today on the unique `ContentHash` index. [SBI-4]
- [x] 1.2 [S] **RED** `BacktestSchemaTests` with `Foreign Keys=True`: deleting a
      `Strategy` deletes its `BacktestRun` rows AND their `BacktestTrade` rows
      (behaviour change — rev 1 dropped only link rows). [SM-1]
- [x] 1.3 [S] **GREEN — delete** `Domain/Entities/BacktestRunStrategy.cs`,
      `Persistence/Configurations/BacktestRunStrategyConfiguration.cs`,
      `Domain/Enums/AttributionStatus.cs`,
      `Application/DTOs/Backtests/StrategyNameRefDto.cs`,
      `tests/Backtests/BacktestAttributionRepairTests.cs`.
- [x] 1.4 [S] **GREEN** `Domain/Entities/BacktestRun.cs`: drop
      `StrategyNameKey`, `RunLabel`, `StrategyLinks`, `DeriveAttributionStatus`
      and their XML docs; add `StrategyId` (required) and `Kind`.
- [x] 1.5 [P] **GREEN** new `Domain/Enums/BacktestRunKind.cs` —
      `Deploy = 1`, `Evaluation = 2`. No `0` member: an unset kind must not be a
      valid slot.
- [x] 1.6 [P] **GREEN** new `Domain/Entities/StrategyWalkForwardExport.cs`
      (`StrategyId`, `OosFromDate`, `DeployParameters`, `EvaluationParameters`,
      `ContentHash`, `SourceFileName`) + `Domain/Entities/WalkForwardWindow.cs`
      (`RowIndex`, IS/OOS period start+end, `DaysIs`/`DaysOos`, four nullable OOS
      numerics, four IS numerics, `Parameters`, `IsFutureWindow`). Windows are
      stored VERBATIM; no aggregate is persisted. [WF-1, WF-4]
- [x] 1.7 [S] **GREEN** `BacktestRunConfiguration`: DROP unique `ContentHash` and
      unique `(StrategyNameKey, RunLabel)`; ADD FK `StrategyId` →
      `Strategy` `DeleteBehavior.Cascade`, UNIQUE `(StrategyId, Kind)`, and a
      NON-unique index on `ContentHash` (de-dup key, not identity). [SBI-4]
- [x] 1.8 [P] **GREEN** `BacktestTradeConfiguration`: add index
      `(BacktestRunId, CloseTime)` — the readiness query in Phase 6 reads it.
- [x] 1.9 [P] **GREEN** new `StrategyWalkForwardExportConfiguration` (UNIQUE
      `StrategyId`, cascade from `Strategy`) + `WalkForwardWindowConfiguration`
      (UNIQUE `(ExportId, RowIndex)`, cascade). [WF-1]
- [x] 1.10 [P] **GREEN** extend `Domain/Constants/BacktestFieldLengths.cs` with
      the WF text widths (`Parameters`, period text, source filename). AC:
      `BacktestSchemaTests.TextColumnLengths_ComeFromTheSharedConstants` covers
      the new columns — no re-hardcoded literal in the configurations.
- [x] 1.11 [S] **GREEN** `Application/Interfaces/IBacktestDbContext.cs`: drop
      `BacktestRunStrategies` and `GetStrategyNameIndexAsync`; add
      `StrategyWalkForwardExports` + `WalkForwardWindows`. Update `AppDbContext`.
      AC: `BacktestDbContextIsolationTests` still passes — no `StrategyTrades`,
      no tracked `Strategies` DbSet on the importer's surface (D2 intact).
- [x] 1.12 [S] **GREEN** `SqxTradeListParserService`: delete `SplitFileName`
      (`:263`) and the `StrategyNameKey`/`RunLabel` members of
      `ParsedBacktestFileDto`; KEEP `Path.GetFileName()` sanitisation (`:82`) and
      the file-level length guard. Delete the filename-contract tests from
      `SqxTradeListParserTests` (old task 1.11).
- [x] 1.13 [S] Migration `ReshapeBacktestRunsForStrategyScopedImport` — ONE
      migration, scaffolded only after 1.3–1.12. Before applying: print the
      command and the target `Server=localhost;Database=AppTA`, and confirm all
      four backtest tables hold 0 rows. After: assert `StrategyTrades = 1582` and
      `Strategies = 140` unchanged; `dotnet ef migrations
      has-pending-model-changes` clean. `Down` restores the rev-1 shape.

## Phase 2: Trade-list import reshaped [S after 1.13]

- [x] 2.1 [S] **RED** `SqxTradeListParserTests`: **F3** (`IS` on 151 rows,
      `OOS1` on 186) is REJECTED WHOLE, naming BOTH observed values, zero rows
      returned; **F1** (329 rows, all `IST`) still accepted with the literal
      preserved on every trade. [SBI-3]
- [x] 2.2 [P] **RED** header-shape guard: **F2** (13-column WF header) posted to
      a trade-list slot → rejected "expected trade-list header, found a different
      column shape", zero trades persisted. [SBI-6]
- [x] 2.3 [S] **GREEN** both guards in `SqxTradeListParserService` as FILE-level
      checks before any persistence. AC: `SegmentIndex`/`SampleTypeRaw`
      classification stays intact — F3 remains the regression fixture (D15).
- [x] 2.4 [S] **RED** rewrite `BacktestImportServiceTests` onto slot idempotency:
      empty slot → `Imported`; occupied + same `ContentHash` → `Unchanged` with
      ZERO writes; occupied + different hash → `Replaced` (prior trades gone, new
      trades in, still exactly one run for that slot). Delete the attribution
      tests (old 4.3/4.4) and the 5-way outcome test (old 4.5). [SBI-4]
- [x] 2.5 [P] **RED** identical bytes imported to `S1` and `S2` Deploy slots →
      both `Imported`, two rows sharing one `ContentHash`, no unique-constraint
      violation. Anti-regression for the dropped index. [SBI-4]
- [x] 2.6 [P] **RED** a file produced from currently-deployed parameters posted
      to the Evaluation slot is stored `Kind=Evaluation` with NO warning and NO
      content-based check — mislabeling is undetectable by construction. [SBI-5]
- [x] 2.7 [S] **GREEN** `BacktestImportService`: signature becomes one file +
      `strategyId` + `kind` (no batch); decision table keyed on
      `(StrategyId, Kind)` and kept INSIDE the retried unit; delete the
      `Reattributed` and `Conflict` members of `BacktestImportResultDto`. AC: the
      `IBacktestDbContextFactory` per-attempt context and the per-file exception
      boundary are UNCHANGED.
- [x] 2.8 [S] Adapt `BacktestImportRetrySafetyTests` and
      `BacktestImportBatchResilienceTests` to the new call shape ONLY. AC: the
      invoke-twice-equals-invoke-once property and the one-file-fails-others-
      survive property are still asserted and still green.

## Phase 3: Calibration de-duplication [S after 2.7]

- [x] 3.1 [S] **RED** `SymbolPointValueCalibratorTests`: F1's bytes imported for
      `S1` and `S2` (same `ContentHash`) → XAUUSD `SampleCount = 90`, NOT 180;
      `PointValue`/`MinObserved`/`MaxObserved` from the single deduplicated set.
      Without this the 6 strategies deployed on both FTMO-Demo2 and SBDEMO2 make
      `SampleCount` report 370 where 185 is true — and `SampleCount` is exactly
      what the floor of 3 evaluates. [CAL-6]
- [x] 3.2 [P] **RED** two genuinely different XAUUSD files (different hashes)
      BOTH contribute — de-dup is by content hash, not by symbol or strategy.
      [CAL-6]
- [x] 3.3 [S] **GREEN** de-dup at the run-selection step: one run per distinct
      `ContentHash` feeds the SL-closed population. AC: MAE-on-SL-closed-only,
      median + evidence, floor 3 and `Inconsistent` >0.5% are UNTOUCHED. Re-base
      CAL-1's scenario on F1's 90 SL closes (F3 can no longer import).

## Phase 4: Walk-forward export capability [S after 1.13; parallel with 2–3]

- [x] 4.1 [S] **RED** new `tests/Backtests/WalkForwardExportParserTests.cs`
      against **F2**: 6 data rows parsed; `"15239,94"` → `15239.94` and
      `"20,68"` → `20.68` — comma as DECIMAL, not thousands. [WF-2]
- [x] 4.2 [P] **RED** dates are `dd.MM.yyyy`: row 5's
      `"20.02.2019 - 08.05.2024"` → start 2019-02-20. Must-fail guard — the trade
      list's `yyyy.MM.dd` cannot parse this value at all. [WF-2]
- [x] 4.3 [P] **RED** `Parameters` inversion: row 1's
      `"TEMAPeriod1=32,ProfitTargetCoef1=5.4,...,EMAPeriod1=110,"` → exactly 5
      pairs INCLUDING `ProfitTargetCoef1 = 5.4` as one token; the trailing comma
      yields an empty token that is DROPPED, not an error. [WF-3]
- [x] 4.4 [P] **RED** future window, both signals required: row 7 →
      `IsFutureWindow = true` with the 4 OOS fields NULL (never 0), `Days OOS`
      = 381 and all 4 IS columns populated; min elapsed OOS Ret/DD = **0.52**
      (0 would mean `N/A` was parsed as zero); ` (future)` suffix WITHOUT the 4
      `N/A`s — and the reverse — REJECTS as "future-window signal mismatch";
      `N/A` on any non-last row REJECTS naming that row. [WF-4]
- [x] 4.5 [P] **RED** whole-file rejects: `,` delimiter; missing `Parameters`
      column; fewer than 2 data rows ("at least 2 windows required"); **F1**
      posted here rejected by header shape naming the mismatch. [WF-2, WF-5, WF-9]
- [x] 4.6 [S] **GREEN** `Application/Interfaces/IWalkForwardExportParser.cs` +
      `Application/DTOs/Backtests/Parsed{WalkForwardExport,WalkForwardWindow}Dto.cs`
      + `Infrastructure/Services/WalkForwardExportParserService.cs`. AC: its OWN
      `DecimalColumns` table and its OWN date format — ZERO shared policy, code
      or culture state with `SqxTradeListParserService`; `Parameters` is
      deliberately ABSENT from the decimal table. [WF-2, WF-3]
- [x] 4.7 [S] **RED** WF persistence: first import for `S1` → export + 6 windows,
      `OosFromDate = 2025-05-26` (row 6's OOS start), `EvaluationParameters` =
      row 6's text, `DeployParameters` = row 7's text; re-import REPLACES the
      export and its windows and recomputes `OosFromDate`. [WF-1, WF-5]
- [x] 4.8 [P] **RED** reflection fence: `OosFromDate` exists on NO `BacktestRun`
      or `BacktestTrade` member — it is owned only by the export. [WF-5]
- [x] 4.9 [S] **GREEN** WF import persistence reusing `IBacktestDbContextFactory`
      (same per-attempt-context retry shape as the trade-list path).
- [x] 4.10 [S] **RED** new `tests/Backtests/OosWindowResolverTests.cs`: Deploy run
      WITH a WF export present → "none" (not an empty date range, not a
      zero-trade filtered set); Evaluation run with NO export → "none";
      Evaluation + export → every trade with `CloseTime >= 2025-05-26`. [WF-6]
- [x] 4.11 [S] **GREEN** `Domain/Backtests/OosWindowResolver.TryGetOosWindow(run,
      export, out from)` as the ONLY way to obtain an OOS boundary. AC: a repo
      grep finds no other `CloseTime >=` in any OOS code path. [WF-6]
- [x] 4.12 [P] **RED→GREEN** order independence: importing F1 to `S1`'s
      Evaluation slot with NO export → 329 trades persist, OOS window "none",
      marker amber; importing F2 for `S1` afterwards → the boundary becomes
      available with ZERO re-import and NO `BacktestTrade` row rewritten (assert
      row ids/timestamps unchanged). Reverse order: export alone persists with
      `OosFromDate` set and no evaluable run. [WF-7, WF-8]

## Phase 5: REST surface [S after 2.7 and 4.9]

- [x] 5.1 [S] **RED** new `tests/Backtests/StrategyBacktestsControllerTests.cs`:
      `POST /api/strategies/{id}/backtests/bogus` → **400** with the file never
      opened and the service never called; `deploy`/`evaluation` route to the
      matching `Kind`; `POST /api/strategies/{id}/walk-forward`; `GET
      /api/strategies/{id}/backtests`. Non-`.csv` rejected server-side; a
      path-traversal filename arrives at the service bare. [SBI-1, WF-1]
- [x] 5.2 [S] **GREEN** `WebAPI/Controllers/StrategyBacktestsController.cs`,
      following the `TradingAccountStrategiesController` nested-resource
      precedent. `{kind}` is a ROUTE SEGMENT constrained to `deploy|evaluation`,
      not a form field. AC: `StrategiesController` untouched, still 11 endpoints.
- [x] 5.3 [S] **GREEN** delete `POST /api/backtests/import` from
      `WebAPI/Controllers/BacktestsController.cs`; keep the 3 GET reads. Update
      `BacktestsControllerTests` — drop the import cases, keep the read cases.
- [x] 5.4 [S] Runtime harness: `dotnet run`, JWT login as the seeded
      `admin@appta.local`, curl **F1**→`/backtests/deploy` (expect 329 trades),
      **F1**→`/backtests/evaluation`, **F2**→`/walk-forward` (expect 6 windows,
      `OosFromDate = 2025-05-26`), and **F3**→`/backtests/deploy` (expect the
      multi-sample-type rejection naming `IS` and `OOS1`). Delete the test rows
      from the backtest tables afterwards; stop the server.

## Phase 6: Grid readiness marker [S after 4.9]

- [x] 6.1 [S] **RED** new
      `tests/StrategyWorkflow/StrategyServiceBacktestReadinessTests.cs`: no run →
      `None`; Deploy only → `SizingOnly`; Evaluation present but NO WF export →
      `SizingOnly` (not green — the boundary is unavailable, not assumed
      satisfied); Evaluation + export + ≥1 trade at/after `OosFromDate` →
      `Evaluable`. [AS-1]
- [x] 6.2 [P] **RED** cost fence: a page of N strategies issues exactly ONE
      additional query for the marker, independent of N. The grid fetches all
      rows in ONE call (`account-detail.component.ts:707`, `pageSize 500`), so
      this is one extra grouped query per page load, not 123. [AS-1]
- [x] 6.3 [S] **GREEN** new `Domain/Enums/BacktestReadiness.cs` + a derived field
      on `Application/DTOs/Strategies/StrategyDto.cs` + ONE grouped aggregate in
      `StrategyService.GetByAccountAsync` keyed by `pageIds`, mirroring the
      existing `pageIds.Contains(...)` trade query at `StrategyService.cs:79-86`.
      AC: no `Strategy.HasBacktest` column, no client-side join, and the marker
      is DERIVED — there is no column a user could flip (D14). [AS-1]
- [x] 6.4 [P] Update `StrategyServiceGetByAccountTests` and
      `StrategyServiceLiveKpisTests` for the new `StrategyDto` arity.

## Phase 7: Angular [S after 5.2]

- [x] 7.1 [S] **Delete** `features/sqx/backtests/import-backtests-modal/` (4
      files) and its import/usage in `backtests-list.component.ts:19,26`.
- [x] 7.2 [S] **RED→GREEN** `backtests-list`: remove the `Unmatched` panel, its
      computed signal, template block, SCSS and spec assertions; the runs table
      drops `strategyNames`/`attributionStatus` and gains `strategy` + `kind`.
      Page stays read-only.
- [x] 7.3 [S] **RED** `core/services/backtest.service.spec.ts`:
      `importDeploy` / `importEvaluation` / `importWalkForward(strategyId, file)`
      each POST ONE file to its own URL; delete the multi-file `importFiles` test.
- [x] 7.4 [S] **GREEN** `core/services/backtest.service.ts` accordingly — typed
      against the new REST DTOs; keep the thin Observable-returning shape.
- [x] 7.5 [S] **RED→GREEN** new
      `features/broker-accounts/account-detail/import-strategy-backtests-modal/`:
      THREE LABELLED slots (Deploy / Evaluation / WF Export), each independently
      optional and independently re-importable; submitting only Deploy imports
      only Deploy and leaves the other two untouched; a wrong-shaped file
      surfaces the header-mismatch reason for THAT slot with no partial write.
      AC: no unlabelled drop zone and no kind inference anywhere. [AS-2]
- [x] 7.6 [S] **GREEN** `account-detail.component.ts`: a 4th button in the
      Actions cellRenderer (`:390-425`), same plain DOM-builder shape as
      `performanceBtn`/`commentsBtn`/`deleteBtn`, `stopPropagation` included,
      opening the modal for `params.data`. [AS-2]
- [x] 7.7 [P] **GREEN** readiness marker column with a `cellClass` switch
      (white `None` / amber `SizingOnly` / green `Evaluable`) fed by the new
      `StrategyDto` field — a switch over ~20 virtualised visible cells, no
      second HTTP call. [AS-1]
- [x] 7.8 [P] i18n: add the new `SQX.BACKTESTS.*` and readiness keys to BOTH
      `en.json` and `es.json` (neutral, professional Spanish); DELETE the keys
      orphaned by 7.1/7.2. AC: zero hardcoded strings in the new templates.
- [x] 7.9 [S] `npx prettier --write` on every frontend file touched.

## Phase 8: Verification + orphan sweep [S last]

- [x] 8.1 [S] `dotnet test` full suite (use `-p:BaseOutputPath=<scratch>/` if the
      dev API holds DLLs). **Baseline 303.** Expected ≈ **324 (315–330)**:
      −19 removed (8 `BacktestAttributionRepairTests`, ~4 parser filename tests,
      ~7 attribution/5-way import tests) +≈40 new (WF parser ~12, schema ~5,
      trade-list guards ~3, slot idempotency ~4, OOS resolver ~3, order
      independence ~2, calibration de-dup ~2, cascade ~1, controller ~5,
      readiness ~4). Report the ACTUAL number with the delta accounted line by
      line — an unexplained delta is a failure, not a rounding difference.
- [x] 8.2 [S] `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes`
      exit 0; `dotnet build AppTradingAlgoritmico.slnx -warnaserror` shows ONLY
      the 3 pre-existing `CS9113`.
- [x] 8.3 [S] `npx ng test --watch=false`. **Baseline 350.** Expected ≈ **354
      (348–360)**: −7 removed (5 `import-backtests-modal`, ~2 Unmatched panel)
      +≈11 new (3-slot modal ~6, action button + marker ~3, service ~2). Also
      `tsc --noEmit` on `tsconfig.json` AND `tsconfig.spec.json`, and
      `prettier --check` on every changed frontend file.
- [x] 8.4 [S] Orphan sweep — repo grep for `StrategyNameKey`, `RunLabel`,
      `AttributionStatus`, `BacktestRunStrategy`, `FindMatchingStrategyIds`,
      `Reattributed`, `import-backtests-modal`, `api/backtests/import`. AC: zero
      hits outside `openspec/changes/**` history.
- [x] 8.5 [S] Map all 24 requirements / 48 scenarios across the 5 spec domains to
      a named passing test. AC: no orphaned requirement, no orphaned test.

## Threat Matrix

Design records the matrix as N/A (no routing, shell, subprocess, VCS automation
or executable-file classification). The two security-relevant surfaces are
covered by explicit RED tasks: `Path.GetFileName()` on `IFormFile.FileName` plus
the server-side extension whitelist (**5.1**), and unknown `{kind}` rejected at
model binding before the service or the file is touched (**5.1**).

## Deliberately OUT of scope (deferred, not silently solved)

The simulator engine, resizing, selection and R-normalization. `SimulationResult`
+ `EvidenceProfile` (D14) and the WF robustness aggregates (D13) — slice 1
RECORDS the windows, slice 3 JUDGES them. Recalibration is still not triggered by
a strategy DELETE, and D1's cascade now removes trades rather than links, so a
stale `SymbolCalibration` is slightly MORE reachable than in rev 1: known gap,
recorded, not solved here. The exception boundary around
end-of-batch calibration was CLOSED in revision 3 (WU5) — see below.


---

# REVISION 3 — rev2 review correction (8 corroborated findings, 1 transaction, 8 work units)

Not new feature work: every item below repairs something the rev2 review proved
wrong in shipped code. Baseline backend 352 / frontend 366; final 365 / 371.

## Visible defects

- [x] R3.1 [WU1] The grid rendered raw i18n keys. `readinessLabel` returned
      `'SQX.BACKTESTS.READINESS_*'` and the component injected no translator, so
      ag-grid's `valueFormatter` wrote the key verbatim into every row. Labels now
      resolve through `TranslateService.stream` into a signal that `columnDefs`
      depends on. AC: the marker column's own formatter returns "Evaluable" /
      "Sizing only" / "None", and follows a language switch.
- [x] R3.2 [WU2] A header-only CSV was accepted as a successful import, and
      `ReplaceAsync` wiped an occupied slot while reporting `Replaced`. BOTH halves
      fixed: the parser rejects a zero-usable-row file file-level, and the readiness
      aggregate requires a run that HOLDS TRADES (`HasAnyRun` → `HasSizingEvidence`).
      AC: header-only file is Rejected and writes nothing; a zero-trade run is
      `None`, not `SizingOnly`.
- [x] R3.3 [WU3] A backend failure rendered identically to "nothing imported yet"
      — `loadRuns` swallowed the error, `loadCalibrations` had no error callback at
      all and the rejection escaped the component. Per-panel error signals, rendered
      as `role="alert"`, gating the empty state. AC: a failing load shows the error
      and never the empty state; the two panels fail independently.

## Integrity

- [x] R3.4 [WU4] `WalkForwardExportParserService` never referenced
      `BacktestFieldLengths` while writing `Parameters` into `nvarchar(1000)` and a
      filename into `nvarchar(260)`. Both guards added, FILE-level (row order is
      meaning). AC: over-length `Parameters` rejects naming the row and the limit;
      exactly-at-limit is accepted.
- [x] R3.5 [WU5] Concurrent imports raced on the calibration upsert — an unguarded
      read-then-insert against a UNIQUE index, called OUTSIDE the exception boundary,
      so the loser became a bare 500 for a request whose rows had already committed.
      Both halves: the upsert retries once on a fresh context (converging on the
      winner's row), and the call moved inside the boundary WITHOUT rewriting the
      import's true outcome — the failure is carried in `Reason` and rendered in the
      modal as a warning. AC: losing the race still imports and still calibrates; a
      permanent calibration fault reports `Imported` + a named reason, never a throw.

- [x] R3.6 [WU6] The migration had no working rollback: `Down()` re-added
      `StrategyNameKey`/`RunLabel` with `defaultValue: ""` and then created a UNIQUE
      index over that pair, which collides at two rows — the ordinary steady state.
      `Down()` now discards the backtest rows first and documents the loss.
      EDITED the existing migration rather than adding a new one, because `Down()`
      is per-migration and a later migration cannot repair an earlier one's.
      AC: no unique index is created over surviving rows that cannot satisfy it.

## Honesty

- [x] R3.7 [WU7] `design.md` D8 asserted the OPPOSITE of the implemented
      invariant ("returns an empty sequence" for a Deploy run — the permissive shape
      `OosWindow` exists to make unrepresentable). Corrected, along with the stale
      parser/interface names, `Parameters (500)` → `WalkForwardParameters (1000)`,
      the D12 marker rule, and two data-flow entries naming things never built. Two
      dead doc-comment targets fixed in production source. AC: no documented claim
      contradicts shipped behaviour.
- [x] R3.8 [WU8] `TryGetOosWindow` never checked that the export belonged to the
      run's strategy, and the tests paired two INDEPENDENT `Guid.NewGuid()` values —
      certifying "an unrelated strategy's boundary is valid for this run" as the
      contract. Ownership check added; fixtures now share an owning strategy.
      AC: a foreign export yields no window.

## Deliberately NOT fixed in revision 3 (recorded in the ledger)

`rejectedRowCount` never rendered; zero logging across the slice; calibration
staleness when a replace changes symbol; a strategy delete not triggering
recalibration; raw provider messages in the import response; missing row/size caps
on uploads; the readiness aggregate having no fallback if its tables are absent;
`BacktestReadiness.None` doubling as a not-computed placeholder; `ParseAsync`
length/complexity.
