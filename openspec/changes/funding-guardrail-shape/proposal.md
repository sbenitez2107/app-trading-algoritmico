# Proposal: Funding Guardrail Shape

> First of three changes split out of the `funding-destination-modelling` exploration.
> Blocks `axi-stage-tracking` (2) and `funding-objective-functions` (3). Backend-only except
> for a nullability fix that reaches the Angular risk-limits modal.

## Intent

`BrokerRiskLimits` models exactly one rulebook — FTMO's — and forces the other two services into it.

| Gap | Consequence today |
|---|---|
| `FundingService` is unbound to `Kind` (`RiskLimitsService.ValidateKindFields` reads only `dto.Kind`) | A `DarwinexZero` row can be saved as `LossLimits` and rendered with breach + headroom semantics the `funding-guardrails` spec forbids for that vendor |
| Axi's max loss is **per stage** (−7% Seed/Incubation/Acceleration/Pro/Pro 500, −10% Pro M, on that stage's initial allocation) | Unrepresentable in a single scalar `MaxLossLimitPct`; the operator must flatten six rules into one number and lose which stage it belongs to |
| Axi has **no published daily loss limit** | `DailyLossLimitPct` invites a fabricated value, then drives `DailyHeadroomPct`/`DailyBreached` off it |
| FTMO is **two products** (1-Step: 3% daily, 10% *trailing*, rebaselined 00:00 CE(S)T off the highest preceding day-end balance, reset to 90% of new initial capital after a withdrawal · 2-Step: 5% daily, 10% *static*) | Nothing records which product a row is; `DrawdownModel` can silently contradict the product |
| `DrawdownModel` is non-nullable, default `Static`, persisted on `VarTarget` rows | A meaningless value that reads as a real Darwinex fact |
| `FundingService` has no live spec — it exists only in `Domain/Enums/FundingService.cs` and an immutable archive | No spec governs its members or its meaning |
| FTMO and Axi both breach on **equity including unrealised open P&L**, evaluated continuously; the app holds only closed trades | Any computed breach figure is a **lower bound** with no mechanism saying so |

**Why now**: both follow-on changes read this shape. Landing them on top of it multiplies the migration
and validation risk instead of isolating it in one small, revertible diff.

## Scope

### In Scope

1. `GuardrailKind` gains `StagedLossLimits`; new normalized child entity `FundingStageLimit`.
2. `FtmoProduct` discriminator (`OneStep`/`TwoStep`) on `LossLimits` rows whose service is `Ftmo`.
3. Hard `Kind`↔`FundingService` binding on write; tolerant, flagged read for legacy rows.
4. `FundingService` promoted into `openspec/specs/funding-guardrails/spec.md`.
5. One **shared** closed-trade-lower-bound disclosure covering FTMO and Axi.
6. `DrawdownModel` made nullable and confined to `LossLimits`.

### Out of Scope

- **Own capital as a destination** — deferred to ~2027 by explicit user decision. No `OwnCapital` enum member. See Risks for the `Other = 0` default.
- **Axi stage membership on `Portfolio`**, stage transitions, quarantine counting → `axi-stage-tracking`. This change ships the stage *rulebook table* only, never a portfolio's current stage.
- **Any objective function, score, or ranking** → `funding-objective-functions`.
- Axi progression columns (`MinDaysInStage`, `MinTrades`, `FundingMultiplier`, `ProfitSharePct`, `MinEquity`, `MaxFunding`). See Assumption A1.
- Darwinex margin call (100%) / stop-out (50%). See Approach §5.
- Any change to VaR computation, correlation, or `PortfolioAnalyticsCalculator` maths.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `funding-guardrails`: adds the `StagedLossLimits` kind and stage rulebook, the `FtmoProduct`
  discriminator, the `FundingService` enum requirement, the `Kind`↔`FundingService` binding,
  `DrawdownModel` nullability, and the shared unrealised-P&L disclosure.
- `portfolio-monthly-var`: no requirement change expected — the `VarTarget` path is unchanged.
  Re-confirm during `sdd-spec`; if a delta is needed it is additive only.

## Approach

Adopt the exploration's Option 2 (per-service discriminated shape + normalized child table), with four
refinements. Rejected alternatives are recorded below.

### 1. Discriminator, not two FTMO kinds

`GuardrailKind = LossLimits | VarTarget | StagedLossLimits`.

The exploration floated `FtmoTrailing`/`FtmoStatic` as kinds. **Rejected**: trailing-vs-static is
already `DrawdownModel`, an orthogonal field; encoding it twice creates a contradictable pair. Instead
add `FtmoProduct? { OneStep, TwoStep }`, required when `FundingService = Ftmo`, with a cross-field
invariant — `OneStep ⇒ DrawdownModel = Trailing`, `TwoStep ⇒ DrawdownModel = Static` (both `HIGH`
confidence in `SERVICE_FTMO.md` §2). Numeric thresholds stay user-sourced and unchecked.

The unique index on `Broker` already lets the operator hold both FTMO products at once by naming the
brokers distinctly (e.g. `FTMO 2-Step`), matching how `TradingAccount.Broker` joins today. **No key
change.**

### 2. Axi: `StagedLossLimits` + `FundingStageLimit` child

| Parent field under `StagedLossLimits` | Rule |
|---|---|
| `DailyLossLimitPct` | MUST be null — NOT FOUND in `SERVICE_Axi_Select.md`; the app must not invent one |
| `MaxLossLimitPct`, `ProfitTargetPct`, `DrawdownModel` | MUST be null — they live per stage |
| `TargetVarPct`, `VarFloorPct` | MUST be null |

`FundingStageLimit`: `Id`, `BrokerRiskLimitsId` (FK, cascade), `StageOrdinal` (int),
`StageName` (string), `MaxLossLimitPct` (required), `ProfitTargetPct` (nullable — Pro M publishes
none). Unique index `(BrokerRiskLimitsId, StageOrdinal)`.

Breach consequence for Axi is **Quarantine with stage demotion, not termination** — carried as a
spec requirement and a render rule, not as a stored column (it is a constant of the service, not a
user-sourced number).

**Rejected — more nullable columns on the flat row**: Axi's limits are a 1-to-many relation; ~30
denormalized columns is exactly what `BatchStage` was split out of `Batch` to avoid, and it does not
solve FTMO at all.
**Rejected — JSON / `OwnsOne` / `ToJson` payload**: the codebase has **zero** owned-entity or JSON
domain mapping. The only JSON-as-string is `StrategyGridPreset.VisibleColumnsJson`, a non-queryable
UI preference. Introducing it here would make `ValidateKindFields` stringly-typed and give up the
EF-level guarantees the current design relies on. No argument strong enough to break that pattern.

### 3. `Kind`↔`FundingService` binding — hard on write, flagged on read

The tradeoff is real: this codebase deliberately treats the operator as the authority on numbers
(`Verified`, "USER-SOURCED — never hardcoded"). The chosen line:

> **The app owns which rulebook *shape* applies. The operator owns every number inside it.**

Which shape a vendor uses is a vendor fact with `HIGH` confidence, not an opinion — so it is validated.

| `FundingService` | Required `Kind` |
|---|---|
| `Ftmo` | `LossLimits` |
| `Axi` | `StagedLossLimits` |
| `DarwinexZero` | `VarTarget` |
| `Other` | unconstrained (escape hatch for unmodelled services) |

`UpsertAsync` MUST reject a mismatch (`ArgumentException`, consistent with today's validation style).
**Reads stay tolerant**: a pre-existing row that violates the binding is returned with a
`RulebookMismatch` flag rather than throwing, so the Risk tab surfaces and lets the operator fix it
instead of breaking. This is what makes the migration non-destructive (§ Migration).

### 4. Shared closed-trade-lower-bound disclosure

Reuse the existing unstorable-vendor-fact mechanism from `funding-guardrails/spec.md`
(`VarHorizonDays` / the 45-day window are declared "NOT stored fields — the app cannot honour them").
One requirement, one field, both services — `SERVICE_FTMO.md` §2 and `SERVICE_Axi_Select.md` §2 state
the limitation identically and say the resolution must be shared.

Mechanism: a single nullable `BreachBasis` on the guardrail readout —
`ClosedTradeLowerBound` for `LossLimits` and `StagedLossLimits`, `null` for `VarTarget`. The spec
states that a breach figure derived from closed trades MUST be labelled a lower bound and MUST NOT be
presented as the vendor's breach verdict, because an intraday excursion that dipped below the floor
and recovered before any trade closed is invisible to the app.

**Additive only.** `DailyHeadroomPct` and `DailyBreached` keep byte-identical computation; the basis
field is added beside them. Changing their semantics would regress shipped tests for no gain here.

### 5. Darwinex margin call / stop-out — deferred, via the same mechanism

Margin call at 100% and stop-out at 50% are levels on the **equity/margin ratio of open positions**.
The app holds no open-position or margin data, so a stored 100/50 pair would be two numbers no
computation can consume — the same class of unstorable fact as `VarHorizonDays`. It is therefore
**declared documentation-only in the shared disclosure requirement**, not silently omitted and not
stored. It is an account-level forced liquidation, not a funding breach, so it must never produce a
`breached` flag on the `VarTarget` card.

### 6. `DrawdownModel` nullability

`BrokerRiskLimits.DrawdownModel` → `DrawdownModel?`. Required non-null when `Kind = LossLimits`; MUST
be null for `VarTarget` and `StagedLossLimits`. `ServiceGuardrailDto.DrawdownModel` is *already*
nullable, so the analytics path is unaffected; `BrokerRiskLimitsDto.DrawdownModel` becomes nullable,
which reaches the Angular modal.

### Source-compatibility constraint (load-bearing)

New members on `UpsertBrokerRiskLimitsDto` / `BrokerRiskLimitsDto` MUST be **appended with default
values** (`FtmoProduct? FtmoProduct = null`, `IReadOnlyList<UpsertFundingStageLimitDto>? Stages =
null`). Both are positional records; appending without defaults breaks every existing call site and
turns a byte-identical test set into a rewritten one.

## Affected Areas

| Layer | Area | Impact | What changes |
|---|---|---|---|
| Domain | `Enums/GuardrailKind.cs` | Modified | `+ StagedLossLimits` |
| Domain | `Enums/FtmoProduct.cs` | New | `OneStep`, `TwoStep` |
| Domain | `Enums/FundingService.cs` | Unchanged | Promoted into spec only — no new members |
| Domain | `Entities/BrokerRiskLimits.cs` | Modified | `DrawdownModel` → nullable, `+ FtmoProduct?`, `+ ICollection<FundingStageLimit>` |
| Domain | `Entities/FundingStageLimit.cs` | New | Stage rulebook row |
| Application | `DTOs/Portfolios/BrokerRiskLimitsDto.cs` | Modified | Nullable `DrawdownModel`; appended `FtmoProduct`, `Stages`, `RulebookMismatch` |
| Application | `DTOs/Portfolios/FundingStageLimitDto.cs` | New | Read + upsert stage records |
| Application | `DTOs/Portfolios/PortfolioAnalyticsDto.cs` | Modified | `ServiceGuardrailDto` gains `BreachBasis` (additive) |
| Infrastructure | `Services/RiskLimitsService.cs` | Modified | `ValidateKindFields` extended; stage collection upsert; binding check |
| Infrastructure | `Services/PortfolioService.cs::GetRiskAsync` | Modified | Third `Kind` branch + `BreachBasis`; existing two branches untouched |
| Infrastructure | `Persistence/Configurations/BrokerRiskLimitsConfiguration.cs` | Modified | Nullable column, new FK/child config |
| Infrastructure | `Persistence/Migrations/` | New | 1 migration (see below) |
| WebAPI | `Controllers/RiskLimitsController.cs` | Modified | No new endpoints; changed payload contract only |
| Angular | `features/portfolios/risk-limits-modal` | Modified | Nullable `drawdownModel`, FTMO product select, Axi stage rows |
| Angular | `features/portfolios/portfolio-detail` | Modified | Render the staged card + the lower-bound label |
| Angular | `core/services/portfolio.service.ts` | Modified | Type updates only |
| Specs | `openspec/specs/funding-guardrails/spec.md` | Modified | Delta (see Capabilities) |

## Migration and Rollback

**One migration, `ReshapeFundingGuardrails`.** Additive except for one column-nullability change.

| Step | Operation | Destructive? |
|---|---|---|
| 1 | `CREATE TABLE FundingStageLimits` + FK + unique `(BrokerRiskLimitsId, StageOrdinal)` | No |
| 2 | `ADD COLUMN FtmoProduct int NULL` | No |
| 3 | `ALTER COLUMN DrawdownModel int NULL` | **Yes — schema-relaxing** |
| 4 | `UPDATE BrokerRiskLimits SET DrawdownModel = NULL WHERE Kind = 1` (`VarTarget`) | **Yes — data write** |

No row is deleted, no column is dropped, no `Kind` value is rewritten. Existing `Axi` +
`LossLimits` rows are **left exactly as persisted** and surface as `RulebookMismatch` on read — the
migration must not guess a stage split, because inventing values is forbidden by
`.agents/knowledge/imox/INDEX.md`.

**Rollback**: `Down()` runs `UPDATE BrokerRiskLimits SET DrawdownModel = 0 WHERE DrawdownModel IS
NULL`, restores `NOT NULL`, drops `FtmoProduct`, drops `FundingStageLimits`. This is **lossless for
pre-change data**: step 4 only nulls rows that held `Static` (`0`) — the enum default — so the
restore returns the exact prior value. Rows created *after* the change lose their stage rows and
FTMO product on rollback; back up `FundingStageLimits` before running `Down()` if any exist. Because
the DTO contract changes, a rollback of the API without the web build leaves the modal sending a null
`drawdownModel` into a non-nullable column — **roll back both projects together**.

## Testing Approach (Strict TDD)

Every item lands RED → GREEN → REFACTOR via `dotnet test`. Backend first; the Angular slice follows
with Vitest.

**Expected to remain byte-identical** (regression fence — if any of these change, the change is wrong):

- `Portfolios/PortfolioAnalyticsCalculatorTests.cs` — all
- `Portfolios/PortfolioAnalyticsCalculatorLiveOutputRegressionTests.cs` — all
- `Portfolios/BacktestPortfolioAnalyticsAdapterTests.cs` — all
- `Portfolios/BrokerRiskLimitsTests.cs` — both (neither asserts `DrawdownModel`)
- `Portfolios/RiskLimitsServiceTests.cs` — all 7, **provided** the source-compatibility constraint
  above is honoured
- `Portfolios/PortfolioServiceRiskTests.cs` — every `LossLimits` headroom/breach and `VarTarget`
  no-breach assertion

**Expected to change**: none of the above are rewritten. New tests are added for the new kind, the
binding, the FTMO product invariant, `DrawdownModel` nullability, stage persistence, and
`BreachBasis`. Frontend `risk-limits-modal.component.spec.ts` and
`portfolio-detail.component.spec.ts` gain cases and adjust to the nullable field.

**Bar**: 560 backend tests green, 0 warnings (`warnings-as-errors`), plus the new tests.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Positional-record members appended without defaults break ~15 shipped test call sites | High if unguarded | Explicit source-compatibility constraint above; verified first in `sdd-tasks` |
| Existing `Axi`/`LossLimits` row becomes invalid | Med | Tolerant read + `RulebookMismatch` flag; write-time rejection only. Single-user app, expected 0-2 rows |
| `FundingService.Other = 0` is the zero-default, so an unset enum silently means "unmodelled service" and skips the binding | Med | Raised as a **risk, not scope** per instruction. Deferring `OwnCapital` means the trap persists; recommend a guard in `axi-stage-tracking` or a dedicated change |
| `StagedLossLimits` may still be too narrow for a fourth service | Low | Discriminator is extensible; `Other` remains unconstrained |
| 400-line review budget | Med | Backend and Angular slices are independently deliverable; forecast in `sdd-tasks` |
| Rollback across two projects | Low | Documented above — roll back API and web together |

## Dependencies

None external. Blocks `axi-stage-tracking` and `funding-objective-functions`.

## Success Criteria

- [ ] A `DarwinexZero` row cannot be written as `LossLimits`; `Axi` cannot be written as `LossLimits`; `Ftmo` cannot be written as `VarTarget`.
- [ ] An Axi guardrail holds six stage rows with per-stage max loss and no daily loss limit.
- [ ] An FTMO row carries its product, and `OneStep`+`Static` is rejected.
- [ ] No `VarTarget` row persists a `DrawdownModel`.
- [ ] `FundingService` is defined by a live requirement in `openspec/specs/funding-guardrails/spec.md`.
- [ ] Exactly one requirement — not one per service — states the closed-trade lower-bound limitation, and every FTMO/Axi breach readout carries `BreachBasis`.
- [ ] Darwinex `VarTarget` output is unchanged; the listed test files are byte-identical.
- [ ] `dotnet test` green with 0 warnings; migration applies and reverts cleanly.

## Assumptions Flagged

- **A1** — Axi progression columns (`MinDaysInStage`, `MinTrades`, `FundingMultiplier`,
  `ProfitSharePct`, `MinEquity`, `MaxFunding`) are **excluded**, contradicting the exploration's field
  list. Rationale: they are progression inputs owned by `axi-stage-tracking`, no consumer reads them
  here, and strict TDD cannot drive a column beyond a persistence round-trip. Adding them later is a
  purely additive migration. **Reverse this if the user prefers one stage-table migration.**
- **A2** — `Other` is left unconstrained. If the user wants `Other → LossLimits` only, say so; it is a
  one-line validation change but risks rejecting an existing row.
- **A3** — `portfolio-monthly-var` needs no delta. To be re-confirmed in `sdd-spec`.
- **A4** — Both FTMO products are expressed as two rows with distinct `Broker` names. If the user
  expects one FTMO row carrying both products, the unique-`Broker` key must change — a materially
  larger migration.

## Proposal Question Round

Interactive answers were not available to this executor. These need a human decision before `sdd-spec`:

1. **A1** — one stage-table migration now with all progression columns, or the narrow breach-only
   table plus an additive migration in `axi-stage-tracking`?
2. **Binding strictness** — is hard write-rejection plus tolerant flagged read the right line, or
   should the mismatch be advisory everywhere given the operator-as-authority principle?
3. **A4** — do you run 1-Step and 2-Step simultaneously? If yes, is "two brokers with distinct names"
   acceptable, or must one FTMO row carry both products?
4. **Darwinex margin call / stop-out** — accept deferral as documentation-only, or is a visible
   stop-out warning on the `VarTarget` card wanted now (it would need open-position margin data the
   app does not hold)?
5. **`FundingService.Other = 0`** — accept as a flagged risk for this change, or address the
   zero-default trap now despite own capital being deferred to ~2027?
