# Design: Funding Guardrail Shape

## Technical Approach

Extend the proven `GuardrailKind` discriminator with `StagedLossLimits` and add a normalized
`FundingStageLimit` child table for Axi. One pure Domain policy — `GuardrailShape` — states which
`Kind` each `FundingService` requires; the write path throws on violation, the read path *computes*
a flag from the same policy. No EF owned entities, no JSON columns.

Two DTO facts (`RulebookMismatch`, `BreachBasis`) are **computed properties, not constructor
parameters**. This is the load-bearing choice: a computed member cannot be omitted at a call site,
cannot be defaulted wrongly, and cannot contradict the numbers beside it — so the closed-trade
caveat is enforced at the DTO boundary rather than documented. It also adds zero positional
parameters, which is stronger than the appended-with-default constraint the proposal required.

## Architecture Decisions

| # | Decision | Rejected alternative | Rationale |
|---|---|---|---|
| D1 | `GuardrailShape` static in `Domain/Guardrails/` is the single source of the `Kind`↔`FundingService` rule; writer throws, reader flags | Duplicate the table in `RiskLimitsService` and `ToDto` | One rule, two consumers — write-reject and read-flag cannot drift |
| D2 | `RulebookMismatch` is a **computed** property on `BrokerRiskLimitsDto` | Appended positional param with `= false` default | A default-false flag silently means "fine" when a call site forgets it |
| D3 | `BreachBasis` is a **computed** property on `ServiceGuardrailDto`, a pure function of `Kind` (`LossLimits`/`StagedLossLimits` → `ClosedTradeLowerBound`; `VarTarget` → `null`) | Sub-record wrapping the breach fields | Wrapping `DailyHeadroomPct`/`DailyBreached` would break `PortfolioServiceRiskTests`; a computed function of `Kind` is unforgeable and satisfies "one mechanism, both services" |
| D4 | `DrawdownModel` on a non-`LossLimits` payload is **normalized to null**, not rejected | Reject with `ArgumentException` | Rejection breaks `UpsertAsync_ValidVarTargetPair_PersistsUnchanged` — its helper passes `DrawdownModel.Static`. Normalization reaches the same persisted invariant with the fence intact |
| D5 | `FtmoProduct` is **optional on write**; the `OneStep⇒Trailing` / `TwoStep⇒Static` invariant is enforced only when non-null. A null product on an `Ftmo` row raises `RulebookMismatch` on read | Required when `FundingService = Ftmo` | Conditional-required breaks `UpsertAsync_ValidLossLimitsPayload_PersistsUnchanged` (no product passed). Tolerant-write + flagged-read matches the policy already chosen for legacy rows |
| D6 | `Up()` performs **no data write**; the stale `DrawdownModel` on legacy `VarTarget` rows is left as persisted, hidden by read normalization in `ToDto`, and healed by the next upsert | Proposal step 4 (`UPDATE ... SET DrawdownModel = NULL WHERE Kind = VarTarget`) | See Migration — the proposal's losslessness claim is false |
| D7 | `RulebookMismatch` covers both wrong-`Kind` and `Ftmo`-without-product | Two separate flags | Both resolve to the same operator action; accepted information loss |

## Migration — `ReshapeFundingGuardrails`

The proposal claimed step 4 was lossless because `VarTarget` rows "held `Static` (`0`)".
**Verified false**: `ValidateKindFields` never inspects `DrawdownModel`, and
`UpsertBrokerRiskLimitsDto.DrawdownModel` is non-nullable, so `Trailing` is accepted and persisted
on a `VarTarget` row. Live data cannot be inspected (no DB authorisation), so the design must be
correct without it.

**Resolution — delete step 4.** `Up()` is schema-only:

| Step | Operation | Data write |
|---|---|---|
| 1 | `CREATE TABLE FundingStageLimits` + FK (cascade) + unique `(BrokerRiskLimitsId, StageOrdinal)` | No |
| 2 | `ADD COLUMN FtmoProduct int NULL` | No |
| 3 | `ALTER COLUMN DrawdownModel int NULL` | No |

`Down()`: `UPDATE BrokerRiskLimits SET DrawdownModel = 0 WHERE DrawdownModel IS NULL` → restore
`NOT NULL` → drop `FtmoProduct` → drop `FundingStageLimits`.

Why this rollback is correct **by construction, not by assumption**: before the migration the
column was `NOT NULL`, so no pre-change row can be `NULL`. Every row the `Down()` fill touches was
therefore written *after* the change and has no prior value to preserve — `0` is the column default
the pre-change schema itself would have supplied. The fill is provably scoped from the schema, not
from the data. Rows created after the change still lose `FtmoProduct` and stage rows on rollback:
back up `FundingStageLimits` first, and roll back API + web together (unchanged from the proposal).

## Sequence — upsert write path

```
Controller      RiskLimitsService        GuardrailShape        AppDbContext
    │ UpsertAsync(dto)                        │                    │
    ├──────────────►│ broker blank? ─► ArgumentException            │
    │               ├──► RequiredKindFor(dto.FundingService)        │
    │               │◄── Kind? (null for Other)                     │
    │               │  mismatch ─────► ArgumentException            │
    │               ├──► IsFtmoProductConsistent(product, ddModel)  │
    │               │  false ────────► ArgumentException            │
    │               ├─ ValidateKindFields(dto)  (3 branches now)    │
    │               │    StagedLossLimits: Daily/Max/Profit/Var/DD must be null;
    │               │    Stages required, non-empty, MaxLossLimitPct required,
    │               │    StageOrdinal unique
    │               ├─ NORMALIZE: ddModel := null when Kind != LossLimits   (D4)
    │               ├──────────── load-or-create by Broker ────────►│
    │               ├──────────── replace Stages collection ───────►│
    │               ├──────────── SaveChangesAsync ────────────────►│
    │◄─── ToDto(entity)  (DrawdownModel projected null off-kind;    │
    │      RulebookMismatch computed)                               │
```

## Sequence — risk read path producing `RulebookMismatch` / `BreachBasis`

```
PortfoliosController   PortfolioService        Calculator       AppDbContext
    │ GetRiskAsync(id)        │                     │                │
    ├────────────────────────►├─ LoadMemberInputs ──────────────────►│
    │                         ├─ ComputeVaR ───────►│                │
    │                         ├─ load BrokerRiskLimits by Broker ───►│
    │                         │  (Include(Stages) — StagedLossLimits only)
    │                         │
    │                         ├─ switch (lim?.Kind ?? LossLimits)
    │                         │   VarTarget       → existing branch, untouched
    │                         │   LossLimits      → existing branch, untouched
    │                         │   StagedLossLimits→ NEW: stage rows, no daily
    │                         │                     headroom, DailyBreached=false
    │◄── ServiceGuardrailDto  │
    │      .BreachBasis      => computed from Kind        (D3)
    │      (RulebookMismatch surfaces via the config DTO on the Risk tab)
```

## File Changes

| File | Action |
|---|---|
| `Domain/Enums/GuardrailKind.cs` | Modify — `+ StagedLossLimits = 2` |
| `Domain/Enums/FtmoProduct.cs`, `Domain/Enums/BreachBasis.cs` | Create |
| `Domain/Guardrails/GuardrailShape.cs` | Create — `RequiredKindFor`, `IsShapeConsistent`, `IsFtmoProductConsistent` |
| `Domain/Entities/BrokerRiskLimits.cs` | Modify — `DrawdownModel?`, `FtmoProduct?`, `ICollection<FundingStageLimit> Stages` |
| `Domain/Entities/FundingStageLimit.cs` | Create — `BrokerRiskLimitsId`, `StageOrdinal`, `StageName`, `MaxLossLimitPct`, `ProfitTargetPct?` |
| `Application/DTOs/Portfolios/BrokerRiskLimitsDto.cs` | Modify — `DrawdownModel?`; appended `FtmoProduct? = null`, `Stages = null`; computed `RulebookMismatch` |
| `Application/DTOs/Portfolios/FundingStageLimitDto.cs` | Create |
| `Application/DTOs/Portfolios/PortfolioAnalyticsDto.cs` | Modify — computed `BreachBasis` on `ServiceGuardrailDto` (no new parameter) |
| `Infrastructure/Services/RiskLimitsService.cs` | Modify — binding, 3-branch validation, normalization, stage upsert |
| `Infrastructure/Services/PortfolioService.cs` | Modify — third `Kind` branch + `Include(Stages)` |
| `Infrastructure/Persistence/Configurations/BrokerRiskLimitsConfiguration.cs`, `FundingStageLimitConfiguration.cs` | Modify / Create |
| `Infrastructure/Persistence/Migrations/*_ReshapeFundingGuardrails.cs` | Create |

Axi stage rows carry **breach fields only**; progression columns belong to `axi-stage-tracking`.
Axi's daily loss limit stays `NOT FOUND` — never stored, never defaulted. Darwinex margin
call / stop-out remain documentation-only and never produce a `breached` flag.

## Testing Strategy (strict TDD — RED first, in this order)

| # | RED test | Pins |
|---|---|---|
| 1 | `GuardrailShapeTests` — required kind per service; `Other` unconstrained | D1, the binding table |
| 2 | `RiskLimitsServiceTests` (new): `DarwinexZero`+`LossLimits`, `Axi`+`LossLimits`, `Ftmo`+`VarTarget` rejected | Write-side binding |
| 3 | `Ftmo`+`OneStep`+`Static` rejected; `TwoStep`+`Static` accepted; null product accepted | D5 invariant |
| 4 | `VarTarget` upsert carrying `DrawdownModel.Trailing` persists `null` | D4 normalization |
| 5 | `StagedLossLimits` upsert persists 6 stage rows; duplicate `StageOrdinal` rejected; any parent scalar non-null rejected | Stage shape |
| 6 | `ToDto` of a legacy `Axi`+`LossLimits` row returns `RulebookMismatch = true` and does not throw | D2, tolerant read |
| 7 | `PortfolioServiceRiskTests` (new): `StagedLossLimits` guardrail emits no daily headroom, `DailyBreached = false` | Third branch |
| 8 | `BreachBasis` is `ClosedTradeLowerBound` for both breach kinds and `null` for `VarTarget` | D3 |

**Regression fence — must stay byte-identical:** `PortfolioAnalyticsCalculatorTests`,
`PortfolioAnalyticsCalculatorLiveOutputRegressionTests`, `BacktestPortfolioAnalyticsAdapterTests`,
`BrokerRiskLimitsTests`, all 7 `RiskLimitsServiceTests`, the `PortfolioServiceRiskTests`
LossLimits/VarTarget assertions. D4 and D5 exist specifically to keep this true; the entity
object-initializer at `PortfolioServiceRiskTests.cs:102` still compiles against `DrawdownModel?`.
**No fence test is expected to break.**

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary.

## Open Questions

- [ ] Proposal question rounds 1-5 remain unanswered by a human; this design assumes A1 (breach-only
  stage table), A2 (`Other` unconstrained), A4 (one FTMO row, `TwoStep`).
- [ ] `FundingService.Other = 0` zero-default remains a recorded risk, not scope.
