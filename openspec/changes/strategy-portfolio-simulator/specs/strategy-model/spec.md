# Delta for Strategy Model

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

- GIVEN `Strategy S1` has a Deploy run with 329 trades and an Evaluation run with 337 trades
- WHEN `S1` is deleted
- THEN both `BacktestRun` rows and all 666 `BacktestTrade` rows are deleted by cascade — unlike the prior draft, where only link rows were removed and the runs survived orphaned

#### Scenario: Two strategies sharing an underlying SQX strategy each own their own run

- GIVEN the same SQX strategy is deployed as `Strategy S1` (FTMO-Demo2) and `Strategy S2` (SBDEMO2)
- WHEN the identical trade-list file is imported to each strategy's Deploy slot
- THEN two independent `BacktestRun` rows exist, one per strategy, each deletable independently of the other
