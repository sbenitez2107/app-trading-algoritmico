# Portfolio Monthly VaR Specification

## Purpose

Estimate a monthly VaR95 proxy from the existing calendar-dense daily net
P&L series (`AnalyticsSeries.BuildDailyNetSeries`), used by `VarTarget`
guardrail readouts. This is a realized close-to-close approximation, not
Darwinex's forward-looking open-position VaR (KB §5).

> Note: `WindowedDailyNets` (`PortfolioAnalyticsCalculator.cs:394-407`) feeds
> the same calendar-dense series — including zero-filled weekend/holiday
> days — into the existing *daily* VaR percentile, which biases that number
> low. This is a known, separately-tracked defect and is out of scope here.

## Requirements

### Requirement: 30-Calendar-Day Rolling Window Aggregation

The estimator MUST aggregate the dense daily net series into rolling
30-calendar-day window sums. It MUST NOT scale the daily VaR by √t, and MUST
NOT use 21-trading-day windows — the series is calendar-day dense
(zero-filled on no-trade days), so a 30-element window already spans 30
calendar days, matching Darwinex's stated monthly horizon (KB §2, §5 trap 1).

#### Scenario: Rolling sums computed over calendar days
- GIVEN a dense daily net series covering 100 calendar days including weekends
- WHEN the estimator runs
- THEN it produces window sums over each consecutive 30-calendar-day slice, with weekends included as zero-net days

### Requirement: 5th Percentile as Monthly VaR

The monthly VaR95 estimate MUST be the 5th percentile of the window-sum
distribution, taken directly with no further distributional adjustment.

#### Scenario: Known series produces the expected percentile
- GIVEN a daily net series with a known set of 30-day window sums
- WHEN the estimator computes monthly VaR
- THEN the result equals the 5th percentile of those window sums

### Requirement: Minimum History Gate

The estimator MUST require at least 90 calendar days of dense daily-net
history before producing a monthly VaR estimate. Below that threshold, the
system MUST show an explicit "insufficient history" state instead of any
numeric estimate, band position, or multiplier.

#### Scenario: Sufficient history shows the estimate
- GIVEN a broker with 120 calendar days of daily-net history
- WHEN the VarTarget card is rendered
- THEN the monthly VaR estimate, band position, and multiplier are all shown

#### Scenario: Insufficient history is called out explicitly
- GIVEN a broker with 60 calendar days of daily-net history
- WHEN the VarTarget card is rendered
- THEN it shows an explicit "insufficient history" state and no numeric VaR, band position, or multiplier

### Requirement: Portfolio-Wide and Per-Broker Scope

The estimator MUST be computable both at portfolio level and per broker, so
it can back a `VarTarget` guardrail's readouts for any broker within a
portfolio.

#### Scenario: Multi-broker portfolio computes per broker
- GIVEN a portfolio with a Darwinex Zero broker and an FTMO broker
- WHEN monthly VaR is computed
- THEN a per-broker monthly VaR estimate is available for the Darwinex Zero broker

### Requirement: Band Position Against Floor and Target

When a monthly VaR estimate exists, the system MUST report where it falls
relative to `[VarFloorPct, TargetVarPct]` (below floor, within band, above
target) without applying breach or headroom semantics (see
`funding-guardrails`: No Breach or Headroom Semantics for VarTarget).

#### Scenario: Estimate within band
- GIVEN `VarFloorPct = 0.0325`, `TargetVarPct = 0.065`, and a monthly VaR estimate of 0.05
- WHEN the card renders
- THEN it reports the estimate as within the band, with no pass/fail colouring

### Requirement: Approximation Disclaimer

Every rendering of the monthly VaR estimate MUST carry an explicit
disclaimer that it is a realized close-to-close proxy, not Darwinex's
forward-looking open-position VaR.

#### Scenario: Disclaimer always present with the estimate
- GIVEN a VarTarget card showing a monthly VaR estimate
- WHEN it renders
- THEN the disclaimer text is shown adjacent to the number
