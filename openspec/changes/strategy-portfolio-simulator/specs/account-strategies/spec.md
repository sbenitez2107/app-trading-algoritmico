# Delta for Account Strategies

## ADDED Requirements

### Requirement: Strategy Grid Exposes A Backtest Readiness Marker

`StrategyDto`, as returned by `GetByAccountAsync`, MUST include a derived
backtest-readiness marker computed server-side per strategy: `None` (white)
when no `BacktestRun` exists; `SizingOnly` (amber) when a Deploy run exists
but the strategy is not fully evaluable (no Evaluation run, or no WF export,
or the Evaluation run has zero trades at/after `OosFromDate`); `Evaluable`
(green) when an Evaluation run, a WF export, AND at least one trade at/after
`OosFromDate` all exist. The marker MUST be computed for the whole requested
page in one additional query, not per row and not client-side.

#### Scenario: No backtest data

- GIVEN strategy `S1` has no `BacktestRun`
- WHEN the account's strategy grid loads
- THEN `S1`'s marker is `None` (white)

#### Scenario: Deploy run only

- GIVEN `S1` has a Deploy run and nothing else
- WHEN the grid loads
- THEN `S1`'s marker is `SizingOnly` (amber) — sizing is available, evaluation is not

#### Scenario: Fully evaluable

- GIVEN `S1` has an Evaluation run, a WF export, and at least one trade on/after `OosFromDate`
- WHEN the grid loads
- THEN `S1`'s marker is `Evaluable` (green)

#### Scenario: Evaluation run without its WF export is still amber

- GIVEN `S1` has an Evaluation run but no WF export yet
- WHEN the grid loads
- THEN `S1`'s marker is `SizingOnly` (amber), not green — the OOS boundary is unavailable, not assumed satisfied

### Requirement: The Per-Row Import Action Declares The Run Kind Via Three Labelled Slots

The strategies grid's per-row Actions cellRenderer MUST expose an import
action opening one modal with three independently optional, independently
re-importable, labelled slots: Deploy, Evaluation, and WF Export. The slot the
user drops a file into IS the declaration of what that file is — the system
MUST NOT offer a single unlabelled drop zone that infers the kind.

#### Scenario: Partial import is valid

- GIVEN the import modal is open for `S1`
- WHEN the user fills only the Deploy slot and submits
- THEN only the Deploy run is imported; Evaluation and WF Export remain whatever they were before

#### Scenario: A file dropped in the wrong-shaped slot is rejected naming the mismatch

- GIVEN the import modal is open for `S1`
- WHEN a WF-export file is dropped into the Deploy slot
- THEN the submission is rejected, naming the header-shape mismatch, and no partial write occurs for that slot
