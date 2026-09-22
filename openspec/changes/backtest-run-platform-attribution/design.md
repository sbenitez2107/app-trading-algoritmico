# Design: BacktestRun platform attribution (slice C of layer 0)

> Two chained PRs: **C1** backend, **C2** Angular (platform selector + backtests-list column). Both
> target `feature/backtest-run-platform-attribution`; that branch merges to `main` once (D5).
> Settled decisions 1–10 from the proposal are inputs here, not re-litigated.

## Technical Approach

One nullable enum column on `BacktestRun`, caller-declared at import, plumbed controller →
`IBacktestImportService` → the two write branches. One migration adds the column and backfills a
**user-supplied historical fact** whose provenance is carried in the SQL itself. No calculator, no
new endpoint, no feature flag, no consumer branching on the value.

## Architecture Decisions

### D1 — Transport: `[FromQuery] PlatformType? sourcePlatform`, plus an explicit `Enum.IsDefined` guard

**Choice**: optional query parameter on the existing `POST api/strategies/{strategyId}/backtests/{kind}`,
refused with 400 when bound to an undeclared member.

**Validated against the code** — the proposal's D5 reasoning holds, with one correction:

| Proposal claim | Verdict |
|---|---|
| `kind` is a route segment because it selects the SLOT | **Holds.** `StrategyBacktestsController:34-43` says exactly that, and adds the URL/access-log visibility rationale. |
| Precedent exists: `[FromQuery] BacktestRunKind?` on the GETs | **Holds.** `GetComparability:113` and `GetCostDecomposition:137`. Note the precedent is about *binding style*, not optionality — both are required-with-explicit-check. |
| A form field is invisible in logs and has no precedent here | **Holds**, and is the controller's own stated reason for rejecting a form field for `kind`. |
| "an unparseable `sourcePlatform` → 400" is unit-testable | **WRONG.** Tests instantiate the controller directly (`StrategyBacktestsControllerTests:31`), so model binding never runs — the same limitation `GetComparability`'s XML doc already documents. An unparseable string cannot reach the action. |

**Alternative considered**: a form field alongside the file (the request is already multipart).
**Rejected** — the value would vanish from access logs, contradicting the rationale the controller
already records for `kind`, and no form field other than `file` exists on this surface.

**The real hazard the proposal missed**: ASP.NET binds `?sourcePlatform=7` to `(PlatformType)7`
with **no** model-state error (enum conversion uses `Enum.Parse`, which accepts any numeral). That
is an undeclared member persisted as provenance. The controller therefore gets a guard mirroring
`TryParseKind`'s intent, which is also what makes the contract drivable by a direct-instantiation test:

```csharp
if (sourcePlatform is not null && !Enum.IsDefined(sourcePlatform.Value))
    return BadRequest(new { message = $"Unknown source platform '{(int)sourcePlatform.Value}'. Expected MT4 or MT5." });
```

### D2 — Migration mechanics; provenance lives in the SQL, not only in C#

Fluent (`BacktestRunConfiguration`, after the `Kind` line):

```csharp
// Caller-declared provenance. NO IsRequired, NO HasDefaultValue, NO index — deliberately.
// Null means "not declared at import" and must stay distinguishable from MT4 (= 0).
// BacktestSchemaTests pins this; the comment documents it, the test enforces it.
builder.Property(x => x.SourcePlatform);
```

Migration `AddSourcePlatformToBacktestRun`:

```csharp
internal const string BackfillSql = """
    -- PROVENANCE, NOT A DEFAULT. On 2026-09-21 Sebastian Benitez asserted, from his own SQX
    -- build history, that every BacktestRun loaded up to that date came from MT4-era work.
    -- This is a USER-SUPPLIED HISTORICAL FACT recorded once — not derived, inferred or chosen
    -- by the system. The WHERE clause is point-in-time and load-bearing: rows created after
    -- this migration stay NULL until a caller declares a platform. There is no DB default and
    -- no HasDefaultValue anywhere. That the written 0 equals PlatformType's CLR default is a
    -- COINCIDENCE: 0 is MT4 because the user says the files were MT4; if MT4 were 7, this
    -- would write 7.
    UPDATE BacktestRuns SET SourcePlatform = 0 WHERE SourcePlatform IS NULL;
    """;

protected override void Up(MigrationBuilder b)
{
    b.AddColumn<int>(name: "SourcePlatform", table: "BacktestRuns", type: "int", nullable: true);
    b.Sql(BackfillSql);   // no defaultValue:, no defaultValueSql: on the AddColumn — on purpose
}

protected override void Down(MigrationBuilder b)
    => b.DropColumn(name: "SourcePlatform", table: "BacktestRuns");
```

Two deliberate departures from the `20260426190244_AddInitialBalanceToTradingAccount` precedent:
the provenance is a **SQL** comment (so `dotnet ef migrations script` carries it to whoever applies
it, not just to a C# reader), and the literal is an `internal const` so a test can pin it —
`InternalsVisibleTo(AppTradingAlgoritmico.UnitTests)` is already declared on Infrastructure.

### D3 — `Down` drops the column and the assertion with it. Acceptable.

The assertion has nowhere to live without the column, and a `Down` that tried to preserve it
(a side table, a log row) would outlive the schema that gives it meaning. **It is acceptable
because it is recoverable**: A1 is a fact the user still holds in September 2026 and can re-assert,
and re-running `Up` reproduces it exactly. The irreversibility that would matter — losing the
ability to distinguish MT4 from MT5 rows *after* the October population arrives — is not created by
`Down`; it is created by not shipping. That window is the reason for the schedule, not for a
reversible `Down`.

### D4 — Service threading

`ImportTradeListAsync(strategyId, kind, file, sourcePlatform, ct)` — new parameter before `ct`,
`PlatformType?`, no default value on the interface (a C# optional parameter would let a caller omit
it by accident, which is the silent-null hazard in a different coat). Threaded verbatim through
`ImportOneFileAsync` → `PersistOneFileAsync` → `CreateNewRunAsync` / `ReplaceAsync`. `ReplaceAsync`
assigns unconditionally (`run.SourcePlatform = sourcePlatform;`) next to the existing `ContentHash` /
`SourceFileName` / `Symbol` assignments. The `Unchanged` branch (`BacktestImportService:167-173`)
gains **zero** statements; `BacktestImportRetrySafetyTests` must stay byte-identical.

### D5 — The PR seam, and what makes C1 honest on its own

C1 is coherent and independently deployable: schema, backfill, API capability and read exposure all
land together, and a curl/Swagger caller has the full capability. Its partialness is the one the
orchestrator named — **every modal import records null, invisibly**, because C1 ships no UI.

Handled by **chaining the branches rather than the releases**: C1 targets
`feature/backtest-run-platform-attribution`, C2 targets C1's branch, and the feature branch reaches
`main` only once both merge. The seam is a *review* seam (two reviewable units, backend and
frontend, with different reviewers and different test runners), not a *release* seam, so the silent-null
window never exists in production. This is the Feature Branch Chain the shared review guard describes.

**Fallback if C1 is released alone** (deadline pressure): the release note must state that
UI imports record no platform until C2, and A2 must be re-confirmed before the migration runs. A
release note is weak disclosure — it is the fallback, not the plan.

### D6 — C2: one modal-level, optional, never-MT4-by-default selector, plus a verbatim list-column display

| Question | Decision | Why |
|---|---|---|
| Where | One `<select>` in `import-strategy-backtests-modal.component.html`, in `__body` after `__intro`, **above** the `@for` slot list | Both trade lists in one submission come from one SQX build. Two controls ask the same question twice and invite an inconsistent answer nothing downstream can use. |
| Scope | Applies to `deploy` and `evaluation` only; **not** `walkForward` | A walk-forward export is `StrategyWalkForwardExport`, which has no such column (proposal Q4). |
| Required? | **Optional**, mirroring D5's API contract | Requiring it forces a guess on legacy files — fabrication in a different coat. |
| Default | `signal<PlatformType \| null>(null)`; the first `<option value="">` is a visible **"Not declared"** | The hazard is `0 as PlatformType` at `account-form.component.ts:58`. `null` cannot be mistaken for MT4. |
| Disclosure | When a trade-list slot holds a file and the platform is null, a `role="status"` note renders: *"These runs will be recorded without a platform."* Submit is **not** blocked | This is the "deliberately visible null" — the user cannot be unaware they left it undeclared. |
| List column | A new column in `backtests-list.component.html` rendering `PLATFORM_LABELS[run.sourcePlatform]` when set, or `SOURCE_PLATFORM_NOT_DECLARED` when `null` | Declaring without a way to verify what was actually recorded is the same hazard in the other direction (proposal D8, spec.md's added display requirement) — this is what resolves proposal Q5. |
| List rendering rule | **Verbatim only**: no inference, no fallback to `TradingAccount.Platform`, no derivation; `null` never renders blank or dashed | Same hazard as D1/D9's nullable choice, now in the view layer. |

The list column reuses `SOURCE_PLATFORM_LABEL` as its header text and `PLATFORM_LABELS` /
`SOURCE_PLATFORM_NOT_DECLARED` for cell values — no new i18n keys are needed beyond the six below.

Service (`core/services/backtest.service.ts`):

```ts
importDeploy(strategyId: string, file: File, sourcePlatform?: PlatformType): Observable<BacktestImportResultDto>
// postFile gains an optional HttpParams. Appended ONLY when `!== undefined` — never `if (sourcePlatform)`,
// because PlatformType.MT4 === 0 is falsy and would silently drop an explicit MT4 declaration.
```

This is the same rule `getGroupRisk` already documents for `segment=0` (`backtest.service.ts:443-445`) —
an existing in-repo precedent, not a new convention.

i18n, both `public/assets/i18n/en.json` and `es.json` under `SQX.BACKTESTS` (Dual-Entry Protocol):

| Key | EN | ES |
|---|---|---|
| `SOURCE_PLATFORM_LABEL` | Source platform | Plataforma de origen |
| `SOURCE_PLATFORM_HINT` | Which platform produced these trade lists. Recorded as provenance and never inferred — an undeclared run stays undeclared. | Qué plataforma produjo estas listas de operaciones. Se registra como procedencia y nunca se infiere: una ejecución sin declarar queda sin declarar. |
| `SOURCE_PLATFORM_NOT_DECLARED` | Not declared | Sin declarar |
| `SOURCE_PLATFORM_MT4` / `_MT5` | MT4 / MT5 | MT4 / MT5 |
| `SOURCE_PLATFORM_UNDECLARED_NOTE` | These runs will be recorded without a platform. | Estas ejecuciones se registrarán sin plataforma. |

Exposed as `PLATFORM_LABELS: Record<PlatformType, string>`, mirroring the existing `BACKTEST_KIND_LABELS`.

### D7 — The web app has no `PlatformType` enum. C2 creates one.

**Refutation of the exploration**: `trading-account.service.ts:7` declares
`export type PlatformType = 0 | 1;` — a union alias, not an enum. That is *why* line 58 has to write
`0 as PlatformType`: the alias gives no way to name MT4. **Choice**: C2 adds
`app/core/models/platform-type.model.ts` with `export enum PlatformType { MT4 = 0, MT5 = 1 }` and
turns the alias into a one-line re-export. `account-form.component.ts:58` is **not** fixed (R8 stands);
the enum only makes its wrongness legible. **Fallback if the re-export produces compile fallout beyond
one or two call sites**: declare the enum locally in `backtest.service.ts` and move de-duplication into
the R8 follow-up. **Rejected**: a second, differently-named type (`BacktestSourcePlatform`) — one
domain concept, two names, is worse than the alias.

### D8 — No `TradingAccount.Platform` pre-fill in C2

R1 permits it as a *suggestion*. A pre-filled `<select>` is indistinguishable from a user choice at
submit time, which re-creates the exact hazard. Honest pre-fill needs a distinct "suggested — confirm"
state. **Deferred, trigger named**: the first time the user complains about re-declaring the platform
on every import.

## Data Flow

    modal select (null | MT4 | MT5)
        └─> BacktestService.importDeploy/importEvaluation(id, file, sourcePlatform?)
              └─ HttpParams: appended only when !== undefined
                  └─> POST .../backtests/{kind}[?sourcePlatform=0|1]
                        └─ controller: Enum.IsDefined guard -> 400 | pass through
                            └─> IBacktestImportService.ImportTradeListAsync(..., sourcePlatform, ct)
                                  ├─ new slot  -> CreateNewRunAsync  : SourcePlatform = value (may be null)
                                  ├─ same bytes-> Unchanged          : NO WRITE (untouched)
                                  └─ new bytes -> ReplaceAsync       : SourcePlatform = value, unconditionally
                                        └─> BacktestRuns.SourcePlatform (int NULL)
                                              └─> BacktestRunDto / BacktestRunSummaryDto (verbatim, null stays null)
                                                    └─> backtests-list.component (verbatim; null -> visible not-declared label)

## File Changes

| File | PR | Action | Description |
|---|---|---|---|
| `Domain/Entities/BacktestRun.cs` | C1 | Modify | `PlatformType? SourcePlatform` + XML remarks: caller-declared, never derived; null = not declared |
| `Infrastructure/Persistence/Configurations/BacktestRunConfiguration.cs` | C1 | Modify | Explicit no-default `Property` line + comment (D2) |
| `Infrastructure/Persistence/Migrations/*_AddSourcePlatformToBacktestRun.cs` | C1 | Create | Column + `internal const BackfillSql` (D2/D3) |
| `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` + `.Designer.cs` | C1 | Modify | EF-generated |
| `Application/Interfaces/IBacktestImportService.cs` | C1 | Modify | One parameter, no C# default (D4) |
| `Infrastructure/Services/BacktestImportService.cs` | C1 | Modify | Thread through 4 methods; `Unchanged` branch untouched |
| `Application/DTOs/Backtests/BacktestRunDto.cs`, `StrategyBacktestsDto.cs` | C1 | Modify | One field each |
| `Infrastructure/Services/BacktestReadService.cs` | C1 | Modify | Two projections (`:34`, `:83`) |
| `WebAPI/Controllers/StrategyBacktestsController.cs` | C1 | Modify | `[FromQuery]` param + `Enum.IsDefined` guard + XML doc (D1) |
| `tests/.../Backtests/BacktestSchemaTests.cs` | C1 | Modify | Contract tests (D9) |
| `tests/.../Backtests/BacktestImportServiceTests.cs`, `StrategyBacktestsControllerTests.cs` | C1 | Modify | Import + controller cases |
| `tests/.../Backtests/BacktestMigrationProvenanceTests.cs` | C1 | Create | Pins the backfill SQL (D9) |
| `app/core/models/platform-type.model.ts` | C2 | Create | `enum PlatformType { MT4 = 0, MT5 = 1 }` (D7) |
| `app/core/services/trading-account.service.ts` | C2 | Modify | Alias → re-export (D7) |
| `app/core/services/backtest.service.ts` (+ `.spec.ts`) | C2 | Modify | Param, `HttpParams`, `PLATFORM_LABELS`, DTO fields |
| `...import-strategy-backtests-modal.component.{ts,html,scss,spec.ts}` | C2 | Modify | Selector, signal, undeclared note (D6) |
| `...backtests-list.component.{ts,html,scss,spec.ts}` | C2 | Modify | New column; verbatim rendering + visible not-declared state (D6) |
| `public/assets/i18n/{en,es}.json` | C2 | Modify | Six keys each |

Slice A's `RunIdQuery` and slice B's `RunQuery` are narrow `.Select()` projections and are not disturbed.

## Interfaces / Contracts

```csharp
public PlatformType? SourcePlatform { get; set; }   // null = not declared at import

Task<BacktestImportResultDto> ImportTradeListAsync(
    Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file,
    PlatformType? sourcePlatform, CancellationToken ct);
```

```ts
export interface BacktestRunSummaryDto { /* … */ sourcePlatform: PlatformType | null; }
export interface BacktestRunDto        { /* … */ sourcePlatform: PlatformType | null; }
```

## Testing Strategy (strict TDD — every case RED first)

`dotnet test` (C1) / `pnpm test` (C2). No integration harness
(`config.yaml: integration_tool_installed: false`) — controller cases are direct-instantiation unit
tests, which is what makes D1's explicit guard necessary rather than merely tidy.

### D9 — The contract test that pins nullability

Three assertions, in `BacktestSchemaTests` (same `BacktestTestDbContext` + `db.Model.FindEntityType`
idiom as `BacktestRun_ContentHashIndex_IsNotUnique:371`), designed so each failure mode is caught by
a *different* one:

```csharp
[Fact] public void BacktestRun_SourcePlatform_IsNullableWithNoDefaultAnywhere()
{
    // Guards the proposal's highest-likelihood risk: a future reader treating the 2026-09-21
    // backfill as a code-chosen default and "tidying" the field to non-nullable. It is not a
    // default; it is one user's assertion about rows that already existed.

    // 1. CLR shape — fails if someone writes `public PlatformType SourcePlatform`.
    typeof(BacktestRun).GetProperty(nameof(BacktestRun.SourcePlatform))!
        .PropertyType.Should().Be(typeof(PlatformType?));

    // 2. A freshly constructed run is UNDECLARED, not MT4. Pins D4's coincidence directly.
    new BacktestRun { SourceFileName = "f.csv", ContentHash = "h" }
        .SourcePlatform.Should().BeNull();

    // 3. EF model — fails if someone adds HasDefaultValue(0) / HasDefaultValueSql / IsRequired.
    using var db = new BacktestTestDbContext(_options);
    var p = db.Model.FindEntityType(typeof(BacktestRun))!.FindProperty(nameof(BacktestRun.SourcePlatform))!;
    p.IsNullable.Should().BeTrue();
    p.GetDefaultValue().Should().BeNull();
    p.GetDefaultValueSql().Should().BeNull();
}
```

Assertion 1 is the load-bearing one: making the property non-nullable while leaving assertion 3
green is the exact failure mode the risk describes, and 1 catches it. Assertion 2 additionally
fails to *compile* under FluentAssertions' `EnumAssertions` if the property loses its nullability —
a compile break is a louder RED than a failing assert, and that is deliberate.

| Group | Pins | PR |
|---|---|---|
| Contract | D9 above | C1 |
| Migration provenance | `BackfillSql` contains `WHERE SourcePlatform IS NULL`, the asserter's name, `2026-09-21`, and the phrase `USER-SUPPLIED`; contains neither `DEFAULT` nor `defaultValue` | C1 |
| Import — new run | MT5→MT5; MT4→MT4; **omitted→null, not MT4** | C1 |
| Import — replace | supplied overwrites; **omitted nulls a previously recorded value** (D6 of the proposal) | C1 |
| Import — unchanged | identical bytes take the no-write path with a platform supplied; `BacktestImportRetrySafetyTests` byte-identical | C1 |
| Controller | absent → 200 + `null` forwarded; `MT4`/`MT5` → forwarded verbatim; **`(PlatformType)7` → 400 and the service is never called**; the guard runs *before* `OpenReadStream` | C1 |
| Read | both DTOs carry the value verbatim; a null stays null | C1 |
| Service (C2) | MT4 sends `?sourcePlatform=0` (the falsy-zero trap); MT5 sends `1`; undefined appends nothing | C2 |
| Component (C2) | default is `null` and no request carries `sourcePlatform` until the user chooses; choosing MT4 sends `0`; the undeclared note renders when a trade-list file is queued with no platform; walk-forward never receives the param | C2 |
| List (C2) | MT4/MT5 recorded → verbatim label rendered; `null` → distinct not-declared label rendered, never blank, dashed, or defaulted to a platform name | C2 |

**Known breakage in C2**: `import-strategy-backtests-modal.component.spec.ts:126` asserts
`toHaveBeenCalledWith(STRATEGY_ID, expect.any(File))`. A third argument changes the arity and the
assertion fails — it must be updated to expect the explicit `undefined`, which is itself the RED
that proves the undeclared path sends nothing.

## Threat Matrix

N/A — no new route template, no shell command, subprocess, VCS/PR automation, or executable-file
classification. The single untrusted-input surface introduced is the new query parameter, and its
out-of-range-enum case is covered by D1's guard and its RED test; the existing extension whitelist
and `Path.GetFileName` sanitisation are untouched.

## Migration / Rollout

One migration (D2/D3). Deploy order: migrate, then the API — an added nullable column makes an
un-migrated API against the new schema harmless and a migrated schema against the old API harmless.
**Before applying**, re-confirm A2 (no MT5 run already imported); the backfill would silently
mislabel one. Rollback: revert the migration and the modified files; `Down` discards the backfill (D3).

## Sizing per PR

Both figures are **floors**. The last several slices overran by roughly 2×, and the overrun came
from the test surface — which here is ~75% of C1 and ~55% of C2 (list column included).

| PR | Production | Tests | Floor | Realistic (2×) | Budget risk |
|---|---|---|---|---|---|
| C1 backend | ~85 | ~310 | **~395** | 600–800 | `400-line budget risk: High` |
| C2 Angular (selector + list-column display) | ~135 + ~25 (list column) = ~160 | ~130 + ~60 (list column) = ~190 | **~350** | 525–700 | `400-line budget risk: High` |

`Decision needed before apply: Yes` — C1 sits on the 400 line at its floor. Splitting C1 further is
not honest (the proposal's "no seam" argument survives *within* the backend: the column without the
migration is undeployable, the plumbing without either writes nowhere). If C1's floor is exceeded,
that is a `size:exception` conversation, not a third slice. C2's risk moved from Medium to High once
the backtests-list column (previously deferred to a hypothetical C3, see Open Questions) was folded
into it; C2's other unbounded risk is D7's re-export fallout, which has a named fallback. If either
PR's floor is exceeded, that is a `size:exception` conversation, not a further split.

`Chained PRs recommended: Yes` — C1 → C2, feature-branch chain (D5).

## Open Questions

- [x] Q5 RESOLVED: the user requires verification within this change. C2 now includes the
      backtests-list column (D6) rather than deferring it to a hypothetical C3; sizing above
      reflects the added cost.
- [ ] A2 (no MT5 run already imported) is unverified — no DB access authorised. Must be confirmed
      before the migration is applied, not before it is written.
