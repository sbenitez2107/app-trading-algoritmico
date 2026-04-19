# Tasks: Add Strategies to Demo Accounts

## Design Decisions (Closed — do NOT re-ask in apply)

| Decision | Answer |
|----------|--------|
| Column picker position | RIGHT side sidebar |
| Numeric formatting in grid | Plain numbers for MVP — no custom pipe. Formatter pipe is a follow-up, out of scope. |
| TradingAccount delete UX warning | Deferred — SetNull handles it at DB level; UI warning is a follow-up. |

---

## Phase 0: Pre-flight

- [ ] 0.1 Read conventions index: [`.claude/conventions/_index.md`](.claude/conventions/_index.md)
- [ ] 0.2 Read backend conventions: `backend-core.md`, `backend-data.md`, `backend-testing.md`
- [ ] 0.3 Read frontend conventions: `frontend-core.md`, `frontend-design.md`
- [ ] 0.4 Read universal testing conventions: `universal-testing.md`
- [ ] 0.5 Confirm Strict TDD is active (per `sdd-init/app-trading-algoritmico` engram entry)
- [ ] 0.6 Record the three closed design decisions above in the apply-progress artifact

---

## Phase 1: Domain + EF Model

> Pure config — no preceding test task. Behavior verified by Phase 1 SQLite test below.

- [ ] 1.1 Modify [`Domain/Entities/Strategy.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Domain/Entities/Strategy.cs)
  - Change `public Guid BatchStageId` → `public Guid? BatchStageId`
  - Change `public BatchStage BatchStage { get; set; } = null!;` → `public BatchStage? BatchStage { get; set; }`
  - Add `public Guid? TradingAccountId { get; set; }`
  - Add `public TradingAccount? TradingAccount { get; set; }`

- [ ] 1.2 Modify [`Domain/Entities/TradingAccount.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Domain/Entities/TradingAccount.cs)
  - Add `public ICollection<Strategy> Strategies { get; set; } = [];`

- [ ] 1.3 Modify [`Infrastructure/Persistence/Configurations/StrategyConfiguration.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Persistence/Configurations/StrategyConfiguration.cs)
  - Replace existing `HasOne(x => x.BatchStage)` block with `IsRequired(false)` + `OnDelete(DeleteBehavior.SetNull)`
  - Add new `HasOne(x => x.TradingAccount)` block with `IsRequired(false)` + `OnDelete(DeleteBehavior.SetNull)`

- [ ] 1.4 **[TEST — spec: strategy-model M1, M2, M3, M4]** Write SQLite in-memory test: `StrategyConfigurationTests`
  - File: [`tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyConfigurationTests.cs`](app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyConfigurationTests.cs)
  - Add NuGet to test project: `Microsoft.EntityFrameworkCore.Sqlite` (same version as EF Core 10 in Infrastructure)
  - Test cases:
    - `Strategy_WithBothFks_PersistsSuccessfully` — insert strategy with BatchStageId + TradingAccountId set; reload; assert both FKs populated (covers M2)
    - `DeleteBatchStage_DualLinkedStrategy_PreservesRowWithNullBatchStageId` — insert dual-linked strategy, delete BatchStage, assert strategy survives with BatchStageId=null and TradingAccountId intact (covers M3)
    - `Strategy_WithNullBatchStageId_PipelineOnly_IsValid` — insert strategy with only TradingAccountId (BatchStageId=null); reload; assert row exists (covers M1)
  - Use SQLite in-memory with `optionsBuilder.UseSqlite("DataSource=:memory:")` and `db.Database.EnsureCreated()`

---

## Phase 2: Migration

> No test task — covered by Phase 1 SQLite tests + regression suite.

- [ ] 2.1 Generate EF migration:
  ```
  dotnet ef migrations add AddStrategyTradingAccountFk \
    --project src/AppTradingAlgoritmico.Infrastructure \
    --startup-project src/AppTradingAlgoritmico.WebAPI
  ```
  Expected migration file: [`Infrastructure/Migrations/{timestamp}_AddStrategyTradingAccountFk.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Migrations/)

- [ ] 2.2 **MANDATORY review** — open generated migration file and confirm:
  - `AlterColumn` changes `BatchStageId` from NOT NULL to NULL (no data loss)
  - `AddColumn` adds `TradingAccountId uniqueidentifier NULL`
  - Old Cascade FK dropped; two new SetNull FKs added
  - No `RenameColumn`, `DropColumn`, or `DropTable` for unrelated columns
  - Run `dotnet ef migrations script` and review SQL output

- [ ] 2.3 Apply migration against local SQL Server DB (user authorization required before running)

- [ ] 2.4 Run all existing backend unit tests — confirm no regressions:
  ```
  dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests
  ```

---

## Phase 3: Service Layer — StrategyService

- [ ] 3.1 **[TEST — spec: account-strategies R1, R2]** Write `StrategyService_GetByAccountAsyncTests`
  - File: [`tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyServiceGetByAccountTests.cs`](app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyServiceGetByAccountTests.cs)
  - Use EF InMemory provider (already in Infrastructure; no new package needed)
  - Test cases:
    - `GetByAccountAsync_AccountWithStrategies_ReturnsPaginatedResult` — seed account + 3 strategies; assert Items.Count=3, TotalCount=3 (spec R1 scenario 1)
    - `GetByAccountAsync_AccountExistsNoStrategies_ReturnsEmpty` — seed account with no strategies; assert Items empty, TotalCount=0 (spec R1 scenario 2)
    - `GetByAccountAsync_AccountNotFound_ThrowsKeyNotFoundException` — no account seeded; assert throws `KeyNotFoundException` (spec R1 scenario 3)

- [ ] 3.2 **[TEST — spec: account-strategies R2, strategy-model M4]** Write `StrategyService_AddToAccountAsyncTests`
  - File: [`tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyServiceAddToAccountTests.cs`](app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategyServiceAddToAccountTests.cs)
  - Moq `ISqxParserService` + `IHtmlReportParserService`; EF InMemory for context
  - Test cases:
    - `AddToAccountAsync_HappyPath_PersistsStrategyWithTradingAccountId` — both mocks return valid data; assert persisted entity has TradingAccountId=accountId, BatchStageId=null, KPIs from report (spec R2 scenario 1)
    - `AddToAccountAsync_AccountNotFound_ThrowsKeyNotFoundException` — no account in DB; assert throws `KeyNotFoundException` (spec R2 scenario 5)
    - `AddToAccountAsync_HtmlParserReturnsNull_ThrowsArgumentException` — mock returns null; assert throws `ArgumentException` (spec R2 scenario 4)
    - `AddToAccountAsync_BothFksNull_ThrowsException` — attempt create with no accountId and no batchStageId; assert exception before SaveChanges (spec M4)

- [ ] 3.3 Add methods to [`Application/Interfaces/IStrategyService.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Application/Interfaces/IStrategyService.cs):
  ```csharp
  Task<PagedResult<StrategyDto>> GetByAccountAsync(Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default);
  Task<StrategyDto> AddToAccountAsync(Guid accountId, string name, Stream sqxStream, Stream htmlStream, CancellationToken ct = default);
  ```

- [ ] 3.4 Implement `GetByAccountAsync` + `AddToAccountAsync` in [`Infrastructure/Services/StrategyService.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Services/StrategyService.cs)
  - Follow skeleton in `design.md` exactly
  - Inject `IHtmlReportParserService` (already wired in BatchService — check DI)

- [ ] 3.5 Run Phase 3 tests — confirm all green

---

## Phase 4: BatchService — Dual-Linked Strategy Guard

- [ ] 4.1 **[TEST — spec: strategy-model M3]** Write `BatchService_DeleteAsync_DualLinkedStrategyTests`
  - File: [`tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/BatchServiceDeleteTests.cs`](app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/BatchServiceDeleteTests.cs)
  - Seed: one batch with one stage; stage has two strategies: pipeline-only (TradingAccountId=null) + dual-linked (TradingAccountId set)
  - Test case: `DeleteAsync_StageDeletion_PipelineOnlyRemovedDualLinkedPreserved` — after DeleteAsync, assert pipeline-only strategy is gone, dual-linked strategy survives with BatchStageId=null and TradingAccountId preserved

- [ ] 4.2 **[TEST — spec: strategy-model M3]** Write `BatchService_RollbackStage_DualLinkedStrategyTests`
  - File: same file as 4.1 or sibling `BatchServiceRollbackTests.cs`
  - Same seed shape but call `RollbackStageAsync`
  - Test case: `RollbackStageAsync_StageDeletion_PipelineOnlyRemovedDualLinkedPreserved` — same assertions as 4.1

- [ ] 4.3 Modify [`Infrastructure/Services/BatchService.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Services/BatchService.cs)
  - In `DeleteAsync`: change `RemoveRange(stage.Strategies)` → `RemoveRange(stage.Strategies.Where(s => s.TradingAccountId == null))`
  - In `RollbackStageAsync`: same filter change

- [ ] 4.4 Run Phase 4 tests + all existing BatchService tests — confirm green

---

## Phase 5: REST Controller

- [ ] 5.1 **[TEST — spec: account-strategies R1, R2]** Write `TradingAccountStrategiesControllerTests`
  - File: [`tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/TradingAccountStrategiesControllerTests.cs`](app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/TradingAccountStrategiesControllerTests.cs)
  - Mock `IStrategyService`; instantiate controller directly (no TestServer needed)
  - Test cases:
    - `GetStrategies_ExistingAccount_Returns200WithPagedResult` (spec R1 scenario 1)
    - `GetStrategies_AccountNotFound_Returns404` — mock throws `KeyNotFoundException` (spec R1 scenario 3)
    - `PostStrategy_ValidFiles_Returns201WithStrategyDto` (spec R2 scenario 1)
    - `PostStrategy_MissingSqxFile_Returns400` (spec R2 missing sqx scenario)
    - `PostStrategy_MissingHtmlFile_Returns400` (spec R2 missing html scenario)
    - `PostStrategy_UnparseableHtml_Returns400` — mock throws `ArgumentException` (spec R2 scenario 4)
    - `PostStrategy_AccountNotFound_Returns404` — mock throws `KeyNotFoundException` (spec R2 scenario 5)

- [ ] 5.2 Implement [`WebAPI/Controllers/TradingAccountStrategiesController.cs`](app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI/Controllers/TradingAccountStrategiesController.cs)
  - Route: `api/trading-accounts/{accountId}/strategies`
  - GET: `GetStrategies([FromRoute] Guid accountId, [FromQuery] int page=1, [FromQuery] int pageSize=20)`
  - POST: `CreateStrategy([FromRoute] Guid accountId, [FromForm] string name, [FromForm] IFormFile? sqxFile, [FromForm] IFormFile? htmlFile)`
  - Map `KeyNotFoundException → 404`, `ArgumentException → 400`
  - Follow `BatchesController.Create` pattern for stream handling

- [ ] 5.3 Run Phase 5 tests — confirm all green

---

## Phase 6: Frontend Service

- [ ] 6.1 **[TEST — spec: account-strategies R1, R2]** Write `StrategyService_AccountMethods` spec
  - File: [`app.trading.algoritmico.web/src/app/core/services/strategy.service.spec.ts`](app.trading.algoritmico.web/src/app/core/services/strategy.service.spec.ts)
  - Use `HttpTestingController` from `@angular/common/http/testing`
  - Test cases (append to existing spec file or create new — check if spec exists):
    - `getByAccount_BuildsCorrectUrlWithPaginationParams` — call `getByAccount('acc-1', 1, 20)`, assert request URL includes `/api/trading-accounts/acc-1/strategies?page=1&pageSize=20`
    - `addToAccount_PostsFormDataWithNameAndBothFiles` — call `addToAccount('acc-1', 'MyStrat', sqxFile, htmlFile)`, assert POST to correct URL, body is FormData with `name`, `sqxFile`, `htmlFile`

- [ ] 6.2 Extend [`app.trading.algoritmico.web/src/app/core/services/strategy.service.ts`](app.trading.algoritmico.web/src/app/core/services/strategy.service.ts)
  - Add `getByAccount(accountId, page, pageSize)` method
  - Add `addToAccount(accountId, name, sqx, html)` method
  - Follow exact signatures from `design.md`

- [ ] 6.3 Run frontend tests: `pnpm test` — confirm Phase 6 tests green

---

## Phase 7: AddStrategyModalComponent

- [ ] 7.1 **[TEST — spec: account-strategies R6]** Write `AddStrategyModalComponent` spec
  - File: [`app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.spec.ts`](app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.spec.ts)
  - Use Vitest + Angular TestBed; mock `StrategyService`
  - Test cases:
    - `canSubmit_NameEmptyBothFilesSelected_IsFalse` (spec R6 scenario 1 — missing name edge)
    - `canSubmit_NameSetOnlySqxSelected_IsFalse` (spec R6 scenario 1)
    - `canSubmit_NameSetOnlyHtmlSelected_IsFalse` (spec R6 scenario 1)
    - `canSubmit_NameSetBothFilesSelected_IsTrue` (spec R6 scenario 2)
    - `submit_CallsAddToAccountWithCorrectFormData` — assert `strategyService.addToAccount` called with accountId, name, sqx, html
    - `submit_OnSuccess_EmitsStrategyCreated` — mock returns StrategyDto; assert `strategyCreated` emitted
    - `submit_On400Error_SetsErrorSignal` — mock throws HttpError 400; assert `error()` signal non-null

- [ ] 7.2 Create `AddStrategyModalComponent`:
  - Files: [`app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.ts`](app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.ts)
  - [`add-strategy-modal.component.html`](app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.html)
  - [`add-strategy-modal.component.scss`](app.trading.algoritmico.web/src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.scss)
  - Standalone, OnPush, Signals, ngx-translate for all labels
  - Inputs: `@Input({ required: true }) accountId`; Outputs: `strategyCreated`, `cancelled`
  - Signals: `name`, `sqxFile`, `htmlFile`, `isSubmitting`, `error`, `canSubmit` (computed)

- [ ] 7.3 Run Phase 7 tests — confirm all green

---

## Phase 8: AccountDetailComponent

- [ ] 8.1 **[TEST — spec: account-strategies R3, R5]** Write `AccountDetailComponent` spec
  - File: [`app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.spec.ts`](app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.spec.ts)
  - Mock `StrategyService`; provide `ActivatedRoute` stub with `paramMap` emitting `accountId=acc-1`
  - Test cases:
    - `ngOnInit_ReadsAccountIdFromRoute_CallsGetByAccount` — assert `getByAccount('acc-1')` called on init (spec R3 scenario 1)
    - `ngOnInit_ServiceReturns2Strategies_StrategiesSignalHas2Items`
    - `columnDefs_DefaultVisible_ContainsOnlyThe7DefaultColumns` — assert `columnDefs()` has 7 with `hide: false`, rest with `hide: true` (spec R5)
    - `toggleColumn_HiddenColumn_BecomesVisible` — call `toggleColumn('yearlyAvgProfit')`, assert `visibleColumns()` now includes it (spec R5 scenario 3)
    - `openModal_ButtonClick_ShowsAddStrategyModal` — assert modal shown signal is true
    - `onStrategyCreated_RefreshesStrategies` — emit from modal; assert `getByAccount` called again
    - `ngOnInit_ServiceReturns404_SetsErrorSignal` — mock throws; assert `error()` non-null (spec R3 scenario 2)

- [ ] 8.2 Create `AccountDetailComponent`:
  - Files: [`app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.ts`](app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.ts)
  - [`account-detail.component.html`](app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.html)
  - [`account-detail.component.scss`](app.trading.algoritmico.web/src/app/features/darwinex/account-detail/account-detail.component.scss)
  - Standalone, OnPush, Signals + `computed()`
  - `ALL_KPI_COLS` static array — one `ColDef` per field from `StrategyDto`; `agNumberColumnFilter` for numerics, `agTextColumnFilter` for strings
  - `visibleColumns` signal initialized to `Set(['name','totalProfit','winningPercentage','profitFactor','drawdown','numberOfTrades','sharpeRatio'])`
  - `columnDefs` computed from `visibleColumns`
  - Right-side column picker sidebar: scrollable checkbox list iterating `ALL_KPI_COLS`; `(change)="toggleColumn(col.field)"` 
  - `suppressMovableColumns` NOT set

- [ ] 8.3 Run Phase 8 tests — confirm all green

---

## Phase 9: Routing + Row Click

- [ ] 9.1 **[TEST — spec: account-strategies R4]** Write `AccountsListComponent_NavigateToDetail` spec
  - File: [`app.trading.algoritmico.web/src/app/features/darwinex/accounts-list/accounts-list.component.spec.ts`](app.trading.algoritmico.web/src/app/features/darwinex/accounts-list/accounts-list.component.spec.ts)
  - Spy on `Router.navigate`
  - Test cases:
    - `navigateToDetail_RowClick_NavigatesToDemoRoute` — call `navigateToDetail(acc, mouseEvent)` with target NOT a button; assert `router.navigate` called with `['/darwinex','demo', acc.id]`
    - `navigateToDetail_ButtonClick_DoesNotNavigate` — set `event.target` to a `<button>` element; assert `router.navigate` NOT called
    - `navigateToDetail_LiveAccountType_DoesNotNavigate` — set `accountType = 1`; assert no navigation

- [ ] 9.2 Modify [`darwinex.routes.ts`](app.trading.algoritmico.web/src/app/features/darwinex/darwinex.routes.ts)
  - Add child route before `live`:
    ```typescript
    {
      path: 'demo/:accountId',
      loadComponent: () =>
        import('./account-detail/account-detail.component').then(m => m.AccountDetailComponent)
    }
    ```
  - Ensure this route is listed BEFORE the `demo` (list) route to avoid prefix conflict

- [ ] 9.3 Modify [`accounts-list.component.ts`](app.trading.algoritmico.web/src/app/features/darwinex/accounts-list/accounts-list.component.ts)
  - Inject `Router`
  - Add `navigateToDetail(acc: TradingAccountDto, ev: Event): void` method with button guard + accountType=0 guard

- [ ] 9.4 Modify [`accounts-list.component.html`](app.trading.algoritmico.web/src/app/features/darwinex/accounts-list/accounts-list.component.html)
  - Add `(click)="navigateToDetail(acc, $event)"` on each `<tr>` for data rows
  - Ensure action buttons have `(click)="$event.stopPropagation(); ..."` to prevent row click from firing

- [ ] 9.5 Run Phase 9 tests — confirm all green

---

## Phase 10: Integration Smoke Test

> Manual — no automated test task.

- [ ] 10.1 Start backend: `dotnet run --project src/AppTradingAlgoritmico.WebAPI`
- [ ] 10.2 Start frontend: `pnpm start`
- [ ] 10.3 Manual flow:
  - Login → `/darwinex/demo` → confirm account list renders
  - Click a demo account row → confirm navigation to `/darwinex/demo/:accountId`
  - `AccountDetailComponent` loads; grid renders with default 7 visible columns
  - Open column picker sidebar → all ~50 KPI columns listed; toggle one → grid updates
  - Click "Add Strategy" → modal opens; verify submit disabled without both files
  - Upload a real `.sqx` + `.html` pair → `201` returned; grid refreshes with new row
  - Click action button (edit/toggle) → confirm row click navigation does NOT fire
- [ ] 10.4 Document any deviation found as a new task in apply-progress

---

## Phase 11: Cleanup

> No commit — user runs `/commit` skill.

- [ ] 11.1 Update `CHANGELOG.md` `[Unreleased]` section — add entry under `### Added`:
  - "Add strategies directly to demo trading accounts bypassing the SQX pipeline"
  - "AccountDetailComponent with ag-grid showing all ~50 KPI columns and right-side column picker"
  - "TradingAccountStrategiesController: GET + POST nested-resource endpoints"
- [ ] 11.2 Verify no stale usages of removed code (e.g., old `BatchStageId` non-nullable references, any code that assumed `BatchStageId` was always set)
- [ ] 11.3 Verify `StrategyConfiguration` no longer has `Cascade` anywhere for the BatchStage FK
- [ ] 11.4 Run full backend test suite one final time: `dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests`
- [ ] 11.5 Run full frontend test suite one final time: `pnpm --prefix app.trading.algoritmico.web test`
