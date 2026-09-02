# Walk-Forward Export Specification

> **Revision 3 — documentation reconciliation.** Corrections and additions below bring this
> spec in line with what shipped after the correction work units. NO code changed in that pass;
> every requirement added or amended names the test that already pins it.

## Purpose

Parse and own the SQX Optimizer "Walk-Forward Results" export: the boundary
date and per-window IS/OOS KPIs that let an Evaluation run's trades be read as
genuinely out-of-sample. This is a NEW capability — the prior draft of this
change had no WF-export parser, only the trade-list importer. The WF-export
parser is a SEPARATE service from the trade-list parser (see
`sqx-backtest-import`): the two files use inverted decimal and date
conventions, so they MUST NOT share a parsing policy.

## Requirements

### Requirement: WF Export Import Is Strategy-Scoped, One Per Strategy

The system MUST expose WF-export import as `POST
/api/strategies/{strategyId}/walk-forward`. A strategy MUST have at most one
`StrategyWalkForwardExport` (unique `StrategyId`, cascade-deletes its
`WalkForwardWindow` rows).

#### Scenario: First import for a strategy

- GIVEN strategy `S1` has no WF export
- WHEN `WFParamsExport_XAUUSD_H1.csv` is posted to `POST /api/strategies/S1/walk-forward`
- THEN a `StrategyWalkForwardExport` is created for `S1` with 6 `WalkForwardWindow` rows

#### Scenario: Re-import replaces the prior export

- GIVEN `S1` already has a WF export with 6 windows
- WHEN an updated WF export file (a new final window) is imported for `S1`
- THEN the prior export and its windows are removed, the new 6 windows are persisted, and `OosFromDate` is recomputed from the new file

### Requirement: WF Export CSV Uses Comma Decimals And dd.MM.yyyy Dates — The Opposite Of The Trade List

The parser MUST accept `;`-delimited, UTF-8 quoted files with exactly these
columns: Period IS, Period OOS, Days IS, Days OOS, Net profit (IS/OOS),
Ret/DD Ratio (IS/OOS), Drawdown (IS/OOS), Avg. Trades Per Month (IS/OOS),
Parameters. Every numeric column (Days, Net profit, Ret/DD Ratio, Drawdown,
Avg. Trades Per Month, both IS and OOS) MUST parse with a COMMA decimal
separator. Each `Period` column MUST parse as two dates joined by `" - "`,
each in `dd.MM.yyyy` format. This parser MUST NOT share its decimal or date
policy with the trade-list parser.

#### Scenario: Fixture parses fully with comma decimals

- GIVEN `WFParamsExport_XAUUSD_H1.csv` (6 data rows)
- WHEN imported
- THEN row 2's `Net profit (IS)` `"15239,94"` parses to `15239.94` and its `Ret/DD Ratio (IS)` `"20,68"` parses to `20.68` — comma as decimal, not a thousands separator

#### Scenario: dd.MM.yyyy is applied, not the trade list's format (must-fail guard)

- GIVEN row 5's `Period IS` = `"20.02.2019 - 08.05.2024"`
- WHEN parsed
- THEN the start date is 2019-02-20 (day=20, month=02) — a parser applying `yyyy.MM.dd` cannot parse this value at all

#### Scenario: Wrong delimiter or missing column rejects the whole file

- GIVEN a WF export using `,` instead of `;`, or missing the `Parameters` column
- WHEN imported
- THEN rejected before any window is persisted, naming the problem

### Requirement: The WF Parser Enforces The Shared Field-Length Limits

The WF-export parser MUST validate the two length-bounded values it produces
against the SAME shared length constants the database columns are configured
from, so an over-length value is refused while it is still data rather than
surfacing as a non-transient truncation error at persistence time (which no
retry strategy recovers from). The uploaded file NAME MUST NOT exceed the
`FileNameOrKey` limit (260). A row's `Parameters` text MUST NOT exceed the
`WalkForwardParameters` limit (1000). Both violations MUST reject the file
WHOLE — unlike the trade list, a WF export's row ORDER carries meaning (the
boundary is the second-to-last row's OOS start), so dropping one row would
silently move the boundary. The `Parameters` rejection MUST name the offending
row and the limit. A value EXACTLY at the limit MUST be accepted.

#### Scenario: Over-length Parameters rejects the file, naming the row and the limit

- GIVEN a WF export whose row 3 `Parameters` text exceeds 1000 characters
- WHEN it is imported
- THEN the file is rejected whole, naming row 3 and the 1000-character limit, and no window is persisted
- PINNED BY `WalkForwardExportParserTests.ParseAsync_OverLengthParameters_RejectsTheWholeFileNamingTheRowAndTheLimit`

#### Scenario: Parameters exactly at the limit is accepted

- GIVEN a WF export whose `Parameters` text is exactly 1000 characters
- WHEN it is imported
- THEN the file is accepted — the check is an over-length rejection, not an off-by-one
- PINNED BY `WalkForwardExportParserTests.ParseAsync_ParametersExactlyAtTheLimit_IsAccepted`

#### Scenario: Over-length file name rejects the file

- GIVEN a WF export uploaded under a file name longer than 260 characters
- WHEN it is imported
- THEN the file is rejected whole, naming the length and the limit, before any window is persisted
- PINNED BY `WalkForwardExportParserTests.ParseAsync_OverLengthFileName_RejectsTheWholeFile`

### Requirement: The Parameters Field Inverts Punctuation Roles

Inside `Parameters` ONLY, commas MUST separate `key=value` pairs and dots MUST
be the decimal point within a value — the inverse of every other column in
this file. The comma-decimal rule used elsewhere in this file MUST NOT be
applied here. A trailing comma MUST be dropped (one fewer pair), not treated
as an error.

#### Scenario: Trap guard — comma-decimal would destroy this field

- GIVEN row 1's `Parameters` = `"TEMAPeriod1=32,ProfitTargetCoef1=5.4,StopLossCoef1=2.05,TrailingStopCoef1=2.91,EMAPeriod1=110,"`
- WHEN parsed
- THEN exactly 5 pairs result, including `ProfitTargetCoef1 = 5.4` as one token — applying the file's comma-decimal rule would instead split it into `ProfitTargetCoef1=5` and a stray `4`

### Requirement: The Future Window Is Recognized By Two Signals And Excluded From Every Aggregate

The LAST row is the un-elapsed window when its four OOS columns (`Net profit
(OOS)`, `Ret/DD Ratio (OOS)`, `Drawdown (OOS)`, `Avg. Trades Per Month
(OOS)`) are the literal string `N/A` AND its `Period OOS` carries a `
(future)` suffix. `N/A` MUST parse to `null` and set `IsFutureWindow = true`
— NEVER to `0`. Every derived aggregate over WF windows MUST exclude the
future window. The two signals disagreeing (one present without the other)
MUST reject the file. `N/A` on any non-last row MUST reject the file, naming
the row.

#### Scenario: Fixture's row 7 is the future window

- GIVEN row 7's 4 OOS columns are `"N/A"`, `Period OOS` ends `" (future)"`, `Days OOS = 381`, and all 4 IS columns are populated
- WHEN imported
- THEN `IsFutureWindow = true`, the 4 OOS fields are persisted as `null`

#### Scenario: N/A-as-zero would corrupt the worst-window read (must-fail guard)

- GIVEN the 5 elapsed windows' OOS Ret/DD values are 2.06, 1.16, 0.96, 0.52, 1.27
- WHEN the minimum OOS Ret/DD across all windows is computed
- THEN it is 0.52 (the elapsed minimum) — if the future row's `N/A` were parsed as `0`, the minimum would incorrectly be `0`

#### Scenario: Disagreeing signals reject the file

- GIVEN a row with the `(future)` suffix but fully-populated OOS numbers, or the reverse
- WHEN imported
- THEN rejected as "future-window signal mismatch"

#### Scenario: N/A on a non-last row rejects the file

- GIVEN any row before the last one contains `"N/A"`
- WHEN imported
- THEN rejected naming that row

### Requirement: OosFromDate Is Owned By The WF Export, Never Copied Onto A Run

`OosFromDate` MUST be persisted only on `StrategyWalkForwardExport`, computed
as the OOS-start date of the SECOND-TO-LAST row (the elapsed window
immediately before the future one). `DeployParameters` and
`EvaluationParameters` MUST be persisted verbatim from the LAST and
SECOND-TO-LAST rows' `Parameters` text, respectively, as the manual audit
trail against a run's declared `Kind`. A WF export with fewer than 2 rows
MUST be rejected. `OosFromDate` MUST NEVER be written onto any `BacktestRun`
or `BacktestTrade`.

#### Scenario: OosFromDate computed from the second-to-last row

- GIVEN row 6's `Period OOS` = `"26.05.2025 - 12.06.2026"`
- WHEN the fixture is imported
- THEN `OosFromDate = 2025-05-26`, `EvaluationParameters` = row 6's `Parameters` text, `DeployParameters` = row 7's `Parameters` text

#### Scenario: Single-row export is rejected

- GIVEN a WF export with only 1 data row
- WHEN imported
- THEN rejected: "at least 2 windows required"

### Requirement: A Deploy Run's OOS Window Is Underivable, Not Empty

Obtaining a run's OOS boundary MUST go through exactly one function that
returns "none" (not an empty range) when ANY of three conditions holds: `Kind
!= Evaluation`; the strategy has no WF export; or the export handed in belongs
to a DIFFERENT strategy than the run. It returns `OosFromDate` only when `Kind
== Evaluation` AND a WF export exists AND that export's `StrategyId` equals the
run's. No other code path MUST compute an OOS trade subset by any other means
(e.g. an ad hoc `CloseTime >=` filter); that containment is a single-file
convention checked by grep, not a structural guarantee (see `design.md` D8).

(Revision 3 correction: the ownership condition is new. It was previously stated
in terms of Kind and export presence only, and the tests paired two independent
identifiers — certifying "an unrelated strategy's boundary is valid for this run"
as the contract. A mismatched pair yields a date produced by a different
parameter set than the one that produced these trades.)

**Delivery status — this API is built and tested, but NOT YET WIRED.** The
per-run entry point and its trade-filtering operations have ZERO production
callers at this revision. The only wired consumer of the boundary is the grid's
readiness aggregate (see `account-strategies`), which correlates on `StrategyId`
and applies the boundary comparison inside the same single file, so the
single-source containment above genuinely holds. This requirement is retained,
not deleted: `design.md` designates the simulator-engine slice as its consumer,
and the boundary type exists precisely so that slice cannot fabricate an
out-of-sample claim. Scenarios marked NOT WIRED below are pinned at domain level
only; no shipped user-facing feature produces their result.

#### Scenario: Deploy run's OOS window is "none", even with a WF export present

- GIVEN strategy `S1` has a Deploy run and a WF export
- WHEN the Deploy run's OOS window is requested
- THEN the result is "none" — not an empty date range, not a zero-trade set computed by filtering
- PINNED BY `OosWindowResolverTests.TryGetOosWindow_DeployRunWithAnExportPresent_YieldsNoWindowAtAll`; `WalkForwardImportServiceTests.DeployRunPlusExport_IsNotEvaluableEvenThoughBothExist`

#### Scenario: An export owned by another strategy yields no window

- GIVEN an Evaluation run belonging to `S1` and a WF export belonging to `S2`
- WHEN the run's OOS window is requested with that export
- THEN the result is "none" — a boundary from a different parameter set is not a boundary for these trades
- PINNED BY `OosWindowResolverTests.TryGetOosWindow_ExportOwnedByADifferentStrategy_YieldsNoWindow`

#### Scenario: Evaluation run's OOS trades once the export exists (NOT WIRED)

- GIVEN `S1`'s Evaluation run and `S1`'s own WF export (`OosFromDate = 2025-05-26`)
- WHEN the OOS trade set is requested through the per-run boundary API
- THEN it is every trade with `CloseTime >= 2025-05-26`, the boundary date itself included
- PINNED BY `OosWindowResolverTests.OosWindow_Filter_ReturnsOnlyTradesAtOrAfterTheBoundary`; `.OosWindow_Includes_IsInclusiveOfTheBoundaryItself`; `.TryGetOosWindow_EvaluationRunWithAnExport_YieldsTheExportsBoundary`
- NOT WIRED — domain-level only; no production caller produces this set at this revision

### Requirement: A Run Imported Before Its WF Export Stays Valid, Turns Green Later Without Re-Import

Importing a Deploy or Evaluation run for a strategy with no WF export yet
MUST succeed, storing all trades. The grid marker (see `account-strategies`)
MUST reflect the missing boundary honestly (not green) until a WF export is
later imported for the same strategy, at which point the marker updates with
ZERO re-import of the run.

#### Scenario: Evaluation run precedes its WF export

- GIVEN `S1` has no WF export
- WHEN `ListOfTrades_XAUUSD_H1_IST.csv` is imported to `S1`'s Evaluation slot
- THEN 329 trades persist, the OOS window request returns "none", and the marker stays amber
- WHEN the WF export is later imported for `S1`
- THEN the marker turns green, and the Evaluation run's `BacktestTrade` rows are not re-written

### Requirement: A WF Export Imported With No Run Yet

Importing a WF export for a strategy with zero runs MUST succeed, persisting
the windows and `OosFromDate`. The grid marker stays white/amber since no run
exists to evaluate.

#### Scenario: WF export alone

- GIVEN `S1` has no Deploy or Evaluation run
- WHEN `WFParamsExport_XAUUSD_H1.csv` is imported for `S1`
- THEN the export and 6 windows persist, `OosFromDate = 2025-05-26`, and the marker reports no evaluable run

### Requirement: A Trade-List File Posted To The WF-Export Slot Is Rejected (Detectable Case)

The WF-export header (13 columns incl. `Parameters`) is structurally distinct
from the trade-list's 16-column header and MUST be validated. A file whose
header does not match MUST be rejected at `POST
/api/strategies/{id}/walk-forward`, naming the mismatch, before any window is
persisted.

#### Scenario: Trade-list file posted to the WF-export endpoint

- WHEN `ListOfTrades_XAUUSD_H1_IST.csv` is posted to `POST /api/strategies/S1/walk-forward`
- THEN rejected with "expected walk-forward-export header, found a different column shape", zero windows persisted
