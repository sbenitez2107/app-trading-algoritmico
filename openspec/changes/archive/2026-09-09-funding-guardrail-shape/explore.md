# Exploration — Funding destination modelling

> **Scope of this exploration is broader than this change.** It investigated modelling three
> funding destinations — **Darwinex Zero, FTMO, Axi Select** — and concluded the work is too large
> for one change. It recommends three ordered changes; `funding-guardrail-shape` is the first.
>
> **Own capital is explicitly OUT OF SCOPE** by user decision (revisit ~2027).
>
> Engram topic key: `sdd/funding-destination-modelling/explore`.
> Written to OpenSpec by the orchestrator: the `sdd-explore` agent type has no file-write tool, so
> the hybrid mirror could not be produced by the phase agent itself.

## Origin

Two read-only drift audits compared the existing funding-guardrail code and specs against the four
service rulebooks in `.agents/knowledge/imox/`, re-verified on 2026-09-08 against 81 pages of
official Darwinex documentation. Their conclusion: **the drift is incompleteness, not error.**

What is already correct and must not regress:

- `GuardrailKind` (`LossLimits` | `VarTarget`) correctly separates Darwinex Zero's normalization
  model from breach-based services. Field sets are mutually exclusive with server-side rejection,
  and tests pin the absence of breach/headroom semantics for `VarTarget`.
- There are **zero** stale hardcoded service constants in `src/`. Every limit is user-sourced.
- The known-wrong `< 15 min` D-Leverage bucket never entered the codebase, because the spec names
  the three cap values but deliberately not the duration boundaries, declaring that the app cannot
  resolve which bucket applies since it does not track position duration. That refusal to specify
  what could not be honoured made the design robust to a correction made months later.

## Current state

`BrokerRiskLimits` is one flat row per broker name carrying `GuardrailKind`, `FundingService`,
the `LossLimits` fields (`DailyLossLimitPct`, `MaxLossLimitPct`, `ProfitTargetPct`, `DrawdownModel`)
and the `VarTarget` fields (`TargetVarPct`, `VarFloorPct`).

`RiskLimitsService.ValidateKindFields` (`Infrastructure/Services/RiskLimitsService.cs:66-87`)
validates purely from `dto.Kind` and never reads `dto.FundingService`, so nothing binds
`DarwinexZero` to `VarTarget`.

Risk grouping (`PortfolioService.GetRiskAsync`, `PortfolioAnalyticsCalculator.ComputeVaR`) groups by
the free-text `Broker` string on `TradingAccount` and looks up `BrokerRiskLimits` by that same
string. **`FundingService` is carried for display only, never for routing.**

`Portfolio` has only `Broker` (string) and `AccountType` (Demo|Live). **No stage or phase concept
exists in Domain for funding purposes** — `BatchStage`, `PipelineStageStatus` and
`PipelineStageType` all belong to the unrelated SQX pipeline. Verified.

## The problems

1. **`LossLimits` is one shape serving two services it does not fit.**
   - **Axi Select** has no daily loss limit at all. Its max loss is **per stage**
     (−7% Seed/Incubation/Acceleration/Pro/Pro 500, −10% Pro M) computed on **that stage's** initial
     allocation, and breaching means **Quarantine with stage demotion, not termination**. A single
     `MaxLossLimitPct` with a `Static|Trailing` flag has neither a stage axis nor a
     non-terminal-consequence axis.
   - **FTMO is two products.** 1-Step: max daily loss 3%, 10% **trailing** drawdown recomputed at
     00:00 CE(S)T off the highest preceding day-end balance, resetting to 90% of new initial capital
     after a withdrawal. 2-Step: max daily loss 5%, 10% **static**. No field records which product a
     row represents; `DrawdownModel` is the only discriminator and its semantics are unspecified
     beyond the enum name.
2. **FTMO and Axi both breach on equity including unrealised open P&L**, while the app holds only
   closed trades. Both rulebooks state the limitation is identical across the two and its resolution
   must be shared. No spec records it.
3. **No objective function or ranking capability exists.** The only mention is a prohibition:
   `backtest-portfolio-analytics/spec.md:253-255` — "MUST NOT iterate over or rank candidate groups".
4. **`FundingService` is defined in no live spec**, only in the entity and an immutable archive.
5. **`DrawdownModel` is non-nullable** and persisted as `Static` on `VarTarget` rows, where it is
   meaningless under the rulebook.
6. **Nothing binds `FundingService.DarwinexZero` to `Kind = VarTarget`**, so a mis-entered row
   receives headroom and `breached` — the exact framing the rulebook calls modelling the wrong thing.

## Question A — shape of the guardrail split

| Option | Pros | Cons | Effort |
|---|---|---|---|
| 1. More nullable columns on the flat table | Additive, smallest diff | Axi's limits are 1-to-many (6 stages × {max loss, min days, min trades, multiplier, profit share}), not scalars. Denormalizing repeats the anti-pattern `BatchStage` was split out to avoid, and does not address FTMO's recompute logic | Low migration / high long-term complexity |
| **2. Per-service discriminated shape mirroring `GuardrailKind`, plus a normalized child table for Axi stages** | Extends a pattern the codebase already proves and tests; additive migrations; existing `LossLimits`/`VarTarget` tests stay byte-identical | More moving parts; needs a per-portfolio current-stage pointer (deferred to change 2) | Medium |
| 3. JSON / owned-entity payload per service | One migration ever | No precedent for EF owned entities; loses query and index guarantees; weakens the validation story the tests rely on | Low migration / high style-divergence risk |

**Recommendation: Option 2.** It is the only option that structurally fits Axi's per-stage
collection, and it extends rather than replaces a pattern the codebase already trusts.

> **Orchestrator verification note.** The exploration stated there is "zero precedent for
> JSON/owned-entity columns". Verified with a correction:
> - `OwnsOne`, `OwnsMany` and `ToJson` appear **nowhere** in `src/` — that half is exact.
> - JSON-as-string **does** have precedent: `StrategyGridPreset.VisibleColumnsJson` and
>   `ColumnOrderJson`.
>
> The correction **strengthens** the recommendation rather than weakening it. The single JSON
> precedent is confined to per-user UI preferences that are never queried, filtered or validated.
> Guardrail rules need all three. The pattern exists and is deliberately restricted to where those
> guarantees do not matter.

## Question B — the stage axis for Axi

The axis splits in two, and the split is load-bearing:

- **Stage rulebook** (the thresholds themselves) belongs on the new child table keyed to the
  broker's guardrail row. **This change delivers that.**
- **Stage membership** (which stage a portfolio currently sits in) belongs on `Portfolio`. Nothing
  models it today; a nullable current-stage pointer would need to be added. **Deferred to change 2.**

Whether quarantine counting (3rd quarantine → back to Seed) needs a full transition-history table
rather than a current-stage pointer is an open design decision, not resolved here.

## Question C — the unrealised-P&L gap

- The app **can** compute a closed-trade, close-to-close approximation of daily and monthly realized
  loss against static thresholds. That figure necessarily **understates** the true breach rate.
- The app **cannot** compute an intraday dip-and-recover, which both rulebooks state is invisible to
  a closed-trade series.

The precedent to reuse is `funding-guardrails/spec.md`, which declares `VarHorizonDays` and
Darwinex's 45-day window "NOT stored fields — the app cannot honour them … they stay
documentation-only in the knowledge base." Any FTMO/Axi breach figure must be labelled a
closed-trade **lower bound**, through **one shared disclosure mechanism**, not duplicated per service.

## Question D — objective function placement

A new Application-layer capability (`IFundingObjectiveScorer` + DTOs) implemented in Infrastructure,
**consuming** `PortfolioAnalyticsCalculator`'s existing return, drawdown, Sharpe, SQN, VaR and
correlation outputs rather than recomputing them.

The three destinations are **structurally incompatible and must not be unified into one generic
score**: Darwinex is a computable weighted Rating on an uncalibratable scale; FTMO is a pass/breach
simulation, not a rating; Axi is a named proxy because its Edge Score weights and input mapping are
unpublished. The capability's spec must explicitly reconcile with the existing "MUST NOT rank"
prohibition so the two coexist rather than silently conflict.

## Question E — sizing

**Too large for one change.** Recommended split, in dependency order:

| # | Change | Depends on | Contents |
|---|---|---|---|
| 1 | `funding-guardrail-shape` | — | Option 2 redesign, `Kind`↔`FundingService` binding, `FundingService` promoted to a live spec, shared unrealised-P&L disclosure. Backend only |
| 2 | `axi-stage-tracking` | 1 | Portfolio-side stage membership, transitions, quarantine, frontend wiring |
| 3 | `funding-objective-functions` | 1 | New scoring capability with its own spec reconciling the ranking prohibition |

Changes 2 and 3 must **not** be combined with 1: the migration and validation risk in 1 should be
independently reviewable from the two larger, more speculative capabilities that depend on it.

## Risks

- `FundingService` has no live spec owner today.
- Axi quarantine counting may need transition history, not just a current-stage pointer.
- Any FTMO/Axi breach figure risks being over-trusted unless the closed-trade caveat is enforced at
  the DTO level, not merely documented.
- The objective-function spec must explicitly reconcile with the existing MUST-NOT-rank requirement.
- Own capital must stay out of scope for all three changes. Note that `FundingService` has no
  `OwnCapital` member and `Other = 0` is the enum's zero default, so own-capital rows would not read
  as "unmodelled" — they would silently become `Other` and be treated as a `LossLimits` service.
  Harmless while no such portfolio exists; it must not be forgotten when the destination is added.
