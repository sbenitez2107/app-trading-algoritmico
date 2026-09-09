# Tasks: Funding Guardrail Shape

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~850-950 (13 backend files, 1 migration, 2 new + 5 extended test files, 3 frontend files) |
| 400-line budget risk | High |
| Chained PRs recommended | No (maintainer accepted `size:exception`) |
| Suggested split | Single PR |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Domain shape policy + entities + migration | PR 1 (single) | `dotnet test --filter GuardrailShapeTests` | `dotnet ef database update` / `dotnet ef migrations script` | Revert migration + Domain files together |
| 2 | Application/Infrastructure binding, normalization, stage upsert, computed DTO members | PR 1 (single) | `dotnet test --filter RiskLimitsServiceTests\|PortfolioServiceRiskTests` | `dotnet test` full backend suite | Revert Service/DTO files with unit 1 |
| 3 | Angular modal + detail + types | PR 1 (single) | `pnpm --dir app.trading.algoritmico.web test` | manual: open risk-limits modal, save Axi/Ftmo/DarwinexZero rows | Revert Angular files with API together (contract coupling) |

## Phase 1: Domain (data model)

- [x] 1.1 Add `StagedLossLimits = 2` to `Domain/Enums/GuardrailKind.cs`
- [x] 1.2 Create `Domain/Enums/FtmoProduct.cs` (`OneStep`, `TwoStep`)
- [x] 1.3 Create `Domain/Enums/BreachBasis.cs` (`ClosedTradeLowerBound`)
- [x] 1.4 RED: `GuardrailShapeTests` — `RequiredKindFor` per `FundingService` (`Ftmo→LossLimits`, `Axi→StagedLossLimits`, `DarwinexZero→VarTarget`, `Other→null`); `IsFtmoProductConsistent(OneStep,Static)=false`, `(OneStep,Trailing)=true`, `(TwoStep,Static)=true`, `(null,*)=true`
- [x] 1.5 GREEN: create `Domain/Guardrails/GuardrailShape.cs` — `RequiredKindFor`, `IsShapeConsistent`, `IsFtmoProductConsistent`
- [x] 1.6 Modify `Domain/Entities/BrokerRiskLimits.cs` — `DrawdownModel?`, `+ FtmoProduct?`, `+ ICollection<FundingStageLimit> Stages`
- [x] 1.7 Create `Domain/Entities/FundingStageLimit.cs` — `Id`, `BrokerRiskLimitsId`, `StageOrdinal`, `StageName`, `MaxLossLimitPct`, `ProfitTargetPct?`

## Phase 2: Migration

- [x] 2.1 Modify `BrokerRiskLimitsConfiguration.cs` (nullable `DrawdownModel`) and create `FundingStageLimitConfiguration.cs` (FK cascade, unique `(BrokerRiskLimitsId, StageOrdinal)`)
- [x] 2.2 Create `ReshapeFundingGuardrails` migration — `Up()`: create `FundingStageLimits` table, add nullable `FtmoProduct`, relax `DrawdownModel` to nullable; **no data write**. `Down()`: fill `DrawdownModel = 0` where null, restore `NOT NULL`, drop `FtmoProduct`, drop `FundingStageLimits`

## Phase 3: Backend Application/Infrastructure

- [x] 3.1 RED: `RiskLimitsServiceTests` — reject `DarwinexZero+LossLimits`, `Axi+LossLimits`, `Ftmo+VarTarget`
- [x] 3.2 GREEN: `RiskLimitsService.UpsertAsync` binding check via `GuardrailShape.RequiredKindFor`
- [x] 3.3 RED: `RiskLimitsServiceTests` — `Ftmo+OneStep+Static` rejected, `TwoStep+Static` accepted, null `FtmoProduct` accepted
- [x] 3.4 GREEN: wire `IsFtmoProductConsistent` into `UpsertAsync`
- [x] 3.5 RED: `RiskLimitsServiceTests` — `VarTarget` upsert carrying `DrawdownModel.Trailing` persists `null`
- [x] 3.6 GREEN: normalize `DrawdownModel` to `null` when `Kind != LossLimits`
- [x] 3.7 RED: `RiskLimitsServiceTests` — `StagedLossLimits` persists 6 stage rows; duplicate `StageOrdinal` rejected; any parent scalar non-null rejected
- [x] 3.8 GREEN: extend `ValidateKindFields` third branch + stage collection replace-upsert in `RiskLimitsService`
- [x] 3.9 Append `FtmoProduct? FtmoProduct = null`, `IReadOnlyList<UpsertFundingStageLimitDto>? Stages = null` to `UpsertBrokerRiskLimitsDto`; same appended defaults + computed `RulebookMismatch` on `BrokerRiskLimitsDto`
- [x] 3.10 Create `Application/DTOs/Portfolios/FundingStageLimitDto.cs`
- [x] 3.11 RED: DTO/service test — `ToDto` of legacy `Axi+LossLimits` row returns `RulebookMismatch=true` (no throw); `Ftmo` row with null `FtmoProduct` returns `RulebookMismatch=true`
- [x] 3.12 GREEN: implement computed `RulebookMismatch` using `GuardrailShape`
- [x] 3.13 RED: `PortfolioServiceRiskTests` — `StagedLossLimits` guardrail emits no daily headroom, `DailyBreached=false`
- [x] 3.14 GREEN: `PortfolioService.GetRiskAsync` third `Kind` branch + `Include(Stages)`
- [x] 3.15 RED: `PortfolioServiceRiskTests`/analytics DTO test — `BreachBasis=ClosedTradeLowerBound` for `LossLimits`/`StagedLossLimits`, `null` for `VarTarget`
- [x] 3.16 GREEN: computed `BreachBasis` property on `ServiceGuardrailDto`

## Phase 4: Frontend

- [x] 4.1 Update `core/services/portfolio.service.ts` types — nullable `drawdownModel`, `ftmoProduct`, `stages`, `rulebookMismatch`, `breachBasis`
- [x] 4.2 RED: `risk-limits-modal.component.spec.ts` — FTMO product select, Axi stage-rows form array, nullable `drawdownModel` submit
- [x] 4.3 GREEN: `risk-limits-modal.component` — product select + stage rows UI
- [x] 4.4 RED: `portfolio-detail.component.spec.ts` — `StagedLossLimits` card renders stage rows; lower-bound label shown when `breachBasis` set
- [x] 4.5 GREEN: `portfolio-detail.component` — render staged card + `BreachBasis` label

## Phase 5: Verification

- [x] 5.1 Run `dotnet test` from `app.trading.algoritmico.api` — confirm 560 pre-existing + new tests passing, 0 warnings; explicitly diff the regression fence (`PortfolioAnalyticsCalculatorTests`, `PortfolioAnalyticsCalculatorLiveOutputRegressionTests`, `BacktestPortfolioAnalyticsAdapterTests`, both `BrokerRiskLimitsTests`, all 7 pre-existing `RiskLimitsServiceTests`, `PortfolioServiceRiskTests` LossLimits/VarTarget assertions) byte-identical
- [x] 5.2 Run `pnpm --dir app.trading.algoritmico.web test` — confirm green
- [x] 5.3 Verify migration round-trip: `dotnet ef database update` then `dotnet ef database update <previous>` reverts cleanly with no error
