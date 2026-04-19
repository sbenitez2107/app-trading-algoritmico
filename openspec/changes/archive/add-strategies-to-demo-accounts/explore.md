# Exploration: add-strategies-to-demo-accounts

**Date**: 2026-04-18
**Change**: add-strategies-to-demo-accounts
**Approach decided**: Option A — reuse Strategy entity with nullable BatchStageId + nullable TradingAccountId

---

## Current State

### Backend — Strategy Entity

`Strategy.cs` (Domain/Entities) currently has:
- `public Guid BatchStageId { get; set; }` — **non-nullable**, required FK
- `public BatchStage BatchStage { get; set; } = null!;` — required nav property
- 50+ KPI columns (all nullable decimals/ints) from SQX HTML parser
- `ICollection<StrategyMonthlyPerformance> MonthlyPerformance` — child collection

`StrategyConfiguration.cs` configures the FK with:
```csharp
builder.HasOne(x => x.BatchStage)
    .WithMany(bs => bs.Strategies)
    .HasForeignKey(x => x.BatchStageId)
    .OnDelete(DeleteBehavior.Cascade);
```

There is NO `TradingAccountId` column today. No relationship exists between `Strategy` and `TradingAccount`.

### Backend — TradingAccount Entity

`TradingAccount.cs` has: `Name`, `Broker`, `AccountType` (Demo=0/Live=1 enum), `Platform` (MT4/MT5), `AccountNumber`, `Login`, `PasswordEncrypted`, `Server`, `IsEnabled`. No navigation collection to strategies.

### Backend — StrategyService

`IStrategyService` only has two methods:
- `GetByStageAsync(batchId, stageId, page, pageSize)` — filters by `BatchStageId` (always assumes non-null)
- `UpdateKpisAsync(strategyId, dto)` — works by ID, no FK assumption

`StrategiesController` routes:
- `GET api/batches/{batchId}/stages/{stageId}/strategies` — pipeline query
- `PATCH api/strategies/{id}` — KPI update (no FK dependency)

### Backend — BatchService

Creates `Strategy` entities only via `HydrateStrategy()` which sets `Name`, `Pseudocode`, `CreatedAt`, and KPI fields. The `BatchStageId` is set implicitly via EF navigation (`builderStage.Strategies.Add(...)`).

`HtmlReportParserService` is a standalone service (`IHtmlReportParserService`) that takes a `Stream` and returns `ParsedReportDto`. **Fully reusable** — no batch/stage coupling.

### Frontend — Darwinex Feature

`/darwinex` routes to `/darwinex/demo` and `/darwinex/live` (both using `AccountsListComponent`).

`AccountsListComponent`:
- Loads accounts via `TradingAccountService.getAll(broker, accountType)`
- Renders a table: Name | Platform | AccountNumber | Login | Server | Estado | Acciones
- Actions: Edit (modal), Toggle enabled/disabled, Delete
- Uses `Default` change detection (NOT OnPush) — inconsistent with project standard
- **No row click handler** — clicking a row does nothing today
- **No router navigation** from the list

`darwinex.routes.ts` only has `demo` and `live` paths. No child route for account detail exists.

`app.routes.ts` loads darwinex as a lazy-loaded child under the main layout (auth-guarded).

### Frontend — Stage Detail (SQX workflow)

`StageDetailComponent` has a strategy table with:
- Inline editable KPI inputs (onblur update via `PATCH api/strategies/{id}`)
- Top-10 ranking computed signal
- Expand/collapse pseudocode row

This component is **tightly coupled** to the SQX pipeline: it reads `batchId`, `stageType`, `assetId`, `timeframe` from route params and calls `BatchService.getById()` to resolve the `stageId`. Not directly reusable for the account detail view — but the strategy grid pattern (table + KPI columns) is worth extracting as a shared component.

### Frontend — Upload Pattern

`BatchCreateModalComponent` shows the existing pattern for file upload:
- `FormData` / multipart via `batchService.create()` which posts to `api/batches`
- File selected via `<input type="file">`, passed as `IFormFile` on the backend
- No dedicated "upload strategy HTML" endpoint exists yet for account context

### Existing Migrations

Latest migration: `20260418045328_AddFullStrategyKpisAndMonthlyPerformance` — adds all 50+ KPI columns. `BatchStageId` has been present since `20260411212806_AddStrategyWorkflowEntities` as a non-nullable `uniqueidentifier NOT NULL`.

---

## Affected Areas

### Backend
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.Domain/Entities/Strategy.cs` — make `BatchStageId` nullable, add `TradingAccountId?`, add nav property
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` — change FK to optional, add new FK config, update `OnDelete` to `SetNull`
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.Domain/Entities/TradingAccount.cs` — optionally add nav collection `ICollection<Strategy> Strategies`
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.Application/Interfaces/IStrategyService.cs` — add `GetByAccountAsync` and `AddToAccountAsync`
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.Infrastructure/Services/StrategyService.cs` — implement new methods
- `app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI/Controllers/StrategiesController.cs` — add new endpoints
- New EF migration needed

### Frontend
- `app.trading.algoritmico.web/src/app/features/darwinex/darwinex.routes.ts` — add `demo/:accountId` child route
- New component: `features/darwinex/account-detail/account-detail.component.*` — shows strategy grid + add button
- New component: `features/darwinex/add-strategy-modal/add-strategy-modal.component.*` — .sqx + .html upload pair
- `app.trading.algoritmico.web/src/app/features/darwinex/accounts-list/accounts-list.component.html` — add row click handler for navigation
- `app.trading.algoritmico.web/src/app/core/services/strategy.service.ts` — add `getByAccount()` and `addToAccount()` methods

---

## Approaches

### Option A (DECIDED by user) — Extend Strategy entity with optional FKs

Make `BatchStageId` nullable and add `TradingAccountId?` to `Strategy`. A strategy can live in:
- Pipeline only (`BatchStageId` set, `TradingAccountId` null)
- Account only (`TradingAccountId` set, `BatchStageId` null)
- Both (`BatchStageId` + `TradingAccountId` both set — future use case)

**Pros:**
- Zero data migration for existing strategies — they keep their `BatchStageId` as-is
- Reuses all 50+ KPI columns, `MonthlyPerformance`, `HtmlReportParserService`
- Single source of truth for strategy data
- No duplicate entity, no sync problem

**Cons:**
- `StrategyService.GetByStageAsync` query (`WHERE BatchStageId == stageId AND BatchStage.BatchId == batchId`) is fine with nullable — EF handles it correctly
- `StrategyConfiguration.OnDelete` must change from `Cascade` to `SetNull` for `BatchStageId`
- Need to be careful: existing rows have `BatchStageId NOT NULL` in DB — migration must ALTER the column to allow NULLs without breaking existing data

**Effort:** Medium (one migration, new service methods, new frontend route + 2 components)

### Option B — Separate AccountStrategy entity

Create a new `AccountStrategy` entity (or a join table) that holds the relationship between `TradingAccount` and `Strategy`.

**Pros:**
- Cleaner separation — pipeline strategies vs account strategies
- No risk of accidentally querying account strategies from batch endpoints

**Cons:**
- Duplicates the 50+ KPI columns or requires a join in every query
- More tables, more code, more migrations
- User explicitly rejected this — they want to skip SQX pipeline for now and load directly

**Effort:** High

---

## Constraint Analysis

### Does any code ASSUME Strategy.BatchStageId is non-null?

1. **`StrategyService.GetByStageAsync`**: `WHERE x.BatchStageId == stageId && x.BatchStage.BatchId == batchId` — both conditions will naturally exclude null-FK rows, safe.
2. **`BatchService.HydrateStrategy`**: Never touches `BatchStageId` directly — it's set by EF via nav collection add. Safe.
3. **`BatchService.RollbackStageAsync`**: `db.Strategies.RemoveRange(stage.Strategies)` — deletes by nav, not by FK value. Safe with `SetNull` change (cascade still applies for deleting the stage itself, but since `BatchStageId` becomes nullable with `SetNull`, we'd want to keep cascade-delete for the pipeline case or handle it differently — see risks).
4. **`StrategyConfiguration`**: The FK `OnDelete(DeleteBehavior.Cascade)` — this MUST change to `SetNull` for account-only strategies to not be deleted when a BatchStage is deleted. But for pipeline strategies, we still want cascade... The safest approach: make `BatchStageId` nullable with `SetNull`, and handle deletion in service code explicitly (or keep cascade and accept that deleting a batch stage clears the FK on strategies that belong to an account too — which is actually fine, since the strategy still exists with `BatchStageId = null`).
5. **`StrategyDto`**: No `BatchStageId` in the DTO — it's not exposed via API. Safe.
6. **Migrations snapshot**: `BatchStageId` is `uniqueidentifier NOT NULL` — needs an ALTER COLUMN migration.

### TradingAccount has NO strategies collection today

Adding `ICollection<Strategy> Strategies` to `TradingAccount` is optional but conventional for EF navigation. It's not strictly required if we configure the FK only on `Strategy`.

---

## New API Endpoints Needed

```
GET  api/trading-accounts/{accountId}/strategies?page=&pageSize=
POST api/trading-accounts/{accountId}/strategies   (multipart: .sqx + .html files, strategy name)
```

The `POST` endpoint will:
1. Accept `name` (string), optional `sqxFile` (IFormFile), optional `htmlFile` (IFormFile)
2. Parse the HTML with `IHtmlReportParserService.ParseAsync()`
3. Parse pseudocode from `.sqx` via `ISqxParserService`
4. Create a new `Strategy` with `TradingAccountId = accountId`, `BatchStageId = null`
5. Save and return `StrategyDto`

---

## Frontend New Components

### 1. `AccountDetailComponent` (`/darwinex/demo/:accountId`)

- Loads the account details (`GET api/trading-accounts/:id`)
- Loads strategies (`GET api/trading-accounts/:accountId/strategies`)
- Renders a read-only KPI grid (not inline-editable like stage-detail — or can reuse the same editable pattern)
- "Add Strategy" button opens upload modal
- Back navigation to `/darwinex/demo`

### 2. `AddStrategyModalComponent`

- Two file inputs: `.sqx` file (optional) and `.html` file (optional)
- `name` text input (required)
- On submit: POST multipart to `api/trading-accounts/:accountId/strategies`
- On success: emit added strategy, close modal

### AccountsListComponent change

- Add `(click)="openAccount(acc)"` on each row (or row-level button)
- `openAccount(acc)` navigates to `/darwinex/demo/:accountId` (or `/darwinex/live/:accountId`)
- Needs `Router` injection

---

## Risks

1. **Migration ALTER COLUMN nullability**: SQL Server can ALTER `uniqueidentifier NOT NULL` to `uniqueidentifier NULL` without data loss — existing non-null values remain. However, EF migration must be reviewed manually before running (per project rule: "EF auto-rename guesses can be wrong on big migrations — always review").

2. **OnDelete behavior change**: Changing `Cascade` to `SetNull` on `BatchStageId` means deleting a `BatchStage` no longer deletes its strategies — they become "orphan" pipeline strategies with `BatchStageId = null`. This is different from current behavior. Two mitigations: (a) keep service-level explicit strategy deletion before removing stage (already done in `RollbackStageAsync` via `db.Strategies.RemoveRange(stage.Strategies)`), or (b) use a DB-level check constraint. Since `RollbackStageAsync` and `DeleteAsync` already do explicit `RemoveRange`, changing to `SetNull` is safe for the pipeline flow.

3. **AccountsListComponent uses `ChangeDetectionStrategy.Default`** — inconsistent with OnPush standard. The new `AccountDetailComponent` should be OnPush with Signals. The accounts list can be migrated as part of this change or left for a separate refactor (recommend leaving it — avoid scope creep).

4. **No existing upload endpoint for single strategy to account**: The batch flow uploads a ZIP with multiple strategies. The new flow uploads a single `.sqx` + `.html` pair. The `BatchCreateModal` pattern is a good reference but not directly reusable.

5. **StrategyDto does not expose `TradingAccountId` or `BatchStageId`**: This is intentional. The new account-strategy endpoint can return the same `StrategyDto` record — no DTO change needed.

6. **`GetByStageAsync` query safety**: If `BatchStageId` is nullable, the query `WHERE BatchStageId == stageId` will correctly exclude rows with null FK (SQL: `WHERE BatchStageId = @stageId`). EF Core handles nullable FK comparisons correctly.

---

## Recommendation

Proceed with **Option A** as decided. The constraint analysis confirms:
- No existing code breaks from making `BatchStageId` nullable
- `HtmlReportParserService` is 100% reusable (standalone service, no coupling)
- The existing `StrategyDto` is sufficient — no DTO changes needed
- Migration risk is manageable with a manual review step

The frontend work is straightforward: one new route, two new components, one small change to accounts-list for row click navigation.

---

## Ready for Proposal

Yes — sufficient information gathered. The orchestrator can proceed to `sdd-propose` or go directly to `sdd-spec` since the approach is already decided by the user.

Key open question for proposal/spec phase: **Should `OnDelete` for `BatchStageId` stay as `Cascade` (and rely on service-level explicit removal already in place) or switch to `SetNull`?** Recommendation: switch to `SetNull` — it's safer and aligns with the new "strategy can exist without a stage" semantic.
