# Strategy Model Specification

## Purpose

Define the updated domain model for `Strategy`: `BatchStageId` becomes nullable,
`TradingAccountId` is added as an optional FK, and delete semantics for the
BatchStage relationship change from Cascade to SetNull with explicit preservation
rules.

---

## Requirements

### Requirement: BatchStageId Is Nullable

`Strategy.BatchStageId` MUST be changed to `Guid?` (nullable). Existing strategies
with a `BatchStageId` value MUST NOT be affected. The EF migration MUST alter the
column to allow NULL without data loss.

#### Scenario: Existing pipeline strategy unaffected

- GIVEN a strategy with `BatchStageId = stage-1` exists before migration
- WHEN the migration runs
- THEN the strategy still has `BatchStageId = stage-1` and all KPI data intact

#### Scenario: New strategy can be created without a stage

- GIVEN no pipeline stage is involved
- WHEN a strategy is created with `BatchStageId = null` and `TradingAccountId = acc-1`
- THEN the strategy persists successfully with `BatchStageId` as NULL in the DB

---

### Requirement: TradingAccountId Is a Nullable FK to TradingAccount

`Strategy` MUST have a nullable `TradingAccountId` (Guid?) with a corresponding
navigation property to `TradingAccount`. `TradingAccount` MUST expose an
`ICollection<Strategy>` navigation property. The FK MUST use `SetNull` on delete
of `TradingAccount` (consistent with the stage FK behavior after this change).

#### Scenario: Strategy linked to account

- GIVEN a trading account `acc-1` exists
- WHEN a strategy is persisted with `TradingAccountId = acc-1`
- THEN `strategy.TradingAccount` navigation resolves to `acc-1`
- AND `tradingAccount.Strategies` contains the strategy

#### Scenario: Strategy with null TradingAccountId (pipeline-only)

- GIVEN a strategy created via the SQX pipeline with `BatchStageId = stage-1`
- WHEN the strategy is loaded from the DB
- THEN `TradingAccountId` is NULL and `TradingAccount` navigation is null

---

### Requirement: Deleting a BatchStage Sets BatchStageId Null on Account-Linked Strategies

When a `BatchStage` is deleted, strategies that have a `TradingAccountId` set
MUST NOT be deleted. Their `BatchStageId` MUST be set to NULL (via DB-level
`SetNull` or explicit service logic). Strategies that have NO `TradingAccountId`
(pipeline-only) continue to be removed explicitly via `RemoveRange` in
`BatchService` before stage deletion.

#### Scenario: Stage deleted — strategy has TradingAccountId

- GIVEN a strategy with `BatchStageId = stage-1` AND `TradingAccountId = acc-1`
- WHEN `BatchService.DeleteAsync` or `RollbackStageAsync` is called for `stage-1`
- THEN the strategy is NOT deleted
- AND its `BatchStageId` becomes NULL
- AND its `TradingAccountId` remains `acc-1`

#### Scenario: Stage deleted — pipeline-only strategy is removed

- GIVEN a strategy with `BatchStageId = stage-1` AND `TradingAccountId = null`
- WHEN `BatchService.DeleteAsync` or `RollbackStageAsync` removes strategies for `stage-1`
- THEN the strategy IS deleted (existing `RemoveRange` behavior, unchanged)

#### Scenario: EF OnDelete configuration

- GIVEN the `StrategyConfiguration` sets `OnDelete(DeleteBehavior.SetNull)` for the BatchStage FK
- WHEN a BatchStage is deleted WITHOUT explicit service-layer `RemoveRange`
- THEN EF sets `BatchStageId = null` on all remaining strategies for that stage (DB-level fallback)

---

### Requirement: Orphan Strategy Is Invalid Domain State

A strategy with BOTH `BatchStageId = null` AND `TradingAccountId = null` is
considered an invalid domain state. The system SHOULD prevent this by construction
(never create a strategy without at least one FK set). No DB constraint is required;
enforcement is at the service layer.

#### Scenario: Service prevents orphan creation

- GIVEN a caller attempts to create a strategy with no BatchStageId and no TradingAccountId
- WHEN `AddToAccountAsync` or any strategy creation method is invoked
- THEN the method throws a domain exception or returns a validation error before persisting

#### Scenario: Rollback from dual-linked to account-only is valid

- GIVEN a strategy with both `BatchStageId = stage-1` and `TradingAccountId = acc-1`
- WHEN `stage-1` is deleted (SetNull applied)
- THEN the strategy has `BatchStageId = null`, `TradingAccountId = acc-1` — valid, not orphaned
