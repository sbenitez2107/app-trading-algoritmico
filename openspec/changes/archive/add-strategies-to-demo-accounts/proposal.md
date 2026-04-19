# Proposal: Add Strategies to Demo Accounts

## Intent

Enable loading strategies directly into Darwinex demo accounts, skipping the full SQX pipeline for the current small strategy set (<50). The user clicks a demo account card, lands on a detail page showing strategies booked in that account, and can upload new `.sqx + .html` pairs to attach to the account. This unblocks the path toward future MT4/MT5 live P/L integration without first building a full pipeline run.

## Scope

### In Scope
- Make `Strategy.BatchStageId` nullable (strategy can exist without a pipeline stage)
- Add `Strategy.TradingAccountId` nullable FK + navigation to `TradingAccount`
- Change `BatchStageId` `OnDelete` from `Cascade` to `SetNull`
- New endpoints: `GET api/trading-accounts/{id}/strategies`, `POST api/trading-accounts/{id}/strategies` (multipart `.sqx + .html` + name)
- New route `/darwinex/demo/:accountId` with `AccountDetailComponent`
- New `AddStrategyModalComponent` for file-pair upload
- Row click in `AccountsListComponent` navigates to account detail
- EF migration with manual review for column nullability change

### Out of Scope
- Real P/L and trade data from MT4/MT5 (future phase)
- MT4/MT5 broker connectivity or live order sync
- Refactoring `AccountsListComponent` to OnPush (separate concern)
- Removing or replacing the SQX pipeline flow (coexists)
- Bulk strategy upload to account (single pair only)
- Symmetric `/darwinex/live/:accountId` route (deferred — demo flow first)

## Capabilities

### New Capabilities
- `account-strategies`: managing strategies attached directly to a trading account (list, upload single `.sqx + .html` pair, reuse KPI model)

### Modified Capabilities
- `strategy-model`: `Strategy` gains optional `TradingAccountId` and loosens `BatchStageId` to nullable; delete semantics change from `Cascade` to `SetNull` on stage FK

## Approach

Reuse the existing `Strategy` entity (all 50+ KPI columns + `MonthlyPerformance`) and the standalone `HtmlReportParserService`. A `Strategy` becomes a first-class entity that can live in a pipeline stage, a trading account, or both. Add a new `StrategyService.AddToAccountAsync` that parses `.sqx` + `.html` (existing services) and persists with `TradingAccountId` set and `BatchStageId = null`. Frontend adds one lazy-loaded child route under `darwinex` with a standalone OnPush component using Signals.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Entities/Strategy.cs` | Modified | `BatchStageId` -> nullable; add `TradingAccountId?` + nav |
| `Domain/Entities/TradingAccount.cs` | Modified | Add `ICollection<Strategy> Strategies` nav |
| `Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` | Modified | Optional FK, `SetNull`, new FK to `TradingAccount` |
| `Infrastructure/Migrations/` | New | Alter column + add FK migration (manual review) |
| `Application/Interfaces/IStrategyService.cs` | Modified | Add `GetByAccountAsync`, `AddToAccountAsync` |
| `Infrastructure/Services/StrategyService.cs` | Modified | Implement new methods; reuse parsers |
| `WebAPI/Controllers/StrategiesController.cs` (or new `TradingAccountStrategiesController`) | Modified/New | Two new REST endpoints |
| `web/src/app/features/darwinex/darwinex.routes.ts` | Modified | Add `:accountId` child route |
| `web/src/app/features/darwinex/account-detail/` | New | Detail page with strategy grid |
| `web/src/app/features/darwinex/add-strategy-modal/` | New | Upload modal |
| `web/src/app/features/darwinex/accounts-list/accounts-list.component.html` | Modified | Row click -> router navigate |
| `web/src/app/core/services/strategy.service.ts` | Modified | Add `getByAccount`, `addToAccount` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| EF auto-generated migration misnames or drops data on column nullability change | Med | Manual review of migration before apply; test against local DB first |
| `OnDelete` change from `Cascade` to `SetNull` alters pipeline deletion semantics | Low | `RollbackStageAsync` and `DeleteAsync` already do explicit `RemoveRange` on `stage.Strategies`; `SetNull` only affects strategies not explicitly removed |
| `GetByStageAsync` query behavior with nullable FK | Low | EF Core handles nullable FK equality correctly; `WHERE BatchStageId == stageId` naturally excludes null rows |
| Scope creep into OnPush refactor of `AccountsListComponent` | Low | Explicitly out of scope; only change is adding row click handler |

## Rollback Plan

- EF migration `Down()` reverts `TradingAccountId` column removal and restores `BatchStageId` to `NOT NULL` with `Cascade`
- No data loss: both FKs become nullable, existing rows preserve their `BatchStageId` values, and any account-only strategies inserted post-migration are dropped by rollback (acceptable — they are test/new data from this feature)
- Frontend new routes and components can be removed without side effects (they are purely additive; `accounts-list` row click is the only edit — revert the template line)
- Git revert of the change commit restores previous state

## Dependencies

- Existing `IHtmlReportParserService` (already built — reused as-is)
- Existing `ISqxParserService` (already built — reused as-is)
- Existing `TradingAccount` entity and `TradingAccountService`

## Success Criteria

- [ ] Clicking a demo account row navigates to `/darwinex/demo/:accountId` and loads its strategies
- [ ] Uploading a valid `.sqx + .html` pair creates a `Strategy` with `TradingAccountId` set, `BatchStageId` null, and all KPIs parsed
- [ ] Existing SQX pipeline (`/sqx`) continues to work unchanged — strategies still created with `BatchStageId` set
- [ ] Deleting a `BatchStage` no longer cascades to delete strategies that have a `TradingAccountId`
- [ ] EF migration applies cleanly and reverses cleanly against a local test DB
