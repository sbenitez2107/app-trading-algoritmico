# Funding Guardrails Specification

## Purpose

Per-broker funding guardrails are typed by `GuardrailKind` so each funding
service is modeled only with the fields its own rulebook defines.
`LossLimits` (Other/FTMO/Axi) keeps today's breach-style fields; `VarTarget`
(Darwinex Zero) models a monthly VaR-target rulebook with no breach semantics
(`.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` §1-§3).

## Requirements

### Requirement: GuardrailKind Discriminator

Each `BrokerRiskLimits` row MUST carry a `Kind` of `LossLimits` or
`VarTarget`. The kind determines which field set is valid and how the
Risk-tab card renders.

#### Scenario: Creating a LossLimits guardrail
- GIVEN the user selects a broker other than Darwinex Zero
- WHEN they save the guardrail
- THEN the row is persisted with `Kind = LossLimits`

#### Scenario: Creating a VarTarget guardrail
- GIVEN the user selects Darwinex Zero as the broker
- WHEN they save the guardrail
- THEN the row is persisted with `Kind = VarTarget`

### Requirement: Kind Determines Valid Field Set

`LossLimits` MUST expose `DailyLossLimitPct`, `MaxLossLimitPct`,
`ProfitTargetPct`, `DrawdownModel` — unchanged from today. `VarTarget` MUST
expose only `TargetVarPct` and `VarFloorPct`. `VarHorizonDays` (30) and the
vendor's 45-day calculation window are NOT stored fields — the app cannot
honour them (no open-position series); they stay documentation-only in the
knowledge base. `RiskLimitsService.UpsertAsync` MUST reject a payload that
sets a field not valid for its kind.

#### Scenario: Loss fields rejected on a VarTarget payload
- GIVEN an upsert payload with `Kind = VarTarget`
- WHEN it also sets `DailyLossLimitPct`
- THEN the request is rejected with a validation error

#### Scenario: Var fields rejected on a LossLimits payload
- GIVEN an upsert payload with `Kind = LossLimits`
- WHEN it also sets `TargetVarPct`
- THEN the request is rejected with a validation error

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
