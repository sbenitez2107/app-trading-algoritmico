# SQX Backtest Import Specification

> **Revision 3 — documentation reconciliation.** Corrections and additions below bring this
> spec in line with what shipped after the correction work units. NO code changed in that pass;
> every requirement added or amended names the test that already pins it.

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

#### Scenario: The importable fixture parses with the correct per-column convention

- GIVEN `ListOfTrades_XAUUSD_H1_IST.csv` — 329 rows, the only committed trade-list fixture that is importable as a run
- WHEN it is imported
- THEN every row's `OpenPrice`/`ClosePrice` parses as dot-decimal (e.g. `"1066.19"` → `1066.19`) and every row's `Size`/`Profit/Loss`/`Balance`/`MAE`/`MFE` parses as comma-decimal (e.g. `"0,44000"` → `0.44000`), across all 329 rows, regardless of host culture
- PINNED BY `SqxTradeListParserTests.ParseAsync_F1Fixture_Parses329Rows`; `.ParseAsync_UnderDeDeCulture_ProducesIdenticalResult`

(Revision 3 correction: this scenario previously demanded "both fixtures … 666 rows
total", counting `_OOST.csv`'s 337 rows. The single-sample-type requirement below
rejects that fixture WHOLE, so no row of it is ever parsed into a run — the 666-row
claim was unsatisfiable by construction, not merely untested. `_OOST.csv` survives
only as that rejection's regression fixture.)

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

### Requirement: A File With No Usable Trade Row Is Rejected Whole, Never Imported As A Success

A trade-list file that yields ZERO accepted trade rows MUST be rejected at
FILE level and MUST NOT be reported as a successful import. Both shapes count:
a file carrying only a valid header and no data rows, and a file whose every
data row was individually rejected. The rejection message MUST distinguish the
two cases and MUST state how many data rows were rejected in the second. This
guard MUST run AFTER the single-symbol and single-sample-type guards, so a file
failing one of those still receives the more specific diagnosis. A rejected file
MUST leave an already-occupied `(StrategyId, Kind)` slot exactly as it was — the
outcome `Rejected` writes nothing, and is the fourth outcome alongside
`Imported`/`Unchanged`/`Replaced`.

#### Scenario: A header-only file is rejected, not counted as an import

- GIVEN a file with a valid 16-column trade-list header and no data rows
- WHEN it is imported to a slot
- THEN the outcome is `Rejected`, naming "no trade rows", and nothing is persisted
- PINNED BY `SqxTradeListParserTests.ParseAsync_HeaderOnlyFile_IsRejectedWholeAndNotReportedAsASuccessfulImport`

#### Scenario: Every data row rejected rejects the file, naming the count

- GIVEN a file whose every data row is individually rejected
- WHEN it is imported
- THEN the outcome is `Rejected`, naming "no usable trade rows" and the number of rejected rows
- PINNED BY `SqxTradeListParserTests.ParseAsync_EveryDataRowRejected_RejectsTheWholeFileNamingTheCount`

#### Scenario: A rejected file does not wipe an occupied slot

- GIVEN `S1`'s Deploy slot already holds a run with its trades
- WHEN a header-only file is imported to that same slot
- THEN the outcome is `Rejected` and the existing run and all its trades remain intact — no `Replaced`, no partial delete
- PINNED BY `BacktestImportServiceTests.ImportTradeListAsync_HeaderOnlyFileIntoAnOccupiedSlot_LeavesTheRunIntact`

### Requirement: The Read Page Distinguishes A Backend Failure From An Empty Dataset

The backtests read page loads runs and calibrations as TWO independent requests.
A failing request MUST surface an error message in that panel, rendered with an
assertive live-region role, and MUST NOT fall through to the panel's "nothing
imported yet" empty state — the two are different facts and rendering them
identically hides an outage. The two panels MUST fail independently: a
calibration outage MUST NOT claim the runs list failed. An error MUST clear once
a later load of the same panel succeeds.

#### Scenario: A failing runs load shows an error, not the empty state

- GIVEN the runs request fails
- WHEN the page loads
- THEN the runs panel renders the error and does NOT render its empty state
- PINNED BY `backtests-list.component.spec.ts > loadRuns_WhenTheRequestFails_ShowsAnErrorAndNotTheEmptyState`

#### Scenario: The calibrations panel fails independently

- GIVEN the calibrations request fails while the runs request succeeds
- WHEN the page loads
- THEN only the calibrations panel reports an error; the runs panel renders its rows normally
- PINNED BY `backtests-list.component.spec.ts > loadCalibrations_WhenTheRequestFails_ShowsAnErrorAndNotTheEmptyState`

#### Scenario: A recovered load clears the error

- GIVEN the runs panel is showing an error from a previous failed load
- WHEN a later runs load succeeds
- THEN the error is cleared and the rows are rendered
- PINNED BY `backtests-list.component.spec.ts > loadRuns_AfterAFailure_ClearsTheErrorOnceItSucceeds`

### Requirement: Run Identity Is (StrategyId, Kind); ContentHash Is A De-Dup Key, Not Identity

`BacktestRun` identity MUST be the unique pair `(StrategyId, Kind)`.
`ContentHash` (SHA-256 over raw bytes) MUST NOT carry a unique index — the
same bytes MAY legitimately back two runs under two different strategies.
Importing into an empty `(StrategyId, Kind)` slot MUST produce `Imported`.
Importing into an occupied slot with identical `ContentHash` MUST produce
`Unchanged` and write nothing. Importing into an occupied slot with a
different `ContentHash` MUST produce `Replaced`: prior trades removed, new
trades inserted, no second run created. These three outcomes describe an
ACCEPTED file only; a file rejected at parse time produces `Rejected` and leaves
the slot untouched (see "A File With No Usable Trade Row Is Rejected Whole").

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
