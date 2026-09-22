# Delta for SQX Backtest Import — Platform Attribution (Slice C)

> Adds provenance only. **Does not correct** the MT4/MT5 commission-timing difference recorded at
> `.agents/knowledge/imox/SERVICE_Darwinex_Zero.md:521-525` — see Non-Goals. Builds on the shipped
> `sqx-backtest-import` capability; extends `BacktestRun`, its import endpoint, its two read DTOs,
> and the backtests-list display only. No existing requirement in the main spec is edited — every
> requirement below is additive.

## ADDED Requirements

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

## Non-Goals (recorded, not designed for)

- **The MT4/MT5 commission-timing correction.** Verified verbatim at
  `.agents/knowledge/imox/SERVICE_Darwinex_Zero.md:521-525`: MT4 books its round-trip cost 100% at
  entry, MT5 books it 50/50 across entry and exit. This capability records which platform produced
  a run; it does not adjust any figure for the difference. That correction is a separate, larger
  change gated on this field existing.
- **A richer availability enum** (`NotDeclared` / `Unknown` / `MT4` / `MT5`), on the
  `EmbeddedCostAvailability` precedent. Deferred: nothing reads `SourcePlatform` to branch behavior
  today. The trigger that would justify it is named — the moment a consumer must distinguish
  "recorded before the field existed" from "genuinely unknown," most plausibly the commission-
  timing correction above.
- **An edit path for an already-imported run's platform.** No PATCH endpoint exists; the Unchanged
  no-write path (above) means a slot already holding the right bytes cannot be relabelled without
  re-importing different bytes.
- ~~Any Angular surface~~ **superseded** — the import modal selector, the backtests-list column
  above, and their i18n keys are now in scope as a chained PR (C2), per design.md D6–D7. What
  remains out of scope in Angular is any surface beyond the selector and the list column — e.g.
  filtering, sorting, or exporting by platform — and the unrelated `account-form.component.ts:58`
  defect (listed separately below).
- **`StrategyWalkForwardExport`.** Same blind spot as the live `account-form.component.ts:58`
  zero-default hazard; neither is touched by this change.
- Slice A's `RunIdQuery` and slice B's `RunQuery` narrow projections (naming only `r.Id` and, for
  slice B, `r.Symbol`) are **verified undisturbed** by this new column and MUST NOT be defensively
  extended to include it.
