# Proposal: BacktestRun platform attribution (slice C of layer 0)

> Named as deferred in slice B's Non-Goals. Built on `explore.md` in this folder. Delivered as two
> chained PRs — **C1** backend, **C2** Angular (platform selector + backtests-list column) — per
> design.md D5.
> **This slice records provenance. It does not correct any figure** — see Non-Goals.

## Intent

`SIMULATOR_ROADMAP.md` records that after the October 2026 mentoring the user builds all new
strategies for **MT5**, while the existing 123 strategies on SBDEMO2 stay **MT4** — *"plan for a
mixed MT4/MT5 population, not a clean cutover"*. The roadmap states the consequence itself: *"the
evidence layer must know which platform produced each run"*, and *"carry the platform on the
evidence from the start, so a mixed population is expressible on the day it appears rather than
retrofitted."*

The reason it matters is measured and already in the KB, verbatim at
`SERVICE_Darwinex_Zero.md:521-525`: **`MT4 => Roundtrip (100% applied at the entry)` versus
`MT5 => 50% applied at the entry and 50% at the exit`.** Same round-trip cost, different day. Every
daily-resolution measurement — the FTMO daily-loss simulation, the divergence decomposition — will
attribute cost to different days depending on the platform, and today a `BacktestRun` cannot say
which one produced it.

**Why now, before any MT5 file exists**: the field must exist *before* the mixed population arrives,
because the moment both kinds are loaded and indistinguishable, no later change can separate them —
the information is simply gone. The backfill is only possible while the user can still assert, from
their own build history, that every loaded run is MT4. That window closes in October.

## Scope

### In Scope

1. **`PlatformType? SourcePlatform` on `BacktestRun`** — nullable, caller-supplied, never derived.
2. **Import plumbing** — an optional `sourcePlatform` query parameter on
   `POST api/strategies/{id}/backtests/{kind}`, carried through `IBacktestImportService` into
   `CreateNewRunAsync` and `ReplaceAsync`.
3. **One migration** — add the nullable column, plus a point-in-time backfill to MT4 whose
   `migrationBuilder.Sql` comment records **who asserted it, on what date, and that it is a
   user-supplied historical fact, not a derived or defaulted value** (D2).
4. **Read exposure** — `SourcePlatform` added to `BacktestRunDto` and `BacktestRunSummaryDto` and to
   their two projections in `BacktestReadService`.
5. **Angular platform declaration and display (PR C2, chained onto C1)** — an optional platform
   selector in the import modal (defaults to not-declared, never falls back to MT4); a real
   `PlatformType` enum introduced in the web app, replacing the `0 | 1` union alias at
   `trading-account.service.ts:7`; EN/ES i18n keys; and **the recorded `SourcePlatform` rendered
   verbatim as a column in the backtests list**, so the user can verify what was actually stored.
   See design.md D6–D7 and D8 below.

### Out of Scope

- **The commission-timing correction.** See Non-Goals — this is the point most likely to be
  misread.
- **Any consumer reading `SourcePlatform` to branch behaviour.** Nothing does, by design (R3).
- **Deriving the platform from `TradingAccount.Platform`** — rejected, R1.
- **A richer availability enum** — deferred with a named trigger, R3.
- **An edit path for an already-imported run** — no PATCH endpoint (see Open questions Q2).
- ~~All Angular work~~ **superseded**: the import modal selector, the backtests-list column, and
  their i18n keys are now IN SCOPE as chained PR C2 (item 5 above; see D8 below and design.md
  D6–D7). What remains out of scope in Angular is any surface beyond the selector and the list
  column — e.g. filtering, sorting, or exporting by platform.
- **`account-form.component.ts:58`'s `0 as PlatformType` default** — a live instance of the same
  defect class in production UI, recorded as a separate follow-up and deliberately not folded in.

## Non-Goals (stated plainly, because this is the likely misreading)

**A platform column does not correct anything.** It does not fix the divergence decomposition, the
FTMO daily-loss simulation, or any future daily-resolution measurement over mixed-platform data. The
commission-timing correction is a **separate, larger change, gated on this field existing**. Slice C
makes the correction *possible*; it does not make it, and nothing shipped here changes a single
number an operator reads today.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `sqx-backtest-import`: the import accepts an optional caller-declared source platform and records
  it on the run; an omitted declaration is recorded as unknown, never as a platform. The backtests
  list additionally renders the recorded value verbatim, including a visibly distinct not-declared
  state (D8).

## Approach

One nullable enum column, plumbed from the controller to the two write branches, plus a migration
that adds the column and backfills a user-supplied historical fact with its provenance stated in
SQL. No calculator, no read service, no new endpoint, no feature flag.

### Architecture decisions

**D1 — Nullable, precisely because `MT4 = 0`.** A non-nullable `PlatformType` would make every row
that nobody declared assert *"this came from MT4"*, which is the optimistic-enum-zero hazard this
codebase records having *"already been bitten by twice"* (`CostDecompositionStatus.cs`,
`EmbeddedCostAvailability.cs`). Nullable makes an undeclared row **visibly undeclared**.

**D2 — The backfill is a user-supplied fact and the migration comment is load-bearing.** On
2026-09-21 the user asserted that every `BacktestRun` loaded to that date came from MT4-era work;
`SIMULATOR_ROADMAP.md` independently records *"The existing 123 strategies on SBDEMO2 are MT4."*
`INDEX.md` §5 forbids the **system inventing** a domain datum — not recording one the user holds and
supplies. A bare `UPDATE BacktestRuns SET SourcePlatform = 0` is **indistinguishable from the
fabrication D1 exists to prevent**, so the comment naming the asserter, the date and the nature of
the claim is part of the deliverable, not decoration. The precedent is
`20260426190244_AddInitialBalanceToTradingAccount.cs`, which states its $100,000 backfill as a
project default; this one states something stronger and must say so.

**D3 — The backfill is a point-in-time assertion, scoped to rows existing when the migration runs.**
`WHERE SourcePlatform IS NULL` at migration time, and **nothing else**: no DB default constraint, no
`HasDefaultValue`, no application-side fallback. Rows created afterwards MUST NOT inherit it — a new
run with no declaration stays null and visibly unset. This is the difference between *"we know these
were MT4"* and *"we assume anything undeclared is MT4"*, and only the first is true.

**D4 — That the backfill value and the CLR default coincide at `0` is a coincidence.** It is not a
justification, and it must not later be read as evidence that the field can safely become
non-nullable. The backfilled rows are MT4 because the user says the files were MT4; if MT4 were
`= 7` the backfill would write `7`.

**D5 — A query parameter, optional, on the existing route.** `kind` is a route segment because it
selects the **slot** — the resource being written. `sourcePlatform` is an attribute of the payload,
not part of the resource's identity, so a route segment would both misstate its role and break the
existing URL. A query parameter keeps the declaration visible in the URL and in access logs — the
stated benefit of the route-segment choice (`StrategyBacktestsController` XML docs) — without
touching the route template, and it matches the existing precedent of `[FromQuery] BacktestRunKind?`
on the two GET endpoints. It is **optional**, because requiring it would force a guess whenever the
user genuinely does not know a legacy file's origin, and a forced guess is the fabrication in a
different coat. **Omitted means null: "not declared at import."** That stays distinguishable from a
backfilled row, which is non-null MT4.

**D6 — `ReplaceAsync` overwrites unconditionally, including with null.** `ReplaceAsync` already
re-derives `ContentHash`, `SourceFileName` and `Symbol` from the incoming request; the slot's
contents are whatever this request says. Carrying a previously recorded platform across a
replacement would be the inference *"the new file came from the same platform as the old one"* —
exactly the class of inference R1 rejects. The recorded platform belongs to the bytes, and the bytes
changed.

**D7 — The `Unchanged` branch still writes nothing.** Identical bytes take the no-write path, and
that is load-bearing for retry idempotency (documented at `BacktestImportService.cs:167-173`).
Adding a write there to record a platform would break a property the retry safety rests on. The
consequence is Q2.

**D8 — Recording without displaying defeats the purpose; C2 must render `SourcePlatform` in the
backtests list.** Until this correction, C2 let the user *declare* a platform but no surface
*displayed* it, so a misdeclaration was undetectable — the user could not tell an unrecorded run
from a wrongly-recorded one. From October 2026 the user imports from both MT4 and MT5; being
unable to read back what was stored defeats this change's own purpose. The list column renders the
stored value **verbatim** — no inference, no fallback to `TradingAccount.Platform`, no derivation —
and a run with no recorded platform renders as visibly **not declared**: never blank, never dashed
in a way that reads as MT4, and never defaulted to a platform name. This is the same hazard D1
exists to avoid, now in the view layer.

### Rejected alternatives

| # | Rejected | Reason |
|---|---|---|
| **R1** | Deriving from `Strategy.TradingAccountId → TradingAccount.Platform` | The FK is **nullable** — a backtest legitimately exists before deployment — and even when populated it records the **live deployment** platform, not the **SQX build** target. It may be offered as a UI pre-fill suggestion later; it is never the source of truth. |
| R2 | Non-nullable `PlatformType SourcePlatform` | `MT4 = 0` means every unset row silently asserts MT4. See D1. |
| **R3** | An `EmbeddedCostAvailability`-style multi-state enum (`NotDeclared` / `Unknown` / `MT4` / `MT5`) | That four-state precedent exists because callers **branch on why a figure is missing**. Nothing reads `SourcePlatform` to branch behaviour today — it is descriptive provenance, not a computation input. **Deferred, with the trigger named**: the moment a consumer must distinguish *"recorded before the field existed"* from *"genuinely unknown"*, most plausibly the commission-timing correction. The backfill actually shrinks that ambiguity — after it, non-null means asserted and null means undeclared-at-import. |
| R4 | Leaving existing rows null | Loses a fact the user holds today and cannot reconstruct after the MT5 population arrives. |
| R5 | A DB default or `HasDefaultValue(0)` | Turns D2's point-in-time assertion into a standing assumption about every future row. See D3. |
| R6 | Parsing the platform from the filename or the CSV | Verified: the 16-column trade list encodes no platform and the filename is not parsed for attribution. There is nothing to parse. |
| R7 | A route segment (`backtests/{kind}/{platform}`) or a form field | See D5. The route segment misstates the value's role and breaks the existing URL; a form field is invisible in logs and has no precedent in this controller. |
| R8 | Fixing `account-form.component.ts:58` in this slice | Same defect class, different surface, different tests. Folding it in would make a provenance change also a UI change. Separate follow-up. |

## Affected modules by layer

| Layer | Path | Impact |
|---|---|---|
| Domain | `Domain/Entities/BacktestRun.cs` | Modified — one nullable property + XML remarks stating what null means |
| Domain | `Domain/Enums/PlatformType.cs` | **Unchanged** |
| Infrastructure | `Persistence/Configurations/BacktestRunConfiguration.cs` | Modified — one `Property`, no default, no index |
| Infrastructure | `Persistence/Migrations/*_AddSourcePlatformToBacktestRun.cs` | New — column + commented point-in-time backfill (D2/D3) |
| Infrastructure | `Services/BacktestImportService.cs` | Modified — signature, `CreateNewRunAsync`, `ReplaceAsync` (D6); `Unchanged` branch untouched (D7) |
| Infrastructure | `Services/BacktestReadService.cs` | Modified — two projections gain one field |
| Application | `Interfaces/IBacktestImportService.cs` | Modified — one parameter |
| Application | `DTOs/Backtests/BacktestRunDto.cs`, `DTOs/Backtests/StrategyBacktestsDto.cs` (`BacktestRunSummaryDto`) | Modified — one field each |
| WebAPI | `Controllers/StrategyBacktestsController.cs` | Modified — one optional `[FromQuery]` parameter |
| Angular (PR C2) | `app/core/models/platform-type.model.ts` | New — real `PlatformType` enum (D7) |
| Angular (PR C2) | `app/core/services/trading-account.service.ts` | Modified — union alias becomes a re-export |
| Angular (PR C2) | `app/core/services/backtest.service.ts` (+ `.spec.ts`) | Modified — param, `HttpParams`, `PLATFORM_LABELS` |
| Angular (PR C2) | `.../import-strategy-backtests-modal.component.{ts,html,scss,spec.ts}` | Modified — selector, i18n |
| Angular (PR C2) | `.../backtests-list.component.{ts,html,scss,spec.ts}` | Modified — new column, verbatim + not-declared rendering (D8) |
| Angular (PR C2) | `public/assets/i18n/{en,es}.json` | Modified — new i18n keys |

Slice A's `RunIdQuery` and slice B's `RunQuery` are explicit narrow projections naming only `r.Id`
(and `r.Symbol`) and are **not** disturbed.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| The backfill is later read as a code-chosen default and the field made non-nullable | **High** | D2's provenance comment in the SQL; D3's no-default rule; D4 stated explicitly; a test pinning that the property is nullable and that no `HasDefaultValue` is configured |
| Slice C read as fixing the reconciliation | **High** | The Non-Goals section exists for exactly this |
| A UI-driven import silently records null until C2 ships | **Certain**, but bounded to the review window | Mitigated by chaining C2 onto C1's branch rather than releasing C1 alone (design.md D5); the feature branch reaches `main` only once both merge |
| A future caller supplies the platform from `TradingAccount.Platform` | Medium | R1 recorded; XML remarks on the property state it is caller-declared, never derived |
| Sizing overrun | **High** | The last several slices overran by ≈2×; the figure below is a floor (see Sizing) |
| The user's 2026-09-21 assertion turns out partly wrong | Low | The backfill is one reversible `UPDATE`; `Down` drops the column entirely |

## Migration and rollback

**One migration.** `Up` adds the nullable column, then one commented `UPDATE … WHERE SourcePlatform
IS NULL`. `Down` drops the column — which also discards the backfill, correctly: the assertion has
nowhere to live without the column.

**Rollback**: revert the migration and the seven modified files. Every change is additive; no
existing read, write or projection changes behaviour when the parameter is omitted, so an unreverted
deploy of the API against a rolled-back schema is the only ordering hazard — standard for an added
column.

## Testing approach (strict TDD — every case RED first)

`dotnet test` from `app.trading.algoritmico.api`. Integration tooling is not installed
(`config.yaml: integration_tool_installed: false`), so controller and import cases are unit tests
over the existing mocked/InMemory harness, matching the current `BacktestImportService` tests.

| Group | Pins |
|---|---|
| Contract | `SourcePlatform` is `PlatformType?`; no `HasDefaultValue` on the property; the CLR default of a freshly constructed `BacktestRun` is **null, not MT4** |
| Import — new run | declared MT5 → stored MT5; declared MT4 → stored MT4; **omitted → stored null, not MT4** |
| Import — replace | a supplied value overwrites; **an omitted value nulls a previously recorded one** (D6) |
| Import — unchanged | identical bytes still take the no-write path with a platform supplied (D7 regression guard) |
| Controller | an absent `sourcePlatform` → 200 and a null column; `0` and `1` accepted as the two declared members only; an out-of-range numeral (e.g. `7`) is rejected by an explicit `Enum.IsDefined` guard → 400, before the file is opened. (The earlier "an unparseable `sourcePlatform` → 400" row is struck: `StrategyBacktestsControllerTests:31` instantiates the controller directly, so model binding never runs and that case is not unit-testable here — see design.md D1.) |
| Read | both DTOs carry the value verbatim; a null stays null and is never rendered as MT4 |

### Must not regress

Backend suite green, 0 warnings. `BacktestImportRetrySafetyTests` is the one to watch — D7 exists to
keep it byte-identical. Slice A and slice B suites are untouched.

## Sizing and slice recommendation

**Void as originally stated (~250–400 lines, one PR).** Angular is in scope (item 5 above), so a
single-PR, backend-only estimate no longer reflects the shipped shape. **The settled shape is two
chained PRs**, per design.md D5: **C1** (backend, In-Scope items 1–4) and **C2** (Angular, item 5 —
selector, real enum, i18n, and the backtests-list column). design.md's "Sizing per PR" table is the
authoritative figure; this document defers to it rather than re-deriving its own:

| PR | Production | Tests | Floor | Realistic (2×) | Budget risk |
|---|---|---|---|---|---|
| C1 backend | ~85 | ~310 | **~395** | 600–800 | `400-line budget risk: High` |
| C2 Angular (selector + list-column display) | ~160 | ~190 | **~350** | 525–700 | `400-line budget risk: High` |

**Treat every figure above as a floor, not an estimate.** The last several slices overran their
figures by roughly 2×, and nothing about this one is exempt. C2's floor moved from Medium to High
risk once the list-column display (D8) was folded into it — it was previously deferred to a
hypothetical C3.

**Two PRs, chained, not one.** Within C1 there is still no honest seam: the column without the
migration is undeployable, the migration without the column is meaningless, and the plumbing
without either writes nowhere — splitting C1 further would manufacture a boundary rather than
produce a reviewable unit. The C1/C2 seam is real: backend and frontend are independently
reviewable, tested by different runners, and chaining the branches (C2 targets C1's branch; the
feature branch reaches `main` once) removes the release-ordering hazard without forcing a single
oversized PR. If either PR's floor is exceeded, that is a `size:exception` conversation, not a
further split.

## Dependencies

- None blocking. `demo-backtest-comparability` and `demo-backtest-cost-decomposition` are shipped and
  unaffected.
- The backfill depends on the user's 2026-09-21 assertion (A1).
- **No database access is performed during this change's design or spec phases.**

## Success criteria

- [ ] A run imported with a declared platform records it; a run imported without one records null.
- [ ] After the migration, every pre-existing row reads MT4 and the migration source states who
      asserted that, when, and that it is a user-supplied fact.
- [ ] A newly created row does **not** inherit the backfill — no default exists at any layer
      (test-pinned).
- [ ] A freshly constructed `BacktestRun` has a null platform, never MT4 (test-pinned).
- [ ] Re-importing different bytes without a declaration clears the recorded platform (D6).
- [ ] The `Unchanged` no-write path is byte-identical; `BacktestImportRetrySafetyTests` unchanged.
- [ ] Backend suite green, 0 warnings; slice A and slice B suites byte-identical.
- [ ] The backtests list renders a run's recorded `SourcePlatform` verbatim; a run with no
      recorded platform renders as visibly not declared — never blank, never dashed, never
      defaulted to a platform name (D8).

## Proposal question round (blocked — needs a human)

The executor could not ask interactively. These shape the proposal and should be answered before
`sdd-spec`:

1. **Q1 — Is the Angular import modal in this slice or the next? RESOLVED: in this change, as
   chained PR C2.** The user decided the platform must be both declarable and *verifiable*
   (D8) — a declare-only UI makes a misdeclaration undetectable, which defeats this change's
   purpose once mixed MT4/MT5 imports begin in October. C1 (backend) and C2 (Angular) ship as two
   chained PRs on one feature branch (design.md D5), so the silent-null window never reaches
   production. Design-phase verification also found the original ~100–200 line estimate for "a
   platform selector" understated the work: **no `PlatformType` enum exists in the web app** —
   `trading-account.service.ts:7` declares a `0 | 1` union alias, which is the mechanism of the
   live `account-form.component.ts:58` defect — so C2 must also introduce a real enum (design.md
   D7). See the Sizing section above for the corrected, floor-only figures.
2. **Q2 — Is there a way to correct a run's platform without re-importing?** **Assumed no** — D7
   keeps the `Unchanged` branch write-free, so a slot already holding the right bytes cannot be
   relabelled. A small PATCH endpoint would close this, at the cost of a write path onto `BacktestRun`
   that does not exist today. Deferred unless the user wants it.
3. **Q3 — Should the import refuse when the platform is omitted?** **Assumed no** (D5). Requiring it
   would force a guess on legacy files whose origin the user may not recall.
4. **Q4 — Does the backfill assertion cover walk-forward exports too?** **Assumed no** — this slice
   touches `BacktestRun` only. `StrategyWalkForwardExport` has the same blind spot, and if the
   commission-timing correction ever reads it, a second slice applies.
5. **Q5 — Should `SourcePlatform` appear in the backtests list UI? RESOLVED: yes, in C2, not a
   later slice.** The field is unverifiable by the user if no surface exposes it — the same defect
   Q1's resolution addresses for the write side. See D8 and the added display requirement in
   `specs/sqx-backtest-import/spec.md`.

## Assumptions (flagged)

- **A1** — The user's 2026-09-21 assertion that every currently loaded `BacktestRun` came from
  MT4-era work. **This is the one fact in this change that the code cannot verify.** If it is wrong
  for some subset, the backfill mislabels those rows, and the only remedy is a second corrective
  `UPDATE` once the user identifies them.
- **A2** — No MT5 run has been imported yet. If one has, the backfill silently mislabels it, and A1
  must be re-confirmed before the migration runs.
- **A3** — Superseded: Q1 flipped. The estimate no longer assumes zero Angular work; the sizing and
  slice recommendation above are the corrected, two-chained-PR figures. Q2 (a PATCH endpoint) is
  still assumed "no" and remains out of scope.
