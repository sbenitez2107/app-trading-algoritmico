# SQX Backtest Import Specification

## Purpose

Import AlgoWizard trade-list CSV exports as strategy-scoped backtest runs.
Import happens as a per-row action on a KNOWN strategy — attribution is an
explicit FK set by the route, never inferred from the file. This capability
imports and reports only: it recommends nothing, resizes nothing, and never
touches live `StrategyTrade` data. WF-export parsing lives in the separate
`walk-forward-export` capability; this spec covers the two AlgoWizard
trade-list artifacts (Deploy run, Evaluation run) only.

## Deleted vs Rewritten (this revision)

DELETED — the entire filename-inferred-attribution defect class no longer
exists, because the strategy is known before the file is read:
- "Strategy Name Extracted From Filename"
- "Attribution Status Is Derived, Never Stored" (and its `Unmatched` scenarios)
- "Multi-File Import In One Operation" (117+ files, one request) — import is
  now one file per slot per strategy, at most 3 slots total
- "Batch Result Reports Every File" — there is no batch, so no batch report

REWRITTEN:
- "CSV Format Parsing" — the prior draft claimed ONE shared comma-decimal
  policy for the whole file. That was wrong: `Open price`/`Close price` use a
  DOT, everything else numeric uses a COMMA. Rewritten below with the
  per-column rule and a must-fail scenario.
- "Walk-Forward Segment Preserved" — narrowed. The parser still preserves the
  raw `Sample type` literal; the import service now REJECTS a file carrying
  more than one distinct value, because a Deploy/Evaluation run must be one
  coherent sample.
- "Re-Import Idempotency" — replaced entirely. Identity used to be
  `ContentHash` alone (globally unique). It is now `(StrategyId, Kind)`;
  `ContentHash` is a de-dup key, not identity, because the same bytes
  legitimately import twice under two strategies (one SQX strategy deployed
  on two accounts).

SURVIVES UNCHANGED (specified elsewhere, not re-specified here): retry safety
via `IBacktestDbContextFactory`; `BacktestFieldLengths` + parser-level length
validation + the per-file exception boundary; `Backtest Trades Never Touch
Live Trade Storage`; `Close Reason Preserved`; `Realized Risk Captured Only
For SL-Closed Trades`; `Nullable StopLoss From First Migration`; `Degenerate
Row Rejected, Not Divided By Zero`.

## Requirements

### Requirement: Import Is Strategy-Scoped By Construction

The system MUST expose trade-list import as `POST
/api/strategies/{strategyId}/backtests/{kind}`, where `{kind}` is a route
segment restricted to `deploy` or `evaluation`. The target `Strategy` MUST be
resolved from the route before the file is read; `BacktestRun.StrategyId`
MUST be a NOT NULL foreign key set from the route, never derived from file
content. An unrecognized `{kind}` value MUST be rejected with `400` by
route/model binding before the service or the file is touched.

#### Scenario: Deploy run imported for a known strategy

- GIVEN strategy `S1` exists with no backtest runs
- WHEN `ListOfTrades_XAUUSD_H1_IST.csv` is posted to `POST /api/strategies/S1/backtests/deploy`
- THEN a `BacktestRun` is created with `StrategyId=S1`, `Kind=Deploy`, and 329 trades persisted

#### Scenario: Unrecognized kind is rejected before parsing

- WHEN a file is posted to `POST /api/strategies/S1/backtests/bogus`
- THEN the response is `400`, the file is never opened, and no `BacktestRun` is created

### Requirement: Trade-List CSV Format Parsing Uses Two Decimal Conventions

The parser MUST accept files with `;` delimiter, UTF-8 quoted fields, dates in
`yyyy.MM.dd HH:mm:ss`, and exactly 16 columns (Ticket, Symbol, Type, Open
time, Open price, Size, Close time, Close price, Profit/Loss, Balance, Sample
type, Close type, MAE ($), MFE ($), Time in trade, Comment). `Open price` and
`Close price` MUST parse with a DOT decimal separator. `Size`, `Profit/Loss`,
`Balance`, `MAE ($)`, `MFE ($)` MUST parse with a COMMA decimal separator.
Parsing MUST be culture-invariant and MUST NOT apply one shared decimal
policy to the whole file.

#### Scenario: Both fixtures parse with the correct per-column convention

- GIVEN `ListOfTrades_XAUUSD_H1_IST.csv` (329 rows) and `_OOST.csv` (337 rows) — 666 rows total
- WHEN each is imported
- THEN every row's `OpenPrice`/`ClosePrice` parses as dot-decimal (e.g. `"1066.19"` → `1066.19`) and every row's `Size`/`Profit/Loss`/`Balance`/`MAE`/`MFE` parses as comma-decimal (e.g. `"0,44000"` → `0.44000`), across all 666 rows, regardless of host culture

#### Scenario: A single shared decimal policy would corrupt one side (must-fail guard)

- GIVEN a row with `Open price = "1066.19"` and `Size = "0,44000"`
- WHEN the file is parsed
- THEN `OpenPrice = 1066.19` (dot as decimal) AND `Size = 0.44000` (comma as decimal) — a parser applying one shared rule to both columns fails this scenario

#### Scenario: Wrong delimiter rejects the whole file

- GIVEN a file using `,` instead of `;`
- WHEN imported
- THEN rejected with "invalid delimiter", zero trades persisted

#### Scenario: Unparseable date rejects the whole file

- GIVEN a row with a date not matching `yyyy.MM.dd HH:mm:ss`
- WHEN imported
- THEN rejected, naming the row and column, zero trades persisted

#### Scenario: Missing column rejects the whole file

- GIVEN a file missing "Close type"
- WHEN imported
- THEN rejected with "missing column: Close type" before any row is persisted

### Requirement: Trade-List Import Requires A Single Sample Type

A trade-list file MUST contain exactly one distinct `Sample type` value across
all rows to be accepted as a Deploy or Evaluation run. A file with more than
one distinct value MUST be rejected whole, naming every distinct value
observed, before any row is persisted. The parser MUST preserve the accepted
file's `Sample type` literal on every `BacktestTrade`.

#### Scenario: Single-segment file is accepted

- GIVEN `ListOfTrades_XAUUSD_H1_IST.csv`, where all 329 rows carry `Sample type = "IST"`
- WHEN imported
- THEN accepted; every trade's stored segment is `"IST"`

#### Scenario: Multi-segment file is rejected whole

- GIVEN `ListOfTrades_XAUUSD_H1_OOST.csv`, which carries two distinct values (`"IS"` on 151 rows, `"OOS1"` on 186 rows)
- WHEN imported as a Deploy or Evaluation run
- THEN rejected naming both observed values, zero trades persisted (this fixture is retained only as this rejection's regression fixture)

### Requirement: Run Identity Is (StrategyId, Kind); ContentHash Is A De-Dup Key, Not Identity

`BacktestRun` identity MUST be the unique pair `(StrategyId, Kind)`.
`ContentHash` (SHA-256 over raw bytes) MUST NOT carry a unique index — the
same bytes MAY legitimately back two runs under two different strategies.
Importing into an empty `(StrategyId, Kind)` slot MUST produce `Imported`.
Importing into an occupied slot with identical `ContentHash` MUST produce
`Unchanged` and write nothing. Importing into an occupied slot with a
different `ContentHash` MUST produce `Replaced`: prior trades removed, new
trades inserted, no second run created.

#### Scenario: Import into an empty slot

- GIVEN strategy `S1` has no Deploy run
- WHEN a file is imported to the Deploy slot
- THEN outcome is `Imported`, trades persisted

#### Scenario: Identical re-import is a no-op

- GIVEN `S1`'s Deploy slot already holds a run imported from this exact file
- WHEN the identical bytes are re-imported to the Deploy slot
- THEN outcome is `Unchanged`, nothing is written, trade count is unchanged

#### Scenario: Different content replaces the run

- GIVEN `S1`'s Deploy slot holds a run
- WHEN a file with different bytes is imported to the Deploy slot
- THEN outcome is `Replaced`: the prior trades are gone, the new file's trades are persisted, and exactly one Deploy run still exists for `S1`

#### Scenario: Identical bytes legitimately back two strategies (anti-regression for the dropped unique index)

- GIVEN the same SQX strategy is deployed under `Strategy S1` (FTMO-Demo2) and `Strategy S2` (SBDEMO2)
- WHEN the identical trade-list file is imported to `S1`'s Deploy slot and then to `S2`'s Deploy slot
- THEN both imports succeed as `Imported`, two `BacktestRun` rows exist with the same `ContentHash`, and no unique-constraint violation occurs

### Requirement: A Deploy File Declared As Evaluation Is Stored, Never Detected

Nothing distinguishes a Deploy file from an Evaluation file structurally —
both are the same 16-column shape, produced by AlgoWizard from different
parameter sets. The system MUST NOT attempt to infer or validate whether a
file's actual parameters match its declared `Kind`. The declared `Kind` MUST
be accepted and stored as given. The only cross-check available is manual:
the WF export's `DeployParameters`/`EvaluationParameters` text (see
`walk-forward-export`), which the user can compare by eye against what they
know they deployed.

#### Scenario: A deploy-run file declared as Evaluation imports unconditionally

- GIVEN a trade-list file produced from the strategy's currently-deployed (last-window) parameters
- WHEN it is posted to `POST /api/strategies/S1/backtests/evaluation`
- THEN it is accepted, persisted with `Kind=Evaluation`, and the system raises no warning and performs no content-based check — the mislabeling is undetectable by construction

### Requirement: A WF Export File Posted To A Trade-List Slot Is Rejected (Detectable Case)

The trade-list and WF-export header lines are structurally different and MUST
be validated on import. A file whose header does not match the trade-list's
16-column shape MUST be rejected at the trade-list endpoints, naming the
mismatch, before any row is persisted.

#### Scenario: WF export posted to the Deploy slot

- WHEN `WFParamsExport_XAUUSD_H1.csv` (13-column WF-export header) is posted to `POST /api/strategies/S1/backtests/deploy`
- THEN rejected with "expected trade-list header, found a different column shape", zero trades persisted
