# Tasks: Typed Funding Guardrails (loss-limits vs var-target)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~950 total / ~430 production (excl. migration designer, tests) |
| 400-line budget risk | Medium-High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 Domain+DB → PR2 Estimator → PR3 Service+Contract → PR4 Frontend |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium-High

**Resolved for apply**: single PR, `size:exception` recorded and approved by the user (~950 total / ~430 production lines). Not split into chained PRs.

Threat matrix: N/A (no auth/permissions/payments/data-loss/shell/chokepoint). Dominant lens: `review-reliability` (migration + branch-by-kind regression risk).

### Suggested Work Units

| Unit | Goal | PR | Focused test | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | `GuardrailKind` + entity fields + EF config + migration | PR1 | `dotnet test --filter BrokerRiskLimits` | `dotnet ef database update` (manage-db skill) | Revert migration + entity/config; nothing else depends on it yet |
| 2 | `RollingWindowSums` + monthly VaR estimator | PR2 | `dotnet test --filter PortfolioAnalyticsCalculatorTests` | N/A — pure calculator, no I/O | Revert `AnalyticsSeries.cs`/`PortfolioAnalyticsCalculator.cs` hunks |
| 3 | DTOs + `RiskLimitsService` validation + `GetRiskAsync` branch-by-kind | PR3 | `dotnet test --filter RiskLimitsServiceTests\|PortfolioServiceRiskTests` | `GET /api/portfolios/{id}/risk` against a seeded VarTarget broker | Revert DTO/service diff; PR1 columns stay valid (nullable) |
| 4 | FE types + extracted `RiskLimitsModalComponent` + `@switch` card | PR4 | `pnpm exec ng test --include risk-limits-modal.component.spec.ts --watch=false`; same for `portfolio-detail.component.spec.ts` | Manual: switch broker FTMO↔Darwinex Zero in modal, check field set/card | Revert `risk-limits-modal/` + `portfolio-detail` diff; PR3 contract is additive |

## Phase 1: Domain, Persistence, Migration (PR1)

- [x] 1.1 RED: entity test asserting new/existing `BrokerRiskLimits` rows default `Kind=LossLimits`, `TargetVarPct`/`VarFloorPct` nullable.
- [x] 1.2 GREEN: create `Domain/Enums/GuardrailKind.cs` (`LossLimits=0`, `VarTarget=1`).
- [x] 1.3 GREEN: modify `Domain/Entities/BrokerRiskLimits.cs` — add `Kind`, nullable `TargetVarPct`/`VarFloorPct`. No horizon field.
- [x] 1.4 GREEN: modify `BrokerRiskLimitsConfiguration.cs` — `Kind` `HasConversion<int>()`; var pcts `HasPrecision(9,6)`; `Broker` index unchanged.
- [x] 1.5 Build, then `dotnet ef migrations add AddGuardrailKind` (manage-db skill); verify DDL is exactly 3 columns, `Kind DEFAULT 0`.
- [x] 1.6 Test 1.1 green (`dotnet test`, 2/2 passed). `dotnet ef database update` against local dev DB: **BLOCKED** — the environment's auto-mode classifier denied the direct DB-connecting CLI command. Migration file is created, verified, and DDL-correct (3 columns, `Kind DEFAULT 0`); it has not yet been applied to the local `AppTA` SQL Server database. Run `dotnet ef database update --project src/AppTradingAlgoritmico.Infrastructure --startup-project src/AppTradingAlgoritmico.WebAPI` manually (or re-run with elevated permission) before relying on the new columns existing in the dev DB.
- [x] 1.7 Regression: existing rows keep `Kind=LossLimits`, identical values — covered via the Phase 3 golden test (`PortfolioServiceRiskTests.GetRiskAsync_LossLimitsGuardrail_GoldenRegression_HeadroomAndBreachUnchanged`), as explicitly permitted by this task's "or via Phase 3 golden test" clause.

## Phase 2: Rolling-Window Estimator (PR2)

- [x] 2.1 RED: `PortfolioAnalyticsCalculatorTests.cs` — window sums on known series; `n<90` → `InsufficientHistory`; p05 matches fixture; zero-filled days don't distort sums; `OverlappingWindows`/`IndependentWindows` on n=250.
- [x] 2.2 GREEN: add `RollingWindowSums(series, windowLength)` to `AnalyticsSeries.cs` + `MonthlyVarHorizonDays=30`/`MinHistoryDays=90` constants (code-only, not persisted).
- [x] 2.3 GREEN: `PortfolioAnalyticsCalculator.cs` (`:250-266`, `:394-407`) — `MonthlyVar95 = -Percentile(sort(RollingWindowSums(series,30)),0.05)` no √t, `MonthlyVar95Percent = /initialCapital`, `OverlappingWindows=n-H+1`, `IndependentWindows=n/H`. Unconditional per service, guardrail-agnostic.
- [x] 2.4 All Phase 2 tests pass (15/15 in `PortfolioAnalyticsCalculatorTests`); calculator has no `BrokerRiskLimits`/DB dependency.

## Phase 3: DTO Contract, Validation, Service Wiring (PR3)

- [x] 3.1 RED: new `RiskLimitsServiceTests.cs` — reject var fields on LossLimits payload; reject loss fields on VarTarget payload; reject `VarFloorPct>TargetVarPct`; reject pct outside `(0,1]`; accept valid pair.
- [x] 3.2 RED: golden test (`PortfolioServiceRiskTests.cs`) — existing LossLimits broker output byte-identical pre/post; VarTarget never emits breach/headroom; VarTarget under 90 days → `InsufficientHistory`, no multiplier.
- [x] 3.3 GREEN: `BrokerRiskLimitsDto.cs` — add `Kind`, `TargetVarPct`, `VarFloorPct` to read/upsert records, no horizon.
- [x] 3.4 GREEN: `PortfolioAnalyticsDto.cs` (`:137-159`) — `ServiceRiskDto` gains monthly-VaR fields; `ServiceGuardrailDto` gains `Kind` + nullable `VarTarget`; add `VarTargetReadoutDto` per design (incl. derived `HorizonDays`).
- [x] 3.5 GREEN: `RiskLimitsService.cs` (`:27-53`) — kind-aware validation per 3.1, throws `ArgumentException` (controller already maps 400).
- [x] 3.6 GREEN: `PortfolioService.cs` (`:374-390`) — branch `GetRiskAsync` by `Kind`: LossLimits keeps headroom/breach; VarTarget emits band position + implied multiplier + `InsufficientHistory` gating, no breach/headroom.
- [x] 3.7 All Phase 3 tests pass (12/12 across `RiskLimitsServiceTests` + `PortfolioServiceRiskTests`), including 3.2 golden regression.

## Phase 4: Frontend Types, Extracted Modal, Card Rendering (PR4)

- [x] 4.1 RED: extend `portfolio-detail.component.spec.ts` — no breach/headroom for VarTarget; insufficient-history state; capital-base $ label; disclaimer shown adjacent to estimate.
- [x] 4.2 RED: new `risk-limits-modal.component.spec.ts` — LossLimits fields for non-Darwinex, VarTarget fields for Darwinex Zero; client-side validators mirror 3.1.
- [x] 4.3 GREEN: `core/services/portfolio.service.ts` (`:160-216`) — `GuardrailKind` enum + discriminated-union types matching 3.4.
- [x] 4.4 GREEN: create `features/portfolios/risk-limits-modal/` — extract inline modal (`portfolio-detail.component.html:421-500`), typed reactive form, `OnPush`, `input()`/`output()`, per `advance-stage-modal` convention.
- [x] 4.5 GREEN: `portfolio-detail.component.ts/.html` — drop inline modal/`limitsForm`, wire new component, `@switch (g.kind)` on card: VarTarget shows band, multiplier, 3 D-Leverage caps + unresolved-cap note, capital label, disclaimer; no breach/headroom.
- [x] 4.6 4.1/4.2 pass via `pnpm exec ng test --include <spec> --watch=false` (17/17 passed across both spec files; direct Vitest CLI was not used — the builder injects TestBed as expected).

## Phase 5: Cross-Cutting Verification

- [x] 5.1 `dotnet test` (full backend suite) — no regressions. **214/214 passed.**
- [x] 5.2 `pnpm exec ng test --watch=false` (full frontend suite) — no regressions. **212/212 passed across 24 test files** (was 205/205 before Phase 6's 7 added tests).
- [x] 5.3 Verify applied migration DDL matches design exactly (3 columns, no horizon, `Broker` index untouched) — confirmed by reading the generated migration file. **Note**: the migration has NOT been applied to the local dev DB yet (see 1.6 — blocked by the environment's DB-connection classifier). DDL correctness is verified from the generated C# migration source, not from an applied database.
- [x] 5.4 Confirm no new `assets/i18n/{en,es}.json` keys added (Spanish stays hardcoded per design) — confirmed via `git status`, no i18n files touched.

## Phase 6: Verify Remediation — Test Gaps (corrective batch, tests only)

`sdd-verify` returned FAIL with 2 CRITICAL findings, both test gaps against already-correct production code (no production code changed in this phase).

- [x] 6.1 `varBandLabel()` had zero test invocations (portfolio-monthly-var: "Band Position Against Floor and Target"). Added 6 unit tests directly against the component method (pure logic — no DOM needed): below floor, exactly at floor, within band, exactly at target, above target, null/insufficient-estimate. **Boundary note**: the spec text ("below floor / within band / above target") does not state which side owns the floor/target value itself. The existing implementation treats both endpoints as inclusive to "within band" (only strict `<` floor is "below", only strict `>` target is "above") — tests assert this actual, already-shipped behavior rather than inventing a different rule. Flagged as spec-silent, not decided unilaterally.
- [x] 6.2 "Implied Multiplier with D-Leverage Context" scenario (funding-guardrails) had zero covering tests. Added 1 component test (`portfolio-detail.component.spec.ts`, DOM-rendered — the caps/note text lives in the template) asserting: multiplier value (`10.83x`), all three D-Leverage caps (`16.25`, `13x`, `9.75x`), and the "no puede resolver ... duración de posición" note.
- [x] 6.3 Proved both new tests are load-bearing: temporarily broke one assertion per test (wrong expected string), ran the focused spec, confirmed RED (2 failed), then restored the correct assertions and confirmed GREEN (17/17 in the focused file).
- [x] 6.4 Re-ran full frontend suite (212/212, 24 files) and full backend suite (214/214) — no regressions. Static gates: `dotnet build` clean, `dotnet format --verify-no-changes` clean, `pnpm exec tsc --noEmit` clean, `pnpm exec prettier --write` applied to the one touched file (was previously unformatted after the edits; now clean).

**No production code was touched in Phase 6** — both gaps were closeable with tests alone, per the corrective batch's scope restriction.
