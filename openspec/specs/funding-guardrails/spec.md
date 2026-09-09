# Funding Guardrails Specification

## Purpose

Per-broker funding guardrails are typed by `GuardrailKind` so each funding
service is modeled only with the fields its own rulebook defines.
`LossLimits` (Other/FTMO/Axi) keeps today's breach-style fields; `VarTarget`
(Darwinex Zero) models a monthly VaR-target rulebook with no breach semantics
(`.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` §1-§3); `StagedLossLimits` (Axi)
models progressive drawdown-based stage demotion.

> Non-goal: FTMO's Standard-vs-Swing axis (weekend-flat, news window, leverage) is deliberately
> excluded — both share identical loss limits, so it is strategy eligibility, not a guardrail.
> Not overlooked; belongs to a future eligibility capability.

## Requirements

### Requirement: FundingService Enum Definition

`FundingService` MUST be `Ftmo | Axi | DarwinexZero | Other`. Each `BrokerRiskLimits` row MUST carry
one. `Other` is the unconstrained escape hatch for unmodelled services.

#### Scenario: Row carries a FundingService
- GIVEN a new `BrokerRiskLimits` row
- WHEN it is upserted
- THEN it persists a `FundingService` value from the enum above

### Requirement: Kind Bound to FundingService

The app owns which rulebook shape applies; the operator owns every number inside it. `Ftmo` requires
`Kind = LossLimits`, `Axi` requires `StagedLossLimits`, `DarwinexZero` requires `VarTarget`; `Other`
is unconstrained. `UpsertAsync` MUST reject a mismatched write. A pre-existing mismatched row MUST
NOT be rejected on read — it MUST be returned with `RulebookMismatch = true`.

#### Scenario: Mismatched write rejected
- GIVEN an upsert payload with `FundingService = DarwinexZero` and `Kind = LossLimits`
- WHEN it is upserted
- THEN the request is rejected with a validation error

#### Scenario: Legacy mismatched row flagged, not broken, on read
- GIVEN a persisted row with `FundingService = Axi` and `Kind = LossLimits`
- WHEN the row is read
- THEN it is returned unchanged with `RulebookMismatch = true` and no exception

### Requirement: FtmoProduct Discriminator

`FtmoProduct` (`OneStep | TwoStep`) MUST be optional on write; a `LossLimits` payload with
`FundingService = Ftmo` and no `FtmoProduct` MUST be accepted. When `FtmoProduct` is non-null,
`OneStep` MUST require `DrawdownModel = Trailing` and `TwoStep` MUST require `DrawdownModel =
Static`; `UpsertAsync` MUST reject a payload violating that invariant. An `Ftmo` row that persists
without a product MUST surface `RulebookMismatch = true` on read, never a write rejection.

#### Scenario: OneStep with Static rejected
- GIVEN an Ftmo LossLimits payload with `FtmoProduct = OneStep` and `DrawdownModel = Static`
- WHEN it is upserted
- THEN the request is rejected with a validation error

#### Scenario: Ftmo payload without a product accepted
- GIVEN an Ftmo LossLimits payload with `FtmoProduct` omitted
- WHEN it is upserted
- THEN the request is persisted unchanged with no validation error

#### Scenario: Ftmo row without a product flagged on read
- GIVEN a persisted `Ftmo` `LossLimits` row with `FtmoProduct = null`
- WHEN the row is read
- THEN it is returned unchanged with `RulebookMismatch = true` and no exception

### Requirement: GuardrailKind Discriminator

Each `BrokerRiskLimits` row MUST carry a `Kind` of `LossLimits`, `VarTarget`, or `StagedLossLimits`.
The kind determines which field set is valid and how the Risk-tab card renders.
(Previously: only `LossLimits` or `VarTarget`.)

#### Scenario: Creating a LossLimits guardrail
- GIVEN the user selects a broker other than Darwinex Zero or Axi
- WHEN they save the guardrail
- THEN the row is persisted with `Kind = LossLimits`

#### Scenario: Creating a VarTarget guardrail
- GIVEN the user selects Darwinex Zero as the broker
- WHEN they save the guardrail
- THEN the row is persisted with `Kind = VarTarget`

#### Scenario: Creating a StagedLossLimits guardrail
- GIVEN the user selects Axi as the broker
- WHEN they save the guardrail
- THEN the row is persisted with `Kind = StagedLossLimits` and its stage collection

### Requirement: Kind Determines Valid Field Set

`LossLimits` MUST expose `DailyLossLimitPct`, `MaxLossLimitPct`, `ProfitTargetPct`, and a required
`DrawdownModel`. `VarTarget` MUST expose only `TargetVarPct` and `VarFloorPct`, and `DrawdownModel`
MUST be null. `StagedLossLimits` MUST expose only its `FundingStageLimit` collection; its
`DailyLossLimitPct`, `MaxLossLimitPct`, `ProfitTargetPct`, `DrawdownModel`, `TargetVarPct`, and
`VarFloorPct` MUST all be null. `VarHorizonDays` (30) and the vendor's 45-day window remain
documentation-only. `UpsertAsync` MUST reject a payload setting a field not valid for its kind.
(Previously: only two kinds; `DrawdownModel` was non-nullable and unconditionally valid for
`LossLimits`.)

> **The two null outcomes for `DrawdownModel` are reached by different mechanisms, and the
> difference is deliberate — do not "make it consistent".**
>
> | Kind | Payload carrying a `DrawdownModel` | Result |
> |---|---|---|
> | `VarTarget` | **accepted, normalized to null** | stored null |
> | `StagedLossLimits` | **rejected** | `ArgumentException` |
>
> The asymmetry is load-bearing rather than an oversight. `RiskLimitsServiceTests.VarTargetDto`
> supplies `DrawdownModel.Static` on every VaR-target payload it builds, so turning that path into a
> rejection breaks a test inside the regression fence. `StagedLossLimits` has no equivalent fixture,
> which is why rejection is the stricter and correct choice there.
>
> Recorded because it reads as an inconsistency at a glance: during this change two independent
> reviewers proposed unifying the two paths, and unifying them in either direction is a regression.
> Full reasoning in `openspec/changes/archive/2026-09-09-funding-guardrail-shape/design.md` (D4),
> and mirrored as comments at both call sites in `RiskLimitsService`.

#### Scenario: Loss fields rejected on a VarTarget payload
- GIVEN an upsert payload with `Kind = VarTarget`
- WHEN it also sets `DailyLossLimitPct`
- THEN the request is rejected with a validation error

#### Scenario: Var fields rejected on a LossLimits payload
- GIVEN an upsert payload with `Kind = LossLimits`
- WHEN it also sets `TargetVarPct`
- THEN the request is rejected with a validation error

#### Scenario: DrawdownModel rejected on a StagedLossLimits payload
- GIVEN an upsert payload with `Kind = StagedLossLimits`
- WHEN it also sets `DrawdownModel`
- THEN the request is rejected with a validation error

#### Scenario: LossLimits payload without DrawdownModel rejected
- GIVEN an upsert payload with `Kind = LossLimits` and no `DrawdownModel`
- WHEN it is upserted
- THEN the request is rejected with a validation error

### Requirement: StagedLossLimits Stage Rulebook

An `Axi` `StagedLossLimits` row MUST hold `FundingStageLimit` children: `StageOrdinal`, `StageName`,
`MaxLossLimitPct` (required), `ProfitTargetPct` (nullable), unique on `(BrokerRiskLimitsId,
StageOrdinal)`. Axi's breach consequence is quarantine with stage demotion, never termination —
rendered, not stored as a column. Stage membership tracking is out of scope (`axi-stage-tracking`).

#### Scenario: Six stage rows persisted
- GIVEN an Axi StagedLossLimits payload with six stage rows, each a distinct `StageOrdinal`
- WHEN it is upserted
- THEN all six rows persist with their `MaxLossLimitPct`

#### Scenario: Duplicate stage ordinal rejected
- GIVEN a StagedLossLimits payload with two stages sharing `StageOrdinal = 1`
- WHEN it is upserted
- THEN the request is rejected with a validation error

### Requirement: Closed-Trade Lower-Bound Disclosure

FTMO and Axi both breach on equity including unrealised open P&L; the app holds only closed trades.
Every `LossLimits`/`StagedLossLimits` readout MUST carry `BreachBasis = ClosedTradeLowerBound`,
labelled a lower bound, never the vendor's verdict. `VarTarget` MUST carry `BreachBasis = null`.
Darwinex margin call (100%)/stop-out (50%) are documentation-only here and MUST NOT produce a
`breached` flag on the `VarTarget` card.

#### Scenario: FTMO breach readout labelled a lower bound
- GIVEN an Ftmo LossLimits guardrail with a computed daily breach from closed trades
- WHEN the Risk-tab card renders
- THEN it shows `BreachBasis = ClosedTradeLowerBound` and a lower-bound label

#### Scenario: VarTarget carries no breach basis
- GIVEN a DarwinexZero VarTarget guardrail
- WHEN its readout is computed
- THEN `BreachBasis` is null and no lower-bound label is shown

### Requirement: VarTarget Percentage Validation

`TargetVarPct` and `VarFloorPct` MUST both be present, expressed as
fractions in `(0, 1]`, and MUST satisfy `VarFloorPct <= TargetVarPct`.

#### Scenario: Floor above target rejected
- GIVEN a VarTarget payload with `VarFloorPct = 0.10` and `TargetVarPct = 0.065`
- WHEN it is upserted
- THEN the request is rejected with a validation error

#### Scenario: Valid pair accepted
- GIVEN a VarTarget payload with `VarFloorPct = 0.0325` and `TargetVarPct = 0.065`
- WHEN it is upserted
- THEN the row is persisted unchanged

### Requirement: No Breach or Headroom Semantics for VarTarget

The Risk-tab card for a `VarTarget` guardrail MUST NOT compute or render a
`breached` flag, a headroom percentage, or pass/fail colouring. Missing the
target rescales leverage; it is not a breach (KB §1).

#### Scenario: VaR estimate above target
- GIVEN a VarTarget guardrail with `TargetVarPct = 0.065`
- WHEN the monthly VaR estimate is 0.09 (above target)
- THEN the card shows no breach badge and no headroom bar

### Requirement: Capital Base Denominator Labelling

Wherever a monthly VaR percentage is shown, the UI MUST explicitly label the
capital base as the portfolio's configured initial capital, including the
dollar amount, so the reader can see when it is not comparable to
Darwinex's 3.25-6.5% band (KB §5 trap 3). No separate configurable capital
base field is introduced.

#### Scenario: Estimate shown with denominator label
- GIVEN a portfolio with initial capital $50,000 and a VarTarget guardrail
- WHEN the monthly VaR estimate is displayed
- THEN the label reads, e.g., "over portfolio capital: $50,000" next to the percentage

### Requirement: Implied Multiplier with D-Leverage Context

The VarTarget card MUST show the implied Risk Engine multiplier
(`TargetVarPct / monthly VaR estimate`, KB §3) alongside the three
D-Leverage caps (16.25 / 13 / 9.75, by position duration) with an explicit
note that the app cannot resolve which cap applies because it does not
track position duration.

#### Scenario: Multiplier shown with caps and note
- GIVEN a VarTarget guardrail with an available monthly VaR estimate
- WHEN the card renders
- THEN it shows the implied multiplier, all three D-Leverage caps, and the unresolved-cap note

#### Scenario: Multiplier withheld when history is insufficient
- GIVEN a VarTarget guardrail whose broker has under 90 calendar days of daily-net history
- WHEN the card renders
- THEN it shows the insufficient-history state instead of a multiplier

### Requirement: Additive Migration to LossLimits

The migration that introduces `Kind` MUST default every existing
`BrokerRiskLimits` row to `LossLimits` with all current field values
unchanged.

#### Scenario: Existing row migrates without data loss
- GIVEN a `BrokerRiskLimits` row created before this change
- WHEN the migration runs
- THEN the row has `Kind = LossLimits` and identical field values and headroom output as before

### Requirement: Non-Destructive Reshape Migration

The migration MUST NOT delete rows, drop columns, or rewrite `Kind`; it MUST null `DrawdownModel`
only on existing `VarTarget` rows. Rollback restores the prior value only where it is preserved, not
assumed identical to a default (a `VarTarget` row may legitimately hold `Trailing`).

#### Scenario: Existing Axi LossLimits row survives unresolved
- GIVEN a pre-migration row with `FundingService = Axi`, `Kind = LossLimits`
- WHEN the migration runs
- THEN the row is unchanged and surfaces as `RulebookMismatch = true` on read
