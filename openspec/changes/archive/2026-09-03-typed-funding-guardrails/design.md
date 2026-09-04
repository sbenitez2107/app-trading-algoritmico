# Design: Typed Funding Guardrails (loss-limits vs var-target)

## Technical Approach

`BrokerRiskLimits` becomes **discriminated by `GuardrailKind`** (`LossLimits = 0`, `VarTarget = 1`) on a single flat table. `LossLimits` behaviour is byte-identical to today. `VarTarget` (Darwinex Zero) carries a target/floor VaR band and gets a **30-calendar-day rolling-sum VaR estimator** with no breach or headroom semantics. The kind travels end-to-end so the frontend never guesses.

All four proposal open questions are closed by the user: denominator = portfolio initial capital (no `VarCapitalBase`); minimum history = 90 calendar days; `VarWindowDays` not stored; implied multiplier shown next to the D-Leverage caps.

## Architecture Decisions

### Decision: Flat entity + `Kind` column, not EF TPH inheritance

| Option | Tradeoff | Verdict |
|---|---|---|
| **Flat entity + `Kind` discriminator + nullable per-kind columns** | Weaker DB-level integrity (var columns are nullable for every row); kind correctness enforced at the service boundary | **Chosen** |
| EF TPH with derived CLR types | Genuinely typed domain, but **EF Core cannot change a tracked entity's type**. `UpsertAsync` is keyed on `Broker` and the modal lets the user switch funding service on an existing row, so a kind change would force delete + re-insert and mint a **new `Id`** that `BrokerRiskLimitsDto.Id` already exposes | Rejected |
| Owned type / `ToJson()` var block | **Zero precedent** in this codebase (no `OwnsOne`, `ToJson`, or check constraints anywhere in `src/`); loses the explicit `decimal(9,6)` precision the other pcts use | Rejected |

Rationale: the upsert path is the decisive constraint. The proposal recommended TPH; the mutable-kind upsert refutes it.

### Decision: Store only values that are BOTH honoured by the estimator AND variable per guardrail

Both conditions must hold. A value the estimator ignores is inert documentation; a value that may never legitimately differ between rows is speculative generality that makes an invalid state representable.

| Field | Stored | Honoured? | Variable per row? | Why |
|---|---|---|---|---|
| `TargetVarPct`, `VarFloorPct` | **Yes** | Yes | Yes | User-confirmed band (`Verified`), never hardcoded |
| `VarWindowDays` (45) | **No** | **No** | — | Vendor's open-position window; the app has no open-position risk series, so it could never drive the estimator |
| Horizon (30 days) | **No** | Yes | **No** | Vendor constant from the KB, not a user input. Two `var-target` rows with different horizons would produce VaR percentages that are no longer comparable against the shared 3.25–6.5% band — the entire point of the readout. A column that must never vary buys nothing |
| `VarCapitalBase` | **No** | — | — | Denominator is the portfolio's configured initial capital; the UI labels it explicitly (KB §5 trap 3) |

The two exclusions are **not the same failure**: 45 fails the first condition, 30 fails the second. The horizon lives as a named constant in the calculator, sourced from the KB document, so changing it is a code change (matching how the KB records vendor drift) rather than a data migration. A future VaR-based service with a different horizon is its own change with its own requirements.

> **Reconciliation note for `sdd-tasks`**: an earlier draft of this design stored `VarHorizonDays` and conflicted with the spec, which treated the horizon as documentation-only. Settled in favour of the spec — entity, migration and upsert DTOs carry **no horizon field**. Spec and design now agree.

### Decision: Estimator lives in the calculator, unconditionally

`PortfolioAnalyticsCalculator.ComputeVaR` computes monthly VaR for **every** service, not just `VarTarget` ones. The calculator is pure math with no DB access and must not learn about guardrails; `PortfolioService.GetRiskAsync` (the layer that already loads `BrokerRiskLimits`) decides what to surface per kind.

### Decision: Extract `RiskLimitsModalComponent` with a typed reactive form

The current modal is inline markup (`portfolio-detail.component.html:421-500`) over a plain `limitsForm` object with `ngModel` and no validators. **Reactive forms are the established convention** — 9 components use them, including two sibling modals (`sqx/workflow/advance-stage-modal`, `sqx/workflow/batch-create-modal`). The inline `ngModel` block is the outlier, and a kind-switched field set would push an already 500+ line template further. Extracting is **convergence on the existing convention, not scope creep**.

Copy stays hardcoded Spanish: the entire `features/portfolios` tree has **zero** ngx-translate usage. The proposal's `assets/i18n/{en,es}.json` line item is dropped — adding keys for only the new block would create a mixed pattern. Pre-existing debt, noted, not addressed here.

### Decision: Per-kind validation now, inline in the service

`RiskLimitsService.UpsertAsync` today validates only a non-empty broker (`:30-31`). This change adds kind-aware validation there, throwing `ArgumentException` — `RiskLimitsController.cs:25` already maps that to 400, so **no controller change**. FluentValidation stays out of scope per the proposal.

Rules: reject var fields on a `LossLimits` payload and loss fields on a `VarTarget` payload; reject `VarFloorPct > TargetVarPct`; reject any percentage outside `(0, 1]`. No horizon rule — the horizon is not user-supplied.

## Estimator Specification

Input is the existing dense **calendar-day** net series from `WindowedDailyNets` (`PortfolioAnalyticsCalculator.cs:394-407`), trimmed to 250 days.

```
const int MonthlyVarHorizonDays = 30;   // vendor constant (KB §2), NOT persisted
const int MinHistoryDays        = 90;   // user decision #2 — 3 independent windows

n = series.Count ;  H = MonthlyVarHorizonDays
if n < MinHistoryDays  →  InsufficientHistory, no estimate emitted
sums[i] = Σ series[i .. i+H-1]      for i = 0 .. n-H     → (n-H+1) windows
MonthlyVar95        = -Percentile(sort(sums), 0.05)      // reuse Percentile :422
MonthlyVar95Percent = MonthlyVar95 / initialCapital
OverlappingWindows  = n-H+1     IndependentWindows = n / H   (integer)
```

No √t scaling (KB §5 trap 1). New helper `AnalyticsSeries.RollingWindowSums(series, H)`.

**Stated statistical weakness (must reach the UI):** at n=250, H=30 that is **221 overlapping but only ~8 independent** observations. The 5th percentile of 221 heavily-correlated sums is a soft tail estimate with an effective sample size near 8. `OverlappingWindows` and `IndependentWindows` both ship in the DTO so the card can show them.

**Interaction with the calendar-dense defect (do not fix here).** `WindowedDailyNets` feeds weekend/holiday zeros into `VarFromDaily` (`:410-419`), biasing the **daily** percentile low — a separate defect with its own change. The new estimator is **unaffected**: zeros sum to zero, and 30 elements = 30 calendar days = Darwinex's stated horizon, so the calendar-dense series is *correct* for rolling sums and *incorrect* only for daily percentiles. Both changes edit `PortfolioAnalyticsCalculator` and `AnalyticsSeries`; whichever lands second rebases. This design does not depend on that fix.

## Data Flow

    StrategyTrade ──→ WindowedDailyNets ──→ dense calendar-day nets
                                                │
                          ┌─────────────────────┴──────────────────────┐
                          ↓                                            ↓
                   VarFromDaily (daily VaR95)          RollingWindowSums(H) → p05 → MonthlyVar95
                          │                                            │
                          └──────────→ ServiceRiskDto ←────────────────┘
                                            │
        BrokerRiskLimits (Kind) ──→ PortfolioService.GetRiskAsync (branch by Kind)
                                            │
                          ┌─────────────────┴─────────────────┐
                          ↓                                   ↓
              LossLimits: headroom + breach          VarTarget: band + multiplier
                          └─────────→ ServiceGuardrailDto ────┘
                                            ↓
                              Angular discriminated union on `kind`

## File Changes

| File | Action | Description |
|---|---|---|
| `Domain/Enums/GuardrailKind.cs` | Create | `LossLimits = 0`, `VarTarget = 1` |
| `Domain/Entities/BrokerRiskLimits.cs` | Modify | `Kind` (non-nullable) + nullable `TargetVarPct` + `VarFloorPct`. **No horizon column** |
| `Infrastructure/Persistence/Configurations/BrokerRiskLimitsConfiguration.cs` | Modify | `Kind` `HasConversion<int>()`; var pcts `HasPrecision(9,6)`; **`Broker` unique index unchanged** |
| `Infrastructure/Persistence/Migrations/` | Create | Additive — see Migration below |
| `Application/DTOs/Portfolios/BrokerRiskLimitsDto.cs` | Modify | `Kind` + `TargetVarPct` + `VarFloorPct` on both records. The horizon is **not** part of the upsert contract |
| `Application/DTOs/Portfolios/PortfolioAnalyticsDto.cs:137-159` | Modify | `ServiceRiskDto` gains monthly-VaR fields; `ServiceGuardrailDto` gains `Kind` + nullable `VarTarget` block |
| `Infrastructure/Services/AnalyticsSeries.cs` | Modify | `RollingWindowSums(series, windowLength)`; `MonthlyVarHorizonDays` / `MinHistoryDays` constants |
| `Infrastructure/Services/PortfolioAnalyticsCalculator.cs:250-266` | Modify | Monthly VaR per service + portfolio-wide |
| `Infrastructure/Services/PortfolioService.cs:374-390` | Modify | Branch by `Kind`; **no headroom/breach for `VarTarget`** |
| `Infrastructure/Services/RiskLimitsService.cs:27-53` | Modify | Kind-aware validation + var-field mapping |
| `web/core/services/portfolio.service.ts:160-216` | Modify | `GuardrailKind` enum + discriminated-union types |
| `web/features/portfolios/risk-limits-modal/` | Create | Extracted standalone modal, typed reactive form, `OnPush`, `input()`/`output()` |
| `web/.../portfolio-detail.component.{ts,html}` | Modify | Drop inline modal + `limitsForm`; `@switch (g.kind)` on the guardrail card |
| `tests/AppTradingAlgoritmico.UnitTests/Portfolios/PortfolioAnalyticsCalculatorTests.cs` | Modify | Rolling-window estimator cases |
| `tests/AppTradingAlgoritmico.UnitTests/Portfolios/RiskLimitsServiceTests.cs` | Create | Per-kind validation cases |

## Interfaces / Contracts

```csharp
public sealed record ServiceGuardrailDto(
    string Service, FundingService FundingService, GuardrailKind Kind,
    bool Configured, bool Verified,
    decimal? DailyLossLimitPct, decimal? MaxLossLimitPct, decimal? ProfitTargetPct,
    DrawdownModel? DrawdownModel,
    decimal ServiceVar95Percent,
    decimal? DailyHeadroomPct, bool DailyBreached,   // LossLimits only — null/false for VarTarget
    VarTargetReadoutDto? VarTarget);                 // VarTarget only

/// <param name="HorizonDays">DERIVED, not stored — echoes the calculator's
/// <c>MonthlyVarHorizonDays</c> constant so the card can label the readout without
/// hardcoding 30 in the template. It is analytics output, never guardrail configuration.</param>
/// <param name="ImpliedMultiplier">TargetVar / StrategyVar (KB §3). Null when the estimate is
/// absent or zero. Indicative only: `f`, cadence and methodology are undocumented (KB §4).</param>
public sealed record VarTargetReadoutDto(
    decimal? TargetVarPct, decimal? VarFloorPct, int HorizonDays,
    bool InsufficientHistory, int ObservationDays,
    int OverlappingWindows, int IndependentWindows,
    decimal? MonthlyVar95, decimal? MonthlyVar95Percent,
    decimal? ImpliedMultiplier);
```

```typescript
export enum GuardrailKind { LossLimits = 0, VarTarget = 1 }
export type ServiceGuardrailDto = LossLimitsGuardrail | VarTargetGuardrail; // discriminated on `kind`
```

D-Leverage caps (16.25 / 13 / 9.75) are **frontend display constants beside the multiplier**, never domain constants and never sent from the API, with an explicit note that the app cannot resolve which cap applies — position duration is not modelled.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Unit | `RollingWindowSums` + monthly p05 on a known series; `n < 90` → `InsufficientHistory`; overlapping/independent counts; zero-filled days do not distort sums | xUnit + FluentAssertions, AAA, in `PortfolioAnalyticsCalculatorTests` |
| Unit | Cross-kind field rejection, `VarFloorPct > TargetVarPct`, pct outside `(0,1]` | New `RiskLimitsServiceTests` over an in-memory/SQLite `AppDbContext` |
| Regression | Existing `LossLimits` rows produce byte-identical headroom/breach output | Golden assertions on `GetRiskAsync` |
| Frontend | Modal switches field set by kind; `VarTarget` card renders no headroom bar and no breach badge | Vitest component tests |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

Single additive EF migration (`AddGuardrailKind`), created and applied via the `manage-db` skill (build first; `AppDbContextFactory` must resolve).

| Column | DDL | Existing-row effect |
|---|---|---|
| `Kind` | `int NOT NULL DEFAULT 0` | Every existing row becomes `LossLimits` — **no data loss, no value rewritten** |
| `TargetVarPct` | `decimal(9,6) NULL` | NULL |
| `VarFloorPct` | `decimal(9,6) NULL` | NULL |

**Three columns, no horizon column.** No backfill script: the `DEFAULT 0` is exactly the intended mapping because every row today is a loss-limits ruleset. The `Broker` unique index is untouched. `Down` drops the three columns; pre-existing rows survive intact, only post-change var-target values are lost. No feature flag — the frontend is additive and an old client ignores the new fields.

## Open Questions

None. All four proposal questions were closed by the user before this phase.

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| **False confidence** — the estimate is read as the real DARWIN VaR | **High — primary risk** | Label "estimate (realized close-to-close proxy)", never "your DARWIN VaR"; show the KB §5 methodology contrast inline; **no pass/fail colouring, no breach badge, no headroom bar** |
| Thin tail — ~8 independent windows | High | `IndependentWindows` shipped in the DTO and displayed; suppressed entirely below 90 days |
| "Below floor" read as safe | Med | Card states KB §3: under-risking instructs the engine to scale **up** |
| Multiplier implies precision | Med | Marked indicative; `f`, cadence, methodology are KB §4 known unknowns and must not be filled in |
| Capital-base mismatch | Med | Denominator labelled "portfolio initial capital" wherever the % appears |
| Nullable per-kind columns allow an invalid row via direct SQL | Low | Accepted tradeoff of the flat shape; service-level validation is the enforcement point (no check-constraint precedent in this codebase) |
| Merge contention with the calendar-dense daily-VaR fix | Low | Same two files; the new estimator is behaviourally independent — whichever lands second rebases |
