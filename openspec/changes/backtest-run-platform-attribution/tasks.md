# Tasks: BacktestRun platform attribution (slice C of layer 0)

Strict TDD. Every RED test task precedes its GREEN implementation task, including the migration
and the Angular components — a compile failure counts as RED where the member under test does not
exist yet. Two chained PRs on `feature/backtest-run-platform-attribution` (design.md D5, Feature
Branch Chain): **C1 — backend**, **C2 — Angular**. Each unit cites the spec requirement/scenario or
design decision it satisfies.

Pattern to copy: `BacktestSchemaTests.cs` (EF contract idiom, `BacktestTestDbContext`),
`BacktestImportServiceTests.cs`, `StrategyBacktestsControllerTests.cs` (direct-instantiation —
model binding never runs, per design D1), `20260426190244_AddInitialBalanceToTradingAccount.cs`
(migration backfill precedent, weaker claim style — do not copy its comment tone verbatim). Read
`.claude/conventions/backend-core.md`, `backend-data.md`, `backend-testing.md` before touching any
`.cs` file; `frontend-core.md`, `frontend-data.md` before touching any Angular file.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | C1 ~395 floor (600–800 realistic) / C2 ~350 floor (525–700 realistic) |
| 400-line budget risk | High (both PRs) |
| Chained PRs recommended | Yes |
| Suggested split | C1 (backend) → C2 (Angular) |
| Delivery strategy | ask-on-risk (default; not overridden for this run) |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

C1 sits on the 400-line budget at its floor and design.md states it cannot be honestly split
further (the column without the migration is undeployable; the migration without the column is
meaningless; the plumbing without either writes nowhere). If either PR's floor is exceeded, that is
a `size:exception` conversation, not a further split.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Nullable `SourcePlatform` column, migration, import plumbing, controller guard, read DTOs | PR C1 (base: `feature/backtest-run-platform-attribution`) | `dotnet test --filter "FullyQualifiedName~Backtests"` | `dotnet test app.trading.algoritmico.api` (full suite once) | Revert migration + the 9 modified backend files; additive-only, no existing behavior changes when the parameter is omitted |
| 2 | Real `PlatformType` enum, service param, modal selector, list column, i18n | PR C2 (base: PR C1's branch) | `pnpm --dir app.trading.algoritmico.web test` | `pnpm --dir app.trading.algoritmico.web test` (full suite once) | Revert the 6 Angular files + 2 i18n files; C1's API tolerates an omitted query param unchanged |

---

## PR C1 — Backend

### Phase 1 — Domain property + EF configuration (D9's three-assertion contract test)

- [x] 1.1 RED: in `BacktestSchemaTests.cs`, add `BacktestRun_SourcePlatform_IsNullableWithNoDefaultAnywhere` —
  the three assertions from design D9 in one test: (1) `typeof(BacktestRun).GetProperty(nameof(BacktestRun.SourcePlatform))!.PropertyType.Should().Be(typeof(PlatformType?))`;
  (2) `new BacktestRun { SourceFileName = "f.csv", ContentHash = "h" }.SourcePlatform.Should().BeNull()`;
  (3) via `BacktestTestDbContext`, `db.Model.FindEntityType(typeof(BacktestRun))!.FindProperty(nameof(BacktestRun.SourcePlatform))!` has `IsNullable == true`, `GetDefaultValue() == null`, `GetDefaultValueSql() == null`. This will not compile yet — that non-compile IS the RED.
  _Satisfies: "`SourcePlatform` Is Nullable, Caller-Declared, And Never Derived" — both scenarios;
  design D9._
- [x] 1.2 GREEN: add `public PlatformType? SourcePlatform { get; set; }` to `Domain/Entities/BacktestRun.cs`
  with XML remarks stating it is caller-declared, never derived, and that null means "not declared
  at import" (proposal Affected modules table).
- [x] 1.3 GREEN: add `builder.Property(x => x.SourcePlatform);` to `BacktestRunConfiguration.cs` after
  the `Kind` line, with the comment from design D2 (no `IsRequired`, no `HasDefaultValue`, no index).
  Confirm 1.1 passes.
- [x] 1.4 Falsification (mandatory — this assertion is load-bearing per design D9): temporarily change
  1.2 to `public PlatformType SourcePlatform { get; set; }` (non-nullable) and re-run 1.1 — confirm
  assertion (1) fails red while assertion (3) may still read `IsNullable == false` consistently (the
  exact failure mode the risk describes: a non-nullable property with a matching EF model). Then
  separately (with 1.2 restored to nullable) add `.HasDefaultValue(0)` to 1.3's line and re-run —
  confirm assertion (3) fails red. Restore both files to their 1.2/1.3 green state before continuing.

### Phase 2 — Migration (provenance lives in SQL, not only in C#)

- [x] 2.1 RED: create `BacktestMigrationProvenanceTests.cs` — a test referencing
  `AddSourcePlatformToBacktestRun.BackfillSql` (an `internal const string`, reachable via
  `InternalsVisibleTo` already declared on `AppTradingAlgoritmico.Infrastructure`). Assert the string
  contains `WHERE SourcePlatform IS NULL`, the asserter's name `Sebastian Benitez`, the literal
  `2026-09-21`, and the phrase `USER-SUPPLIED`; assert it does NOT contain `DEFAULT` or
  `defaultValue`. This will not compile until 2.2 exists — that is the RED.
  _Satisfies: "The Pre-Existing Row Backfill Is A Point-In-Time, User-Supplied Historical Fact" —
  first scenario; design D2._
- [x] 2.2 GREEN: run `dotnet ef migrations add AddSourcePlatformToBacktestRun` (per
  `backend-data.md`'s command) to scaffold `Migrations/*_AddSourcePlatformToBacktestRun.cs`, then
  hand-edit it to match design D2 exactly: `AddColumn<int>(name: "SourcePlatform", table:
  "BacktestRuns", type: "int", nullable: true)` with NO `defaultValue`/`defaultValueSql:`; the
  `internal const string BackfillSql` verbatim from design D2 (asserter, date, "USER-SUPPLIED
  HISTORICAL FACT", the coincidence note); `Up` calls `b.Sql(BackfillSql)` after `AddColumn`; `Down`
  drops the column. Confirm 2.1 passes.
- [x] 2.3 GREEN: update `AppDbContextModelSnapshot.cs` and the migration's `.Designer.cs` — these are
  EF-generated by 2.2's scaffold command; verify they reflect the nullable `int` column with no
  default, do not hand-edit beyond what the scaffold produced.
- [x] 2.4 RED: `..._RowCreatedAfterMigration_DoesNotInheritTheBackfill` (in `BacktestSchemaTests.cs` or
  `BacktestMigrationProvenanceTests.cs`) — a `BacktestRun` created against the post-migration schema
  with no declared platform reads `SourcePlatform == null`, not `MT4`.
  _Satisfies: "The Pre-Existing Row Backfill..." — second scenario._
- [x] 2.5 GREEN: no additional production code expected if 1.2/1.3/2.2 are correct; confirm 2.4
  passes.

### Phase 3 — Import service threading (D4/D6/D7)

- [x] 3.1 RED: extend `IBacktestImportService`'s existing signature test (or add one) asserting
  `ImportTradeListAsync` declares `PlatformType? sourcePlatform` as a parameter positioned BEFORE
  `CancellationToken ct`, with no C# default value on the interface. Will not compile until 3.2 —
  that is the RED.
  _Satisfies: design D4 ("no default value on the interface... the silent-null hazard in a different
  coat")._
- [x] 3.2 GREEN: update `IBacktestImportService.ImportTradeListAsync` to
  `Task<BacktestImportResultDto> ImportTradeListAsync(Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file, PlatformType? sourcePlatform, CancellationToken ct);`
  and thread the new parameter (unimplemented body yet) — this will break every existing call site;
  fix call sites minimally in 3.3–3.6 as compilation requires, without changing existing behavior.
- [x] 3.3 RED: `BacktestImportServiceTests` — new run, `sourcePlatform: PlatformType.MT5` → the
  created `BacktestRun.SourcePlatform == PlatformType.MT5`.
  _Satisfies: "Import Accepts An Optional Declared Platform..." — Scenario "A declared platform is
  recorded on a new run"._
- [x] 3.4 RED: `..._NewRun_DeclaredMT4_StoresMT4` — symmetric case.
  _Satisfies: same requirement, MT4 side of the Interfaces/Contracts pin._
- [x] 3.5 RED: `..._NewRun_OmittedPlatform_StoresNullNotMT4` — `sourcePlatform: null` → the created
  run's `SourcePlatform == null`.
  _Satisfies: "Import Accepts An Optional Declared Platform..." — Scenario "An omitted platform
  records null, not a default"._
- [x] 3.6 GREEN: implement threading through `ImportTradeListAsync` → `ImportOneFileAsync` →
  `PersistOneFileAsync` → `CreateNewRunAsync`, setting `SourcePlatform = sourcePlatform` on the new
  `BacktestRun` in `CreateNewRunAsync`. Confirm 3.3–3.5 pass.
- [x] 3.7 RED: `..._Replace_SuppliedPlatformOverwritesThePreviouslyRecordedOne` — slot holds
  `SourcePlatform = MT4`; re-import different bytes with `sourcePlatform: PlatformType.MT5` →
  outcome `Replaced`, `SourcePlatform == MT5`.
  _Satisfies: "Replacing A Run's Bytes Overwrites Its Recorded Platform Unconditionally..." —
  Scenario "A replacement with a new declared platform overwrites the old one"._
- [x] 3.8 RED: `..._Replace_OmittedPlatformNullsThePreviouslyRecordedOne` — slot holds
  `SourcePlatform = MT4`; re-import different bytes with `sourcePlatform: null` → outcome
  `Replaced`, `SourcePlatform == null`.
  _Satisfies: same requirement — Scenario "A replacement omitting the platform nulls the previously
  recorded one"; design D6._
- [x] 3.9 GREEN: in `ReplaceAsync`, add `run.SourcePlatform = sourcePlatform;` unconditionally
  alongside the existing `ContentHash`/`SourceFileName`/`Symbol` assignments. Confirm 3.7–3.8 pass.
- [x] 3.10 RED: `..._Unchanged_IdenticalBytesWithSuppliedPlatform_TakesTheNoWritePathAndPlatformIsUntouched` —
  slot holds `SourcePlatform = null` from hash `H`; re-import identical bytes with
  `sourcePlatform: PlatformType.MT5` → outcome `Unchanged`, `SourcePlatform` remains `null`.
  _Satisfies: "The Unchanged Outcome Writes Nothing, Including The Platform Column" — its scenario;
  design D7._
- [x] 3.11 GREEN: confirm the `Unchanged` branch in `PersistOneFileAsync` gains ZERO statements — no
  production change expected if 3.6/3.9 only touched `CreateNewRunAsync`/`ReplaceAsync`. Re-run
  `BacktestImportRetrySafetyTests` in full and confirm it is byte-identical; if 3.10 or the retry
  suite fails, stop and investigate rather than adding a write to the `Unchanged` branch.

### Phase 4 — Controller: `[FromQuery]` parameter + `Enum.IsDefined` guard (D1)

- [x] 4.1 RED: `StrategyBacktestsControllerTests.ImportTradeList_SourcePlatformAbsent_Returns200AndForwardsNull` —
  via the existing direct-instantiation `CreateSut()` pattern with a spy/mock
  `IBacktestImportService`, assert a call with no `sourcePlatform` argument results in `null` being
  passed through to `ImportTradeListAsync`.
  _Satisfies: "Import Accepts An Optional Declared Platform..." — controller-level pin from design's
  Testing Strategy table ("absent → 200 + null forwarded")._
- [x] 4.2 RED: `..._SourcePlatformDeclaredMT4OrMT5_ForwardsVerbatim` — parameterized over `MT4`/`MT5`.
  _Satisfies: same table row ("MT4/MT5 → forwarded verbatim")._
- [x] 4.3 RED: `..._SourcePlatformIsUndeclaredNumeral_Returns400AndServiceIsNeverCalled` — pass
  `(PlatformType)7`; assert `BadRequest` is returned and the mock `IBacktestImportService` receives
  ZERO calls. This is the case design D1 identifies as the real hazard (`Enum.Parse` accepts any
  numeral with no model-state error) and the one that IS unit-testable via direct instantiation.
  _Satisfies: "Import Accepts An Optional Declared Platform..." — Scenario "An unparseable platform
  value is rejected before the file is opened" (re-scoped per design D1 to the numeral case, since
  the string-unparseable case is not testable here — see Flagged section); design D1._
- [x] 4.4 GREEN: add `[FromQuery] PlatformType? sourcePlatform` to `ImportTradeList`, and BEFORE
  `TryAcceptCsv`/`file.OpenReadStream()` add the guard from design D1:
  `if (sourcePlatform is not null && !Enum.IsDefined(sourcePlatform.Value)) return BadRequest(new { message = $"Unknown source platform '{(int)sourcePlatform.Value}'. Expected MT4 or MT5." });`
  then thread `sourcePlatform` into `importService.ImportTradeListAsync(...)`. Add XML doc noting the
  guard exists because `Enum.Parse`-based binding accepts undeclared numerals silently. Confirm
  4.1–4.3 pass.
- [x] 4.5 Falsification (mandatory — D1's guard is the case explicitly called out): temporarily
  comment out the `Enum.IsDefined` guard added in 4.4 and re-run 4.3 — confirm it goes red (the mock
  service now receives a call with `(PlatformType)7` instead of zero calls, or the response is not
  `BadRequest`). Restore the guard and confirm 4.3 is green again before moving on.
- [x] 4.6 RED (may fold into 4.3 if the same test already proves it): confirm via the mock's call
  count that the guard runs BEFORE any interaction with the uploaded file — pass a non-null
  `IFormFile` mock alongside `(PlatformType)7` and assert `file.OpenReadStream()`/`file.Length` is
  never touched.
  _Satisfies: design D1 ("the guard runs before OpenReadStream")._

### Phase 5 — Read DTOs and projections (both DTOs, verbatim, null stays null)

- [x] 5.1 RED: extend or create a read-service test (mirror
  `DemoBacktestComparabilityReadServiceTests`'s SQLite/InMemory-backed pattern) —
  `BacktestReadServiceTests.GetRunsAsync_RunHasSourcePlatformMT5_DtoCarriesItVerbatim` and
  `..._GetByStrategyAsync_RunHasSourcePlatformMT5_SummaryDtoCarriesItVerbatim`.
  _Satisfies: "Both Backtest Read DTOs Expose The Source Platform Verbatim" — first scenario._
- [x] 5.2 RED: `..._NullPlatform_StaysNullInBothDtos` — parallel pair for `SourcePlatform == null`,
  asserting neither DTO renders/maps it as `MT4`.
  _Satisfies: same requirement — "A null platform stays null in both DTOs" scenario._
- [x] 5.3 GREEN: add `PlatformType? SourcePlatform` as the last positional member of
  `BacktestRunDto` (`Application/DTOs/Backtests/BacktestRunDto.cs`) and of `BacktestRunSummaryDto`
  (`Application/DTOs/Backtests/StrategyBacktestsDto.cs`); update `BacktestReadService.GetRunsAsync`'s
  join projection (`:34`) and `GetByStrategyAsync`'s projection (`:83`) to pass `r.SourcePlatform`
  verbatim. Confirm 5.1–5.2 pass.

### Phase 6 — Static gates and full suite (C1)

- [x] 6.1 Run `dotnet build AppTradingAlgoritmico.slnx -warnaserror` (leading dash — `/warnaserror`
  is mangled by Git Bash into a path) — zero warnings.
- [x] 6.2 Run `dotnet format AppTradingAlgoritmico.slnx --verify-no-changes` — no formatting diffs.
- [x] 6.3 Run `dotnet test` for the full backend suite. Confirm the **704 pre-existing tests** still
  pass, plus every new C1 test from Phases 1–5, with 0 warnings. Confirm
  `BacktestImportRetrySafetyTests`, slice A (`Divergence/DemoBacktestComparability*`), and slice B
  (`Divergence/CostDecomposition*`) suites are byte-identical.
- [x] 6.4 If any pre-existing test fails or a warning appears, stop and investigate before patching —
  this PR's only behavioral additions are the new parameter/column; every existing call path with
  the parameter omitted must be unchanged.

---

## PR C2 — Angular

Depends on C1 merged/available on its branch (feature-branch-chain, base = `feature/backtest-run-platform-attribution`, C2 base = C1's branch per design D5).

### Phase 7 — Real `PlatformType` enum + re-export (D7)

- [x] 7.1 RED: create `app/core/models/platform-type.model.spec.ts` — asserts `PlatformType.MT4 === 0`
  and `PlatformType.MT5 === 1` as enum members (not a `0 | 1` union type — e.g. assert
  `Object.values(PlatformType)` includes the string keys `'MT4'`/`'MT5'`, which only a real enum
  produces). Will not compile until 7.2 exists — that is the RED.
  _Satisfies: design D7 ("the web app has no `PlatformType` enum; C2 creates one")._
- [x] 7.2 GREEN: create `app/core/models/platform-type.model.ts` —
  `export enum PlatformType { MT4 = 0, MT5 = 1 }`. Confirm 7.1 passes.
- [x] 7.3 RED: extend `trading-account.service.spec.ts` (or add one if none exists) — asserts the
  `PlatformType` re-exported from `trading-account.service.ts` is reference-identical to
  `platform-type.model.ts`'s `PlatformType` (e.g. `PlatformType.MT4` imported from each path
  `=== `).
  _Satisfies: design D7 ("turns the alias into a one-line re-export")._
- [x] 7.4 GREEN: replace `export type PlatformType = 0 | 1;` at `trading-account.service.ts:7` with
  `export { PlatformType } from '../models/platform-type.model';`. **Decision point (D7 fallback)**:
  build the project after this change. If compile fallout is limited to one or two call sites, fix
  them minimally and continue. If fallout is broader/unbounded, REVERT this file's re-export,
  instead declare `enum PlatformType { MT4 = 0, MT5 = 1 }` locally inside `backtest.service.ts`
  (scoped to this change), and defer `trading-account.service.ts`'s de-duplication to the R8
  follow-up — do not expand this PR's scope to fix it. Do NOT touch
  `account-form.component.ts:58`'s `0 as PlatformType` default in either branch of this decision
  (out of scope, R8/non-goal).

### Phase 8 — `backtest.service.ts`: param, `HttpParams`, DTO fields, labels

- [x] 8.1 RED: `backtest.service.spec.ts` — `importDeploy(id, file, PlatformType.MT4)` issues a
  request whose params contain `sourcePlatform=0` (the falsy-zero trap named in design D6: `MT4 ===
  0` must not be dropped by a truthy check).
  _Satisfies: "The TypeScript falsy-zero trap" constraint; precedent `getGroupRisk`'s `segment`
  handling at `backtest.service.ts:443-445`._
- [x] 8.2 RED: `..._ImportDeploy_MT5_AppendsSourcePlatformEquals1`.
  _Satisfies: same constraint, MT5 side._
- [x] 8.3 RED: `..._ImportDeploy_SourcePlatformUndefined_AppendsNoSourcePlatformParamAtAll` — omitted
  third argument produces a request with NO `sourcePlatform` key in params (not `sourcePlatform=`).
  Repeat 8.1–8.3 for `importEvaluation`.
  _Satisfies: design D6 service contract; the omission-means-null rule from the backend spec, now on
  the client._
- [x] 8.4 GREEN: add `sourcePlatform?: PlatformType` as a third parameter to `importDeploy` and
  `importEvaluation`; update `postFile` (or wrap the call) to append `sourcePlatform` to the request
  ONLY when `sourcePlatform !== undefined` — never `if (sourcePlatform)`. Confirm 8.1–8.3 pass.
- [x] 8.5 GREEN: add `sourcePlatform: PlatformType | null` to the `BacktestRunSummaryDto` and
  `BacktestRunDto` TS interfaces in `backtest.service.ts`; add
  `export const PLATFORM_LABELS: Record<PlatformType, string> = { [PlatformType.MT4]: 'SQX.BACKTESTS.SOURCE_PLATFORM_MT4', [PlatformType.MT5]: 'SQX.BACKTESTS.SOURCE_PLATFORM_MT5' };`
  mirroring `BACKTEST_KIND_LABELS`.

### Phase 9 — i18n (Dual-Entry Protocol, six keys)

- [x] 9.1 Add to `public/assets/i18n/en.json` under `SQX.BACKTESTS`: `SOURCE_PLATFORM_LABEL`
  ("Source platform"), `SOURCE_PLATFORM_HINT`, `SOURCE_PLATFORM_NOT_DECLARED` ("Not declared"),
  `SOURCE_PLATFORM_MT4` ("MT4"), `SOURCE_PLATFORM_MT5` ("MT5"),
  `SOURCE_PLATFORM_UNDECLARED_NOTE` — exact EN strings from design D6's table.
- [x] 9.2 Add the same six keys with the ES strings from design D6's table to
  `public/assets/i18n/es.json` in the SAME commit as 9.1 (Dual-Entry Protocol — never split across
  commits; `frontend-data.md`).

### Phase 10 — Import modal: one selector, optional, never-MT4-by-default (D6)

- [x] 10.1 RED: `import-strategy-backtests-modal.component.spec.ts` — the rendered modal has exactly
  ONE `<select>` for source platform (not one per slot), located in `.import-backtests-modal__body`
  after `.import-backtests-modal__intro` and before the `@for` slot-loop's first `<section>`; its
  first `<option value="">` renders the `SOURCE_PLATFORM_NOT_DECLARED` label and is selected by
  default.
  _Satisfies: design D6 ("Where"/"Default" rows)._
- [x] 10.2 RED: `..._SelectingMT4_UpdatesTheSourcePlatformSignalToPlatformTypeMT4` — selecting the MT4
  option sets the component's platform state to `PlatformType.MT4`; the initial state (before any
  selection) is `null`.
  _Satisfies: design D6 ("Default" row: `signal<PlatformType | null>(null)`)._
- [x] 10.3 RED: `..._Submit_WithDeployAndEvaluationFilledAndMT4Selected_ForwardsMT4ToDeployAndEvaluationOnlyNeverToWalkForward` —
  queue deploy + evaluation + walkForward files, select MT4, call `submit()`; assert
  `importDeploy`/`importEvaluation` receive `PlatformType.MT4` as the third argument and
  `importWalkForward` is called with its existing two-argument signature (untouched).
  _Satisfies: design D6 ("Scope" row: applies to deploy/evaluation only, not walkForward)._
- [x] 10.4 RED: `..._Submit_WithNoPlatformChosen_ForwardsUndefinedNotNullNotZero` — with the platform
  left at its default `null` state, `submit()` calls `importDeploy`/`importEvaluation` with
  `undefined` as the third argument (never `null`, never `0`) — this is the RED that proves the
  undeclared path sends nothing over the wire.
  _Satisfies: design D6 ("Required?" row — optional, omission is undefined on the wire)._
- [x] 10.5 RED: `..._WhenATradeListFileIsQueuedAndPlatformIsNull_TheUndeclaredNoteRendersAndSubmitStaysEnabled` —
  queue a deploy file with no platform chosen; assert a `role="status"` element bound to
  `SOURCE_PLATFORM_UNDECLARED_NOTE` renders, and the submit button remains NOT disabled.
  _Satisfies: design D6 ("Disclosure" row — "deliberately visible null"; submit not blocked)._
- [x] 10.6 KNOWN EXPECTED RED — update, do not treat as a regression to silently revert: the existing
  test `submit_OnlyDeployFilled_ImportsOnlyDeployAndLeavesTheOtherSlotsUntouched` at
  `import-strategy-backtests-modal.component.spec.ts:126` currently asserts
  `toHaveBeenCalledWith(STRATEGY_ID, expect.any(File))` (two arguments). Once 10.7 changes
  `importDeploy`'s call arity to three arguments this assertion WILL fail. Update it to
  `toHaveBeenCalledWith(STRATEGY_ID, expect.any(File), undefined)` — this red-then-updated
  assertion is the proof that the undeclared path's third argument is explicit `undefined`, per
  design.md's "Known breakage in C2" note. Do not restore the two-argument form.
  _Satisfies: design's explicit called-out breakage; ties to 10.4's contract._
- [x] 10.7 GREEN: implement the `<select>` in
  `import-strategy-backtests-modal.component.html` per 10.1's placement; add
  `readonly sourcePlatform = signal<PlatformType | null>(null)` and a change handler in
  `.component.ts`; update `submit()` to pass `this.sourcePlatform() ?? undefined` as the third
  argument to `importDeploy`/`importEvaluation` calls only; render the
  `SOURCE_PLATFORM_UNDECLARED_NOTE` paragraph with `role="status"` gated on "a deploy or evaluation
  file is queued AND `sourcePlatform()` is null", not blocking submit. Confirm 10.1–10.6 pass.

### Phase 11 — Backtests-list column (verbatim, visible not-declared state)

- [x] 11.1 RED: `backtests-list.component.spec.ts` — a run with `sourcePlatform: PlatformType.MT4`
  in the rendered table shows the `SOURCE_PLATFORM_MT4` label in the new column.
  _Satisfies: "The Backtests List Displays The Recorded Source Platform Verbatim..." — "A run
  recorded as MT4 renders as MT4"._
- [x] 11.2 RED: `..._MT5RecordedRun_RendersMT5Label` — symmetric case.
  _Satisfies: same requirement — "A run recorded as MT5 renders as MT5"._
- [x] 11.3 RED: `..._NullSourcePlatform_RendersTheNotDeclaredLabelNeverBlankNeverDashNeverMT4` — a run
  with `sourcePlatform: null` renders `SOURCE_PLATFORM_NOT_DECLARED`; assert the cell text is
  neither empty, nor `'—'`, nor the MT4 label.
  _Satisfies: same requirement — "A run with no recorded platform renders as visibly not declared"._
- [x] 11.4 GREEN: add a `COL_SOURCE_PLATFORM` header (reuse the `SOURCE_PLATFORM_LABEL` key) and a
  cell to the runs `<table>` in `backtests-list.component.html`, rendering
  `run.sourcePlatform !== null ? (platformLabels[run.sourcePlatform] | translate) : (notDeclaredKey | translate)`
  — verbatim only, no fallback, no derivation from any other field. Expose `platformLabels =
  PLATFORM_LABELS` and `notDeclaredKey = 'SQX.BACKTESTS.SOURCE_PLATFORM_NOT_DECLARED'` on
  `BacktestsListComponent`. **Bind against `BacktestRunDto.sourcePlatform`** — `runs` on this
  component is typed `BacktestRunDto[]` from `getRuns()` (`backtests-list.component.ts:13,45`), NOT
  `BacktestRunSummaryDto` as design.md D6 and spec.md's requirement prose state; see Flagged section
  below. Confirm 11.1–11.3 pass.

### Phase 12 — Static gates and full suite (C2)

- [x] 12.1 Run `pnpm --dir app.trading.algoritmico.web exec prettier --check .` (prettier MUST run
  from `app.trading.algoritmico.web`, never the repo root) — no formatting diffs; if diffs appear,
  run `pnpm --dir app.trading.algoritmico.web exec prettier --write` on the touched files only.
- [x] 12.2 Run `pnpm --dir app.trading.algoritmico.web exec tsc --build` (NOT a bare `tsc --noEmit`
  from the repo root — the root `tsconfig.json` has `"files": []` and would exit 0 having compiled
  nothing) — zero type errors.
- [x] 12.3 Run `pnpm --dir app.trading.algoritmico.web test` for the full frontend suite (per
  `openspec/config.yaml`'s `testing.frontend.command`). Confirm every pre-existing spec passes
  (including 10.6's UPDATED assertion), plus every new C2 test from Phases 7–11.
- [x] 12.4 If any pre-existing spec fails unexpectedly (i.e., not 10.6, which is the one deliberate,
  already-updated exception) or a type/format error appears, stop and investigate before patching.

---

## Flagged: spec/design items resolved or corrected during task planning

1. **RESOLVED (re-scoped, not dropped)**: spec.md's "An unparseable platform value is rejected
   before the file is opened" scenario posts `?sourcePlatform=bogus` (a non-numeric string) and
   expects 400. Design D1 verifies this is NOT unit-testable here:
   `StrategyBacktestsControllerTests` instantiates the controller directly, so ASP.NET model binding
   — which is what would reject an unparseable string — never runs. Task 4.3 tests the case that IS
   drivable this way instead: a syntactically valid but undeclared numeral, `(PlatformType)7`,
   caught by the explicit `Enum.IsDefined` guard. This mirrors the same limitation already documented
   on `GetComparability`. No test exists for the literal `?sourcePlatform=bogus` string case in this
   suite; if an integration-test harness is added later (`config.yaml: integration_tool_installed:
   false` today), that case should be added then.
2. **CORRECTED, load-bearing for task 11.4**: design.md D6 and spec.md's list-display requirement
   both state the backtests-list column is "sourced from `BacktestRunSummaryDto.sourcePlatform`".
   Verified against the actual code: `backtests-list.component.ts` populates its `runs` signal from
   `BacktestService.getRuns()` (`backtest.service.ts:412-415`), which calls
   `GET /api/backtests/runs` (`BacktestsController.cs:30` → `IBacktestReadService.GetRunsAsync` →
   `BacktestRunDto`, not `BacktestRunSummaryDto`). `BacktestRunSummaryDto` (TS) is used ONLY inside
   `StrategyBacktestsDto`/`getStrategyBacktests()`, which no current component renders — verified via
   a repo-wide grep with zero component matches. Both C# DTOs and both TS interfaces gain the field
   per the spec's own DTO requirement, so this does not change scope, but task 11.4 explicitly binds
   to `BacktestRunDto.sourcePlatform` rather than `BacktestRunSummaryDto`, and `sdd-apply` must not
   "correct" this back to match the design's prose.
3. **Left as a manual/build-time check, not a scripted task**: design D7's fallback ("if the
   re-export produces compile fallout beyond one or two call sites, declare the enum locally in
   `backtest.service.ts` instead") requires running the actual build to know which branch applies.
   Task 7.4 states the decision point explicitly; `sdd-apply` must record which branch it took.
4. **Unverified, blocking migration application, not this task's output**: proposal Assumption A2
   ("no MT5 run has been imported yet") requires database access this phase does not have. The
   migration (task 2.2) must not be applied to any real database until A2 is re-confirmed —
   `sdd-apply` should surface this before running `dotnet ef database update`.
5. **CORRECTED during C1 apply (task 2.1/2.2)**: design.md D2's illustrative `BackfillSql` literal
   contains the English phrase "PROVENANCE, NOT A DEFAULT" — which itself contains the substring
   "DEFAULT". Task 2.1's own RED assertion requires the same string to NOT contain "DEFAULT" or
   "defaultValue" (a guard against a reintroduced SQL/EF default clause). Copying design's text
   verbatim would make its own pinning test permanently red. Resolved by rephrasing only the
   colliding word ("PROVENANCE, NOT SYSTEM-CHOSEN" / "no database-level fallback") while keeping
   every substantive element intact: the asserter's name, the assertion date, the phrase
   "USER-SUPPLIED HISTORICAL FACT", the `WHERE SourcePlatform IS NULL` clause, and the
   CLR-coincidence note. The migration file documents this deviation inline.
