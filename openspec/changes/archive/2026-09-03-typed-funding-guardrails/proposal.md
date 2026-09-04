# Proposal: Typed Funding Guardrails (loss-limits vs var-target)

## Intent

The Risk tab models every funding service as a prop-firm breach ruleset (daily loss, max loss, profit target, drawdown model). Darwinex Zero has **none of those** — its only constraint is a monthly VaR target, and the platform **rescales leverage** toward that target instead of terminating the account (`.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` §1-§3). Today the user cannot configure Darwinex Zero honestly: inventing a daily-loss number produces a green headroom bar that means nothing. Guardrails become **discriminated by kind** so each service is modelled with the fields its rulebook actually defines.

## Scope

### In Scope

- `GuardrailKind` discriminator: `LossLimits` (Other/FTMO/Axi) and `VarTarget` (Darwinex Zero)
- `var-target` fields: `TargetVarPct`, `VarFloorPct`, `VarHorizonDays`, `VarWindowDays` (vendor reference)
- Monthly VaR estimator: rolling **30-calendar-day** sums of the existing daily net series, 5th percentile taken directly (no √t scaling — KB §5 trap 1)
- `var-target` readouts: monthly VaR estimate, band position vs `[floor, target]`, **implied Risk Engine multiplier** = `TargetVar / StrategyVar` (KB §3), D-Leverage caps shown as context
- Kind-aware modal: field set switches with the selected funding service
- Kind-aware backend validation in `RiskLimitsService.UpsertAsync` (today: only a non-empty broker check)
- Migration: additive, existing rows → `LossLimits`
- Honest-approximation labelling everywhere the VaR estimate appears

### Out of Scope

- **No `breached` / `headroom` semantics for `var-target`** — conceptually meaningless (KB §1)
- Making `MaxLossLimitPct` / `ProfitTargetPct` / `DrawdownModel` computational (still display-only)
- Reproducing Darwinex's open-position prospective VaR — impossible from realized closes (KB §5 trap 2)
- Applying a specific D-Leverage cap (requires position duration; not modelled)
- Per-portfolio guardrails — limits stay globally keyed by broker string
- FluentValidation adoption; validation stays inline in the service
- Hardcoding vendor numbers as domain constants (`INDEX.md` §5) — they are prefilled suggestions the user confirms via `Verified`

## Capabilities

### New Capabilities

- `funding-guardrails`: typed per-broker guardrail configuration and per-kind Risk-tab readouts
- `portfolio-monthly-var`: 30-calendar-day rolling VaR95 estimator, portfolio-wide and per broker

### Modified Capabilities

- None (no existing spec covers risk limits or portfolio analytics)

## Approach

**Domain.** `BrokerRiskLimits` gains `Kind`. Recommended persistence is EF **TPH**: one table, `Kind` discriminator, derived `LossLimitsGuardrail` / `VarTargetGuardrail` types — a genuinely typed domain model with a purely additive migration. Final mapping choice belongs to `sdd-design`.

**Estimator.** `PortfolioAnalyticsCalculator` already builds a **calendar-day dense** series (`AnalyticsSeries.cs:46-60` fills no-trade days with `0`). Monthly VaR therefore aggregates **30 calendar days**, which also matches Darwinex's stated 30-day horizon. Over the 250-day window that is ~221 overlapping but only ~8 independent windows — the tail estimate is soft and the UI must say so.

**Wire contract.** `ServiceGuardrailDto` gains `kind`; loss-limit fields (`dailyHeadroomPct`, `dailyBreached`) are populated only for `LossLimits`, var fields only for `VarTarget`. Angular maps this to a TS discriminated union in `portfolio.service.ts` and the template switches on `kind`.

**Capital base.** The app's VaR% is over portfolio initial capital, which may differ from the Darwinex account size (KB §5 trap 3). Proposed: optional `VarCapitalBase` on the var-target guardrail; when null, the denominator is labelled explicitly in the UI. See open question 1.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Enums/GuardrailKind.cs` | New | `LossLimits = 0`, `VarTarget = 1` |
| `Domain/Entities/BrokerRiskLimits.cs` | Modified | `Kind` + var-target fields |
| `Infrastructure/Persistence/Configurations/BrokerRiskLimitsConfiguration.cs` | Modified | Discriminator; var pcts `decimal(9,6)`; `Broker` unique index unchanged |
| `Infrastructure/Persistence/Migrations/` | New | Additive; `Kind` NOT NULL DEFAULT 0 |
| `Application/DTOs/Portfolios/PortfolioAnalyticsDto.cs:148-159` | Modified | `ServiceGuardrailDto` gains `kind` + var block |
| `Application/DTOs/RiskLimits/` | Modified | `BrokerRiskLimitsDto` / `Upsert…` gain kind + var fields |
| `Infrastructure/Services/RiskLimitsService.cs:27-53` | Modified | Kind-aware validation |
| `Infrastructure/Services/PortfolioService.cs:374-390` | Modified | Branch by kind; no headroom for `VarTarget` |
| `Infrastructure/Services/PortfolioAnalyticsCalculator.cs:250-266` | Modified | Monthly VaR per broker |
| `Infrastructure/Services/AnalyticsSeries.cs` | Modified | Rolling-window aggregation helper |
| `web/.../portfolio.service.ts:11-21,160-216` | Modified | `GuardrailKind` enum + discriminated union types |
| `web/.../portfolio-detail.component.ts:133-141,561-599` | Modified | Kind-driven `limitsForm`, `openLimits`, `saveLimits` |
| `web/.../portfolio-detail.component.html:242-301,421-500` | Modified | Per-kind guardrail card and modal field set |
| `web/src/assets/i18n/{en,es}.json` | Modified | Var-target labels + approximation disclaimers |
| `tests/AppTradingAlgoritmico.UnitTests/` | New | Monthly-VaR estimator + kind validation tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| **False confidence** — app estimate read as the real DARWIN VaR | High | Label as "estimate (realized close-to-close proxy)"; never "your DARWIN VaR"; show the methodology contrast (KB §5) inline; no pass/fail colouring |
| Thin tail — ~8 independent 30-day windows | High | Show observation count; suppress the estimate below a minimum-history threshold (open question 2) |
| User reads "below floor" as safe | Med | Surface KB §3: under-risking instructs the engine to scale **up** |
| Multiplier readout implies precision | Med | Mark indicative; `f`, rebalancing cadence and measurement methodology are undocumented (KB §4) |
| Capital-base mismatch invalidates the % | Med | Explicit denominator label; optional `VarCapitalBase` |
| Global broker keying surprises the user | Low | Modal states the edit applies to every portfolio using that broker |
| Vendor numbers drift | Med | Values are user-confirmed via `Verified`, never hardcoded; prefills cite the KB retrieval date |
| Frontend breaks on the new DTO shape | Low | Additive fields; `kind` defaults to `LossLimits` for every existing row |

## Rollback Plan

1. Revert frontend + backend commits.
2. Run the migration `Down` — drops `Kind` and the var-target columns. Pre-existing rows are untouched (all were `LossLimits`).
3. Any Darwinex Zero guardrail created after the change loses its var-target values on rollback; loss-limit rows lose nothing.
4. Frontend-only rollback is safe: the API returns extra fields the old client ignores.

## Dependencies

- `.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` (retrieved 2026-08-14) — the sole source for vendor values
- Existing daily net series (`AnalyticsSeries.BuildDailyNetSeries`)
- Existing `api/risk-limits` GET/PUT contract

## Success Criteria

- [ ] Selecting Darwinex Zero shows only target VaR / floor / horizon / window — no daily loss, max loss, profit target or drawdown model
- [ ] Selecting FTMO / Axi / Other shows exactly today's field set, unchanged
- [ ] The Darwinex Zero card renders **no** headroom bar and **no** breach badge
- [ ] The card shows monthly VaR estimate, band position, implied multiplier, and an explicit approximation disclaimer
- [ ] Existing `BrokerRiskLimits` rows load as `LossLimits` with identical values and identical headroom output
- [ ] `UpsertAsync` rejects var fields on a `LossLimits` payload and loss fields on a `VarTarget` payload
- [ ] `UpsertAsync` rejects `VarFloorPct > TargetVarPct` and percentages outside `(0, 1]`
- [ ] Monthly VaR unit test: a known daily series produces the expected 30-day rolling 5th percentile
- [ ] `dotnet format` and `pnpm format` pass clean

## Proposal question round

Interactive questioning was unavailable in this phase. These need user answers before `sdd-design`:

1. **Capital base.** Add `VarCapitalBase` on the var-target guardrail now, or defer and only label the denominator as "portfolio initial capital"?
2. **Minimum history.** Below how much history should the monthly VaR estimate be suppressed rather than shown? Suggested: 90 calendar days (~3 independent windows).
3. **`VarWindowDays` (45).** Store it at all? The app cannot honour it — it has no open-position risk series — so it would be vendor documentation shown in the UI, not an estimator input.
4. **Multiplier display.** Show the implied multiplier as a bare number, or alongside the D-Leverage caps (16.25 / 13 / 9.75) as context the app cannot resolve without position duration?

Assumptions taken meanwhile: monthly VaR aggregates 30 **calendar** days; loss-limit behaviour is byte-identical to today; guardrails stay globally keyed by broker.
