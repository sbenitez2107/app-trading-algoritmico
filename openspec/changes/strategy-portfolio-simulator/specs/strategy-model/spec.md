# Delta for Strategy Model

> **Revision 3 — documentation reconciliation.** Corrections and additions below bring this
> spec in line with what shipped after the correction work units. NO code changed in that pass;
> every requirement added or amended names the test that already pins it.

## Deleted vs Rewritten (this revision)

DELETED ENTIRELY — filename-based many-to-many attribution no longer exists:
"Backtest Run Attribution By Filename Match" and all 4 of its scenarios
(unique-name match, duplicate-name fan-out, cascade-drops-link-not-run,
no-match-leaves-untouched). The `BacktestRunStrategy` join table,
`StrategyNameKey`, `RunLabel`, and the derived `Unmatched` status all die
with it (see `sqx-backtest-import`).

REWRITTEN AS — attribution is now an explicit FK set at import time, so there
is nothing to "match": see "Backtest Runs Are Strategy-Scoped By
Construction" below. Deleting a `Strategy` now cascades to delete its
`BacktestRun`s AND their `BacktestTrade`s — a BEHAVIOUR CHANGE from the prior
draft, where a cascade removed only link rows and runs survived orphaned.

## ADDED Requirements

### Requirement: Backtest Runs Are Strategy-Scoped By Construction

`BacktestRun.StrategyId` MUST be a NOT NULL foreign key to `Strategy`, set
from the import route at creation time, never inferred or matched after the
fact. The relationship `Strategy -> BacktestRun -> BacktestTrade` MUST
cascade on delete: deleting a `Strategy` deletes every one of its
`BacktestRun` rows and their `BacktestTrade` rows.

#### Scenario: A run is always attributed at creation

- GIVEN strategy `S1` exists
- WHEN a trade-list file is imported to `POST /api/strategies/S1/backtests/deploy`
- THEN the created `BacktestRun.StrategyId = S1` from the route, with no matching step and no possibility of an unmatched run

#### Scenario: Deleting a strategy deletes its runs and trades

- GIVEN `Strategy S1` owns a `BacktestRun` holding trades
- WHEN `S1` is deleted
- THEN the `BacktestRun` row and every one of its `BacktestTrade` rows are deleted by cascade — unlike the prior draft, where only link rows were removed and the runs survived orphaned
- PINNED BY `BacktestSchemaTests.DeletingAStrategy_DeletesItsRunsAndTheirTrades` (real SQLite cascade)

(Revision 3 correction: the premise previously read "a Deploy run with 329 trades
and an Evaluation run with 337 trades … all 666". The 337-row figure is
`_OOST.csv`, which the single-sample-type guard rejects whole, so that pairing
cannot exist. The behaviour was always covered; only the illustrative arithmetic
was dead.)

#### Scenario: Two strategies sharing an underlying SQX strategy each own their own run

- GIVEN the same SQX strategy is deployed as `Strategy S1` (FTMO-Demo2) and `Strategy S2` (SBDEMO2)
- WHEN the identical trade-list file is imported to each strategy's Deploy slot
- THEN two independent `BacktestRun` rows exist, one per strategy, each deletable independently of the other
- PINNED BY `BacktestImportServiceTests.ImportTradeListAsync_IdenticalBytesForTwoStrategies_BothImportAndShareOneContentHash`; `BacktestSchemaTests.BacktestRun_SameContentHashUnderTwoStrategies_BothPersist`

### Requirement: The Reshape Migration's Rollback Executes, And Declares The Data It Discards

The migration that reshapes `BacktestRun` onto strategy-scoped identity MUST
have a `Down()` that can actually run against the ordinary steady state. That
state is TWO rows per strategy — one Deploy slot, one Evaluation slot — and the
prior revision's identity columns are restored with an empty default, so any
unique index over them collides at the second row. `Down()` MUST therefore
DISCARD the imported `BacktestTrade` and `BacktestRun` rows, child rows first,
BEFORE creating any index that surviving rows could not satisfy. The discard
MUST be stated explicitly in the migration's own documentation rather than
implied: the prior revision's identity was parsed out of a file-name convention
that no longer exists, so there is nothing left to derive it from. The loss is
acceptable ONLY because every discarded row is reproducible by re-importing its
source file; it MUST NOT be extended to live trade data.

#### Scenario: The rollback does not demand uniqueness of rows it leaves in place

- GIVEN the reshaped schema holds the ordinary two-slot steady state
- WHEN the migration's `Down()` operations are inspected in order
- THEN the row-clearing operations precede every unique-index creation over the restored identity columns
- PINNED BY `ReshapeBacktestRunsMigrationDownTests.Down_DoesNotDemandUniquenessOfRowsItLeavesInPlace`

#### Scenario: The rollback discards rather than pretending it can rebuild

- GIVEN the rollback is applied
- WHEN its effect on imported data is inspected
- THEN `BacktestTrades` and then `BacktestRuns` are cleared, and the discard is documented as a stated loss, not a silent one
- PINNED BY `ReshapeBacktestRunsMigrationDownTests.Down_DiscardsTheImportedRunsRatherThanPretendingItCanRebuildThem`
