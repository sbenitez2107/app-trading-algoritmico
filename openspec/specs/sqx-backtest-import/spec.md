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

### Requirement: `SourcePlatform` Is Nullable, Caller-Declared, And Never Derived

`BacktestRun` MUST carry a `PlatformType? SourcePlatform` property. The property MUST remain
nullable at every layer — the CLR type, the EF Core configuration, and the database column — and
MUST NOT be given a `HasDefaultValue`, a non-nullable backing type, or any application-side
fallback that substitutes a platform when none was declared. A freshly constructed `BacktestRun`
MUST report `SourcePlatform` as `null`.

> `PlatformType.MT4 = 0` is the CLR default for the enum. A non-nullable `SourcePlatform` would
> therefore make every row nobody declared a platform for silently assert "this came from MT4" —
> the same optimistic-enum-zero hazard this codebase's `CostDecompositionStatus.cs` and
> `EmbeddedCostAvailability.cs` name as one it has "already been bitten by twice." Nullable is the
> only shape that makes an undeclared row visibly undeclared rather than plausibly wrong.

#### Scenario: A freshly constructed run has no platform, not MT4
- GIVEN a new `BacktestRun` instantiated without setting `SourcePlatform`
- WHEN it is inspected
- THEN `SourcePlatform` is `null`, never `PlatformType.MT4`

#### Scenario: Making the property non-nullable, or giving it a database default, fails this requirement
- GIVEN a hypothetical future change that makes `SourcePlatform` non-nullable or configures
  `HasDefaultValue` on it
- WHEN this requirement is checked
- THEN the change violates it, because either shape resurrects the enum-zero hazard this
  requirement exists to prevent

### Requirement: Import Accepts An Optional Declared Platform; Omission Records Unknown, Never A Platform

`POST /api/strategies/{strategyId}/backtests/{kind}` MUST accept an optional `sourcePlatform`
query parameter of type `PlatformType?`. When supplied and parseable, the declared value MUST be
recorded on the created or replaced run. When the parameter is omitted, the request MUST succeed
and the run's `SourcePlatform` MUST be recorded as `null` — never defaulted to `MT4` or any other
member. When supplied but unparseable, the request MUST be rejected with `400` before the file is
opened or any row parsed, mirroring the existing unparseable-`{kind}` guard on the same endpoint.

> A query parameter, not a route segment: `kind` selects the slot being written; `sourcePlatform`
> describes an attribute of the payload, not the resource's identity, and a route segment would
> both misstate its role and break the existing URL. It is optional, not required, because forcing
> a declaration on a legacy file whose origin the user does not recall would force a guess — the
> same fabrication this requirement exists to avoid in the other direction.

#### Scenario: A declared platform is recorded on a new run
- GIVEN strategy `S1` has no Deploy run
- WHEN a file is posted to `POST /api/strategies/S1/backtests/deploy?sourcePlatform=MT5`
- THEN the created run's `SourcePlatform` is `MT5`

#### Scenario: An omitted platform records null, not a default
- GIVEN strategy `S1` has no Evaluation run
- WHEN a file is posted to `POST /api/strategies/S1/backtests/evaluation` with no `sourcePlatform`
  query parameter
- THEN the request succeeds and the created run's `SourcePlatform` is `null`

#### Scenario: An unparseable platform value is rejected before the file is opened
- WHEN a file is posted to `POST /api/strategies/S1/backtests/deploy?sourcePlatform=bogus`
- THEN the response is `400`, the file is never opened, and no `BacktestRun` is created or changed

### Requirement: Deriving The Platform From The Strategy's Trading Account Is Rejected

The system MUST NOT read, join, or otherwise derive `SourcePlatform` from
`Strategy.TradingAccountId → TradingAccount.Platform` at import time or at read time. The recorded
platform MUST come only from the caller's explicit declaration at import.

> `TradingAccount.Platform` records the account's **live deployment** platform, not the SQX
> **build** target a backtest was produced for, and `Strategy.TradingAccountId` is nullable — a
> backtest legitimately exists before any deployment. Either fact alone makes the field unreliable
> as a source of truth; it may be offered as a UI pre-fill suggestion in a later change, but it
> MUST NOT become the value actually recorded.

#### Scenario: An unrelated TradingAccount platform is not consulted
- GIVEN a strategy whose `TradingAccountId` points to a `TradingAccount` with `Platform = MT5`
- WHEN a backtest is imported for that strategy without declaring `sourcePlatform`
- THEN the created run's `SourcePlatform` is `null`, not `MT5`

### Requirement: Replacing A Run's Bytes Overwrites Its Recorded Platform Unconditionally, Including With Null

When an import replaces an occupied slot's content (different `ContentHash`), the write MUST set
`SourcePlatform` to whatever the current request declares — including `null` when the request
omits it — regardless of what the slot previously held.

> `ReplaceAsync` already re-derives `ContentHash`, `SourceFileName`, and `Symbol` from the
> incoming request rather than preserving the old values. Carrying a previously recorded platform
> across a replacement would assert "the new file came from the same platform as the old one" —
> exactly the class of inference the trading-account rejection above forbids, applied to the run's
> own history instead of another table.

#### Scenario: A replacement with a new declared platform overwrites the old one
- GIVEN `S1`'s Deploy slot holds a run with `SourcePlatform = MT4`
- WHEN a file with different bytes is imported to the same slot with `?sourcePlatform=MT5`
- THEN the outcome is `Replaced` and the run's `SourcePlatform` is `MT5`

#### Scenario: A replacement omitting the platform nulls the previously recorded one
- GIVEN `S1`'s Deploy slot holds a run with `SourcePlatform = MT4`
- WHEN a file with different bytes is imported to the same slot with no `sourcePlatform` declared
- THEN the outcome is `Replaced` and the run's `SourcePlatform` is `null`

### Requirement: The Unchanged Outcome Writes Nothing, Including The Platform Column

When an import lands on a slot whose `ContentHash` is identical to what is already stored, the
outcome MUST be `Unchanged` and the write path MUST NOT execute — the previously recorded
`SourcePlatform` (or its absence) MUST remain exactly as it was, even when the request declares a
platform.

> This no-write path is what `BacktestImportRetrySafetyTests` rests on: a transient failure after
> a commit that actually landed re-enters `PersistOneFileAsync`, reads committed state, and must
> settle without writing a second time. Adding a platform write to the `Unchanged` branch would
> break that property for the sake of a field that, by definition, describes bytes that have not
> changed.

#### Scenario: Identical bytes with a supplied platform still take the no-write path
- GIVEN `S1`'s Deploy slot holds a run with `SourcePlatform = null`, imported from bytes with hash `H`
- WHEN the identical bytes are re-imported to the same slot with `?sourcePlatform=MT5`
- THEN the outcome is `Unchanged`, no write occurs, and the run's `SourcePlatform` remains `null`

### Requirement: The Pre-Existing Row Backfill Is A Point-In-Time, User-Supplied Historical Fact

One migration MUST add the nullable `SourcePlatform` column and, in the same migration, backfill
every row where `SourcePlatform IS NULL` at the moment the migration runs to `MT4`. The
migration's `migrationBuilder.Sql` MUST carry a comment naming who asserted the fact, the date of
the assertion, and that it is a user-supplied historical fact rather than a derived or defaulted
value. No `HasDefaultValue`, database default constraint, or application-side fallback MUST exist
anywhere as a consequence of this backfill; a row created after the migration runs MUST NOT
inherit it and MUST remain `null` until a caller declares a platform for it.

> On 2026-09-21 the user asserted that every `BacktestRun` loaded to that date came from MT4-era
> work; `SIMULATOR_ROADMAP.md` independently records "the existing 123 strategies on SBDEMO2 are
> MT4." `.agents/knowledge/imox/INDEX.md` §5 forbids the **system** inventing a domain datum — it
> does not forbid **recording** one the user holds and supplies. A bare `UPDATE BacktestRuns SET
> SourcePlatform = 0` is indistinguishable from the fabrication that rule exists to prevent, so the
> comment naming the asserter, the date, and the claim's nature is part of this requirement, not
> decoration. Contrast the precedent at
> `Persistence/Migrations/20260426190244_AddInitialBalanceToTradingAccount.cs`, whose backfill
> states a **project default** ($100,000) — a weaker claim than a historical fact, so its comment
> style is not sufficient here on its own.
>
> That the backfilled value and `PlatformType`'s CLR default coincide at `0` is a coincidence, not
> a justification: the rows are backfilled to MT4 because the user says the files were MT4, and if
> MT4 were `= 7` the backfill would write `7`. This MUST NOT later be read as licence to make
> `SourcePlatform` non-nullable.

#### Scenario: Every pre-existing row is backfilled with its provenance stated in the migration
- GIVEN a `BacktestRun` row that existed with `SourcePlatform = null` before the migration runs
- WHEN the migration is applied
- THEN the row's `SourcePlatform` is `MT4`, and the migration source contains a comment naming the
  asserter, the assertion date (2026-09-21), and that the value is a user-supplied historical fact

#### Scenario: A row created after the migration does not inherit the backfill
- GIVEN the migration has already run
- WHEN a new `BacktestRun` is created afterward without a declared platform
- THEN its `SourcePlatform` is `null`, not `MT4`, and no default at any layer supplies a value

### Requirement: Both Backtest Read DTOs Expose The Source Platform Verbatim

`BacktestRunDto` and `BacktestRunSummaryDto` (`DTOs/Backtests/StrategyBacktestsDto.cs`) MUST each
gain a `SourcePlatform` field, and both of `BacktestReadService`'s projections that produce these
DTOs (the paged list and `GetByStrategyAsync`) MUST project the underlying column verbatim. A
`null` value MUST pass through as `null` in both DTOs and MUST NOT be rendered, mapped, or
serialized as `MT4` or any other member.

#### Scenario: Both DTOs carry the value verbatim
- GIVEN a `BacktestRun` with `SourcePlatform = MT5`
- WHEN it is projected into `BacktestRunDto` and into `BacktestRunSummaryDto`
- THEN both DTOs report `SourcePlatform = MT5`

#### Scenario: A null platform stays null in both DTOs
- GIVEN a `BacktestRun` with `SourcePlatform = null`
- WHEN it is projected into `BacktestRunDto` and into `BacktestRunSummaryDto`
- THEN both DTOs report `SourcePlatform = null`, never `MT4`

### Requirement: The Backtests List Displays The Recorded Source Platform Verbatim, Including A Visible Not-Declared State

The backtests list MUST render each run's recorded `SourcePlatform` value, sourced from
`BacktestRunDto.sourcePlatform`, without inference, fallback, or derivation. A recorded
`MT4` or `MT5` value MUST render as that platform's label. A `null` value (not declared at import)
MUST render as a visibly distinct **not-declared** state — never blank, never a dash or other
placeholder that could be misread as a declared platform, and never defaulted to a platform name.

> **`BacktestRunDto` is the correct source, and an earlier draft of this requirement named the wrong
> one.** `backtests-list.component.ts` renders `BacktestRunDto[]` obtained from `getRuns()`;
> `BacktestRunSummaryDto` has **no component consumer at all** and is referenced only inside
> `backtest.service.ts` (verified by repo-wide search). Both DTOs still carry the field, per the
> dual-DTO requirement above — but binding the column to the summary DTO would render nothing.

> Until this requirement, the import surface let the user *declare* a platform but no surface
> *displayed* it, so a misdeclaration was undetectable — the user could not tell an unrecorded run
> from a wrongly-recorded one. From October 2026 the user imports from both MT4 and MT5; being
> unable to read back what was actually stored defeats this capability's purpose, which is to
> record provenance, not merely to accept it. This is the same enum-zero hazard the property's
> nullability (see above) exists to avoid, now surfacing in the view layer: rendering a `null` run
> indistinguishably from an MT4 run would silently reintroduce the fabrication this capability
> exists to prevent.

#### Scenario: A run recorded as MT4 renders as MT4
- GIVEN a `BacktestRunSummaryDto` with `sourcePlatform = MT4`
- WHEN it is rendered in the backtests list
- THEN the row displays the MT4 label

#### Scenario: A run recorded as MT5 renders as MT5
- GIVEN a `BacktestRunSummaryDto` with `sourcePlatform = MT5`
- WHEN it is rendered in the backtests list
- THEN the row displays the MT5 label

#### Scenario: A run with no recorded platform renders as visibly not declared
- GIVEN a `BacktestRunSummaryDto` with `sourcePlatform = null`
- WHEN it is rendered in the backtests list
- THEN the row displays a distinct "not declared" label — never blank, never a dash, and never the
  MT4 label or any other platform name

## Non-Goals (Platform Attribution, slice C)

> Adds provenance only. **Does not correct** the MT4/MT5 commission-timing difference recorded at
> `.agents/knowledge/imox/SERVICE_Darwinex_Zero.md:521-525`: MT4 books its round-trip cost 100% at
> entry, MT5 books it 50/50 across entry and exit. This capability records which platform produced
> a run; it does not adjust any figure for the difference. That correction is a separate, larger
> change gated on this field existing.

- **A richer availability enum** (`NotDeclared` / `Unknown` / `MT4` / `MT5`), on the
  `EmbeddedCostAvailability` precedent. Deferred: nothing reads `SourcePlatform` to branch behavior
  today. The trigger that would justify it is named — the moment a consumer must distinguish
  "recorded before the field existed" from "genuinely unknown," most plausibly the commission-
  timing correction above.
- **An edit path for an already-imported run's platform.** No PATCH endpoint exists; the Unchanged
  no-write path (above) means a slot already holding the right bytes cannot be relabelled without
  re-importing different bytes.
- The import modal selector, the backtests-list column above, and their i18n keys are in scope for
  this capability (shipped as chained PR C2). What remains out of scope in Angular is any surface
  beyond the selector and the list column — e.g. filtering, sorting, or exporting by platform — and
  the unrelated `account-form.component.ts:58` defect (listed separately below).
- **`StrategyWalkForwardExport`.** Same blind spot as the live `account-form.component.ts:58`
  zero-default hazard; neither is touched by this change.
- Slice A's `RunIdQuery` and slice B's `RunQuery` narrow projections (naming only `r.Id` and, for
  slice B, `r.Symbol`) are **verified undisturbed** by this new column and MUST NOT be defensively
  extended to include it.
