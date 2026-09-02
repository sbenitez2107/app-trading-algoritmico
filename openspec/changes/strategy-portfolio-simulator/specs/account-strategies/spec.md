# Delta for Account Strategies

> **Revision 3 — documentation reconciliation.** The text below was written for revision 2 and
> is corrected here to match what shipped after the correction work units. NO code changed in
> this pass; every requirement added or amended below names the test that already pins it.

## ADDED Requirements

### Requirement: Strategy Grid Exposes A Backtest Readiness Marker

`StrategyDto`, as returned by `GetByAccountAsync`, MUST include a derived
backtest-readiness marker computed server-side per strategy: `None` (white)
when NO `BacktestRun` of either kind HOLDS AT LEAST ONE trade; `SizingOnly`
(amber) when at least one run holds trades but the strategy is not fully
evaluable (no Evaluation run, or no WF export, or no trade at/after
`OosFromDate`); `Evaluable` (green) when an Evaluation run, a WF export, AND at
least one trade at/after `OosFromDate` all exist. The marker MUST be computed
for the whole requested page in one additional query, not per row and not
client-side.

(Revision 3 correction: white and amber were previously stated in terms of RUN
ROWS — `None` "when no `BacktestRun` exists", `SizingOnly` "when a Deploy run
exists". Both conditions are keyed on TRADES, and amber covers ANY run holding
trades, Deploy or Evaluation. See "A Run Row Is Not Evidence" below and
`design.md` D12.)

#### Scenario: No backtest data

- GIVEN strategy `S1` has no `BacktestRun`
- WHEN the account's strategy grid loads
- THEN `S1`'s marker is `None` (white)
- PINNED BY `StrategyServiceBacktestReadinessTests.GetByAccountAsync_StrategyWithNoRun_IsNone`

#### Scenario: Deploy run holding trades only

- GIVEN `S1` has a Deploy run holding trades and nothing else
- WHEN the grid loads
- THEN `S1`'s marker is `SizingOnly` (amber) — sizing is available, evaluation is not
- PINNED BY `StrategyServiceBacktestReadinessTests.GetByAccountAsync_DeployRunOnly_IsSizingOnly`

#### Scenario: Fully evaluable

- GIVEN `S1` has an Evaluation run, a WF export, and at least one trade on/after `OosFromDate`
- WHEN the grid loads
- THEN `S1`'s marker is `Evaluable` (green)

#### Scenario: Evaluation run without its WF export is still amber

- GIVEN `S1` has an Evaluation run holding trades but no WF export yet
- WHEN the grid loads
- THEN `S1`'s marker is `SizingOnly` (amber), not green — the OOS boundary is unavailable, not assumed satisfied
- PINNED BY `StrategyServiceBacktestReadinessTests.GetByAccountAsync_EvaluationRunWithoutItsExport_IsStillSizingOnly`

### Requirement: A Run Row Is Not Evidence — The Marker Requires Trades

Both `SizingOnly` and `Evaluable` are affirmative claims about what a strategy
supports, and position sizing is derived from trades rather than from the
existence of a run row. A `BacktestRun` holding ZERO `BacktestTrade` rows MUST
therefore contribute NOTHING to the marker: a strategy whose only runs are empty
MUST report `None`, not `SizingOnly`. An empty run alongside a run that holds
trades MUST NOT downgrade the marker either — the rule is existential over runs
holding trades, not universal over run rows.

#### Scenario: A run holding zero trades is white, not amber

- GIVEN `S1` has a Deploy run with no `BacktestTrade` rows
- WHEN the grid loads
- THEN `S1`'s marker is `None` (white) — the run row alone is not sizing evidence
- PINNED BY `StrategyServiceBacktestReadinessTests.GetByAccountAsync_RunHoldingZeroTrades_IsNone_NotSizingOnly`

#### Scenario: An empty run beside a populated one is still amber

- GIVEN `S1` has one run holding trades and one run holding none
- WHEN the grid loads
- THEN `S1`'s marker is `SizingOnly` (amber) — the empty run neither adds nor removes evidence
- PINNED BY `StrategyServiceBacktestReadinessTests.GetByAccountAsync_AnEmptyRunAlongsideARunWithTrades_IsStillSizingOnly`

### Requirement: The Readiness Column Renders Translated Text, Not A Translation Key

The grid's readiness column MUST render the marker as human-readable text in the
active language, and MUST follow a language switch without a page reload. A
translation KEY MUST NOT reach a rendered cell. Because the column's formatter is
a plain function rather than a template binding, no translation pipe runs inside
it: the label MUST therefore be resolved through a reactive translation source
(an observable stream, never a synchronous lookup that can answer with the key
before the translation files have loaded) that the column definition depends on.

#### Scenario: The column formatter emits a label, not a key

- GIVEN the strategies grid is rendered with a strategy whose marker is `Evaluable`
- WHEN the readiness column's own `valueFormatter` produces the cell value
- THEN the value is the translated label (e.g. "Evaluable"), never `SQX.BACKTESTS.READINESS_*`
- PINNED BY `account-detail.component.spec.ts > readinessColumn_RendersTranslatedText_NotTheRawTranslationKey`

#### Scenario: The column follows a language switch

- GIVEN the grid is rendered in one language
- WHEN the active language changes
- THEN the readiness column's labels re-render in the new language with no reload and no re-fetch
- PINNED BY `account-detail.component.spec.ts > readinessColumn_FollowsALanguageSwitch`

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
