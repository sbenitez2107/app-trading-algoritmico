# Archive Report: backtest-run-platform-attribution

**Archived**: 2026-09-22
**Archived to**: `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/`
**Verification**: PASS, no findings. Backend 723 tests passing, frontend 409 tests across 33 files,
`dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean.
**Shipped as**: two chained PRs on `feature/backtest-run-platform-attribution` — C1 backend
(`46da237`), C2 Angular (`f32bc24`). Working tree clean at `f32bc24`.

## Tasks

All 59 implementation tasks in `tasks.md` are checked `[x]`, plus a five-entry "Flagged" section
documenting re-scoping/correction decisions made during planning and apply. No stale unchecked
tasks. Task Completion Gate satisfied without reconciliation.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `sqx-backtest-import` | Updated (merge) | 8 ADDED requirements appended verbatim to the existing main spec; one new `## Non-Goals (Platform Attribution, slice C)` section added at file end. No existing requirement in the main spec was edited, removed, or renamed — this was a purely additive merge, matching the delta's own framing ("No existing requirement in the main spec is edited; every requirement below is additive"). |

The delta spec (`specs/sqx-backtest-import/spec.md` inside the change folder) was NOT a full spec —
it was an `## ADDED Requirements` delta against the already-shipped `sqx-backtest-import` capability
— so it was merged into `openspec/specs/sqx-backtest-import/spec.md` rather than copied as a new
capability directory. The main spec's existing register (revision-note preamble, `### Requirement:`
/ `#### Scenario:` heading structure, blockquote rationale paragraphs) was matched exactly; the 8
new requirements were appended after the last existing requirement ("A WF Export File Posted To A
Trade-List Slot Is Rejected") in the same order they appear in the delta.

One rewording during merge: the delta's own "Non-Goals" bullet for the Angular surface used
strikethrough/"superseded" language appropriate to a change-in-flight ("~~Any Angular surface~~
superseded — ... is now in scope as a chained PR (C2)"). Since the archive reflects a shipped
capability, not an in-progress delta, that bullet was reworded to present tense ("The import modal
selector, the backtests-list column above, and their i18n keys are in scope for this capability
(shipped as chained PR C2)"). No substantive content was dropped — the scope boundary (selector +
list column only, not filtering/sorting/exporting by platform, not `account-form.component.ts:58`)
is preserved verbatim in the following clause.

## Content Verified to Survive the Merge

1. **Backfill is a USER-SUPPLIED FACT, not a code-chosen default.** Landed verbatim in
   `openspec/specs/sqx-backtest-import/spec.md`, under `### Requirement: The Pre-Existing Row
   Backfill Is A Point-In-Time, User-Supplied Historical Fact`:
   > "On 2026-09-21 the user asserted that every `BacktestRun` loaded to that date came from
   > MT4-era work; ... `.agents/knowledge/imox/INDEX.md` §5 forbids the **system** inventing a
   > domain datum — it does not forbid **recording** one the user holds and supplies."

2. **Backfill is POINT-IN-TIME.** Same requirement, main body:
   > "a row created after the migration runs MUST NOT inherit it and MUST remain `null` until a
   > caller declares a platform for it."
   And its second scenario: "A row created after the migration does not inherit the backfill."

3. **The `0`/CLR-default coincidence is a coincidence, not a justification.** Same requirement's
   rationale blockquote, preserved verbatim:
   > "That the backfilled value and `PlatformType`'s CLR default coincide at `0` is a coincidence,
   > not a justification: the rows are backfilled to MT4 because the user says the files were MT4,
   > and if MT4 were `= 7` the backfill would write `7`. This MUST NOT later be read as licence to
   > make `SourcePlatform` non-nullable."

4. **Deriving platform from `TradingAccount.Platform` is a REJECTED alternative**, with reason.
   Landed as its own requirement, `### Requirement: Deriving The Platform From The Strategy's
   Trading Account Is Rejected`:
   > "`TradingAccount.Platform` records the account's **live deployment** platform, not the SQX
   > **build** target a backtest was produced for, and `Strategy.TradingAccountId` is nullable — a
   > backtest legitimately exists before any deployment."

5. **The falsy-zero hazard and its three-layer guard.** Recorded at the property-nullability
   requirement (`### Requirement: SourcePlatform Is Nullable, Caller-Declared, And Never Derived`):
   > "`PlatformType.MT4 = 0` is the CLR default for the enum. A non-nullable `SourcePlatform` would
   > therefore make every row nobody declared a platform for silently assert 'this came from MT4' —
   > the same optimistic-enum-zero hazard this codebase's `CostDecompositionStatus.cs` and
   > `EmbeddedCostAvailability.cs` name as one it has 'already been bitten by twice.'"
   The three concrete guard layers (nullable CLR field, service `!== undefined`, template
   `!== null`) live in `design.md` (preserved unmodified in the archived folder) and in the tasks
   that pin them (task 8.1–8.4 for the service guard, task 11.4/10.4 for the template/undefined
   forwarding), and the list-display requirement's rationale repeats the same hazard in the view
   layer: "This is the same enum-zero hazard the property's nullability (see above) exists to
   avoid, now surfacing in the view layer."

6. **This change does NOT correct reconciliation.** Landed in the new `## Non-Goals (Platform
   Attribution, slice C)` section of the main spec, verbatim from the delta:
   > "Adds provenance only. **Does not correct** the MT4/MT5 commission-timing difference recorded
   > at `.agents/knowledge/imox/SERVICE_Darwinex_Zero.md:521-525`: MT4 books its round-trip cost
   > 100% at entry, MT5 books it 50/50 across entry and exit. This capability records which
   > platform produced a run; it does not adjust any figure for the difference."
   Also restated in `openspec/SIMULATOR_ROADMAP.md`'s updated layer-0 row (see below).

## Accepted Debt / Known Gaps Recorded as Deliberate

All carried into the archived `## Non-Goals` section and the "Flagged" section of `tasks.md`
(preserved unmodified in the archive folder):

- Migration `Down` drops the column and the backfill with it — deliberate, re-assertable via `Up`.
- `?sourcePlatform=bogus` (unparseable string) is untested — direct controller instantiation
  bypasses ASP.NET model binding, same limitation as the existing `GetComparability` precedent.
  The drivable case (an undeclared numeral, `(PlatformType)7`, via `Enum.IsDefined`) is tested.
  Recorded in `tasks.md`'s Flagged item 1.
- `account-form.component.ts:58`'s `0 as PlatformType` default is unchanged — out of scope,
  explicitly named as a live instance of the same defect class in both the delta's Non-Goals and
  now the merged main spec's Non-Goals.
- `StrategyWalkForwardExport` has the same platform blind spot, untouched — carried into the
  merged Non-Goals verbatim.
- design.md's prose incorrectly names `BacktestRunSummaryDto` as the list column's source;
  `tasks.md` Flagged item 2 and the merged spec's list-display requirement both carry the
  correction: `backtests-list.component.ts` actually binds `BacktestRunDto`, and
  `BacktestRunSummaryDto` has no component consumer. The correction is preserved; design.md itself
  (in the archived folder) is left as originally written — the archive does not retroactively edit
  design documents, only the record of what was corrected.

## Outstanding Item — Migration Not Yet Applied

**The migration (`AddSourcePlatformToBacktestRun`) has never been applied to any database.**
`dotnet ef database update` was never run, and verification confirmed no `Database.Migrate()` or
`EnsureCreated()` call exists anywhere in production code, so nothing applies it automatically at
startup. Applying it is an outstanding, user-authorised step — this archive does NOT represent the
column as present in any live database. Recorded explicitly in `openspec/SIMULATOR_ROADMAP.md`'s
updated layer-0 row: "**Outstanding**: the slice C migration has not been applied to any database;
`dotnet ef database update` is a pending, user-authorised step."

## Roadmap Updated

`openspec/SIMULATOR_ROADMAP.md`, layer-0 table row: marked slice C (`backtest-run-platform-
attribution`) shipped alongside slices A and B, noted it lands ahead of the October 2026 SQX v144 /
MT5 migration it was built for, restated the non-correction of commission-timing, and recorded the
outstanding pending migration application.

## Files Created / Modified / Moved

**Created**:
- `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/archive-report.md` (this file)

**Modified**:
- `openspec/specs/sqx-backtest-import/spec.md` — merged 8 ADDED requirements + Non-Goals section
- `openspec/SIMULATOR_ROADMAP.md` — layer-0 row updated to mark slice C shipped

**Moved** (via `git mv`, renames — not copies, source folder no longer exists):
- `openspec/changes/backtest-run-platform-attribution/design.md` → `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/design.md`
- `openspec/changes/backtest-run-platform-attribution/explore.md` → `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/explore.md`
- `openspec/changes/backtest-run-platform-attribution/proposal.md` → `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/proposal.md`
- `openspec/changes/backtest-run-platform-attribution/specs/sqx-backtest-import/spec.md` → `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/specs/sqx-backtest-import/spec.md`
- `openspec/changes/backtest-run-platform-attribution/tasks.md` → `openspec/changes/archive/2026-09-22-backtest-run-platform-attribution/tasks.md`

Verified: `test ! -e "openspec/changes/backtest-run-platform-attribution"` → "source gone: OK".
`git status --short` shows `R` (rename) entries for all five moved files, not new-file/deleted-file
pairs, confirming byte-identical archival.

## Nothing Dropped

All six load-bearing items above, all named accepted debt, and the outstanding-migration item were
carried forward. No source artifact was rewritten into a stub, pointer, or placeholder.

### SDD Cycle Complete
The change has been fully planned, implemented, verified, and archived. The one open follow-up —
applying the migration to a real database — is explicitly a user-authorised step outside this SDD
cycle's scope, not a defect in this archive.
