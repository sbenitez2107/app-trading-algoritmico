## MODIFIED Requirements

### Requirement: Closed-Trade Lower-Bound Disclosure

FTMO and Axi both breach on equity including unrealised open P&L; the app holds only closed trades.
The pre-existing `PortfolioService` daily readout (`DailyBreached`, `DailyHeadroomPct`) is a VaR95
comparison against the daily loss limit, not a closed-trade lower bound of any kind — a VaR quantile
discards the tail, and a breach is exactly a tail event. Its `BreachBasis` MUST be corrected to
disclose it as a VaR comparison — a new `BreachBasis.VarQuantileComparison` value — instead of
`ClosedTradeLowerBound`. The computed `DailyBreached` value itself MUST NOT change — only its label.
On `LossLimits`, `DailyBreached` is the only breach readout, so `LossLimits` MUST carry
`BreachBasis = VarQuantileComparison`, never `ClosedTradeLowerBound`, after this change.
`StagedLossLimits` readouts, which genuinely derive from closed-trade balances rather than a VaR
comparison, MUST continue to carry `BreachBasis = ClosedTradeLowerBound`, labelled a lower bound,
never the vendor's verdict. `VarTarget` MUST continue to carry `BreachBasis = null`. Darwinex margin
call (100%)/stop-out (50%) remain documentation-only and MUST NOT produce a `breached` flag on the
`VarTarget` card.

> This is a label correction, not a value change, and not a replacement. `WorstDay`
> (`PortfolioAnalyticsCalculator.cs:358`) is not a drop-in substitute: it is the portfolio-wide worst
> day over a 250-day window of the weighted live-portfolio series, while `DailyBreached` is computed
> per service from `ServiceRiskDto.Var95Percent` — no per-service worst-day quantity exists on that
> path, and building one is a portfolio-level breach concern deferred to a future layer, not this
> change. The `ftmo-breach-simulation` capability's per-strategy simulator reads nothing from
> `DailyBreached` or its corrected label; it recomputes a genuine closed-trade-replay breach
> independently.

#### Scenario: StagedLossLimits readout stays labelled a lower bound
- GIVEN a `StagedLossLimits` guardrail with a computed daily breach from closed trades
- WHEN the Risk-tab card renders
- THEN it shows `BreachBasis = ClosedTradeLowerBound` and a lower-bound label

#### Scenario: VarTarget carries no breach basis
- GIVEN a DarwinexZero VarTarget guardrail
- WHEN its readout is computed
- THEN `BreachBasis` is null and no lower-bound label is shown

#### Scenario: Legacy DailyBreached readout is relabeled, not revalued
- GIVEN the existing `PortfolioService` daily FTMO readout for a service with `Var95Percent = 6%`
  and `DailyLossLimitPct = 5%`
- WHEN the readout is rendered after this change
- THEN `DailyBreached` remains `true` (its computed value is unchanged) and `BreachBasis` no longer
  reads `ClosedTradeLowerBound`, instead disclosing that the figure is a VaR95 comparison

#### Scenario: Relabeling is disclosed as a changelog-visible label change
- GIVEN the pre-change and post-change `BreachBasis` values for the same `PortfolioService` daily
  readout
- WHEN the two are compared
- THEN the underlying `DailyBreached` boolean and `DailyHeadroomPct` figure are identical, and only
  the `BreachBasis` label differs

#### Scenario: The web Risk-tab card does not silently drop the new label
- GIVEN a `LossLimits` readout with `BreachBasis = VarQuantileComparison`
- WHEN the Risk-tab card renders it in the web app
- THEN the card shows a non-empty, translated (EN/ES) label naming it a VaR95 comparison, not an
  empty string (`portfolio-detail.component.ts:693`'s label map previously returned `''` for any
  unknown `BreachBasis`)
