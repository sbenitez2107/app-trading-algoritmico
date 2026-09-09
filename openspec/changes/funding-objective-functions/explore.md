# Exploration — Funding objective-function / scoring layer

> Scope: **Darwinex Zero and FTMO only.** Axi Select scoring is deferred to change `3b`, because
> Axi's max loss is per-stage on that stage's own allocation and simulating an Axi breach needs a
> current-stage pointer that `axi-stage-tracking` has yet to deliver. Own capital is out of scope
> entirely (user decision, revisit ~2027).
>
> Predecessor: `openspec/changes/archive/2026-09-09-funding-guardrail-shape/`, whose Question D
> already settled that this capability belongs in the Application layer, is implemented in
> Infrastructure, **consumes** `PortfolioAnalyticsCalculator` rather than recomputing, and must not
> unify the destinations into one generic score.
>
> Engram topic key: `sdd/funding-objective-functions/explore`. Written to OpenSpec by the
> orchestrator — the `sdd-explore` agent type has no file-write tool.

## Current state

`PortfolioAnalyticsCalculator` is the single source of derived analytics: `Compute` (all-time KPIs
including `MaxDrawdownPercent`), `ComputeMonthlyReturns` (per-calendar-month compounding via
`AnalyticsSeries.BuildMonthlyReturns`), `ComputeVaR` (rolling 250-day historical VaR plus the
guardrail-agnostic 30-day `ComputeMonthlyVar`), and `ComputeCorrelation`.

`AnalyticsSeries.RollingWindowSums(series, windowLength)` generalises beyond 30 days, but it only
**sums** — it tracks no peak or trough, so it cannot be reused verbatim for a windowed drawdown.

`MonthlyReturnDto` carries two drawdown fields and **neither is the shape the Rating needs**:
`MaxDrawdownPercent` resets each calendar month (too narrow) and `UnderwaterPercent` references the
all-time peak (wrong reference point, not window-scoped).

`GuardrailShape.RequiredKindFor` binds `Ftmo → LossLimits`, `DarwinexZero → VarTarget`,
`Axi → StagedLossLimits`. `BreachBasis` is the established precedent for making a disclosure
structural rather than documentary.

### ⚠️ The existing daily predicate does not mean what its name says

`PortfolioService.GetRiskAsync` holds the only guardrail predicate in the codebase:

```csharp
DailyHeadroomPct: dailyLimit.Value - s.Var95Percent
DailyBreached:    s.Var95Percent > dailyLimit.Value
```

**Verified by the orchestrator against source.** This compares a **95th-percentile statistical VaR
estimate** against the limit. It does **not** answer "did any single day exceed 5%".

`DailyBreached = true` therefore means *"your daily-loss distribution has a tail above the limit, so
roughly one day in twenty would be expected to exceed it"* — a **risk-posture** statement, not a
breach. The field name asserts more than the computation supports, the same class of defect as the
VaR-comparability premise corrected in commit `d50eaa8`.

Consequence for this change: the existing predicate is **not reusable** for a breach simulation, and
placing a real breach verdict beside a field called `DailyBreached` compounds the confusion. Whether
to rename it is a decision for the proposal phase.

`MaxLossLimitPct` (FTMO's 10% total) is stored and echoed but **never evaluated anywhere**.
`GuardrailKind.VarTarget` (Darwinex Zero) has no breach semantics at all, correctly — only a
VaR-band readout.

## Question A — the MUST-NOT-RANK conflict: **no genuine conflict**

Verbatim, `openspec/specs/backtest-portfolio-analytics/spec.md:251-255`:

> "No computation in this capability MAY use a random number generator or a seed. Identical inputs
> MUST return byte-identical figures and density metrics. The capability MUST evaluate exactly one
> caller-specified group; it MUST NOT iterate over or rank candidate groups."

It is scoped by that capability's own `## Purpose` — *"Publish correlation and VaR figures for one
caller-named group."* It forbids **that** capability from internally iterating over groupings. It
says nothing about a **different** capability accepting N caller-specified groups and returning N
independent results.

**No spec delta to `backtest-portfolio-analytics` is required.** The new capability's own spec must
carry a coexistence note: it composes multiple single-group evaluations and never bypasses or loops
inside the one-group contract.

## Question B — the Darwinex Rating, and the honest shape of its output

`Rating = 22% current calendar-month return + 67% last-5-months-plus-current + 11% windowed max
drawdown`, plus track-record bonus points (+1 at 6-12 months, +2 at 12-18, +3 beyond 18).

**The scale bounds and the function mapping returns/drawdown to points are NOT FOUND** in vendor
documentation.

Consequence: the number is real but usable **only to rank the user's own groups against each other**
— never against the vendor's 75 threshold, and never as a 0-100 reading.

Design requirements that follow:
- The DTO must **not** expose `Score` or `Rating` as a bare scalar. Expose the three weighted
  components plus a **computed** non-comparability marker, following the `BreachBasis` pattern that
  makes a caveat impossible to drop at a call site.
- **The track-record bonus is not computable.** The app holds no data on a DARWIN's real
  calibration-start date. It must surface as an explicit "unknown" marker, never a silent `0` — a
  silent zero reads as "no bonus earned", which is a different and false claim.

## Question C — the 6-month windowed max drawdown

Nothing existing computes it. `RollingWindowSums` is a reusable **pattern** (dense daily series,
running accumulator) but not a reusable **function**, because drawdown needs peak/trough tracking.
Needs a small new helper mirroring the existing `ComputeEquityStats` / `MaxDrawdownPercentFromDaily`
shape, re-seeding the peak at each 6-calendar-month window start.

Same disclosure discipline as VaR applies: Darwinex measures drawdown peak-to-trough on quote data
sampled every **30 seconds**, so any daily-close figure **understates** it. This needs its own
computed disclosure property — not `BreachBasis`, which is breach-scoped rather than
drawdown-scoped.

## Question D — the FTMO pass/breach simulation

Target is the **2-Step Swing** account: max daily loss **5%**, max total drawdown **10% static**.

**Computable**: realized daily loss aggregated from closed trades against 5%, and cumulative
realized loss against 10% static from `initialCapital`.

**Not computable**: FTMO reads **equity including unrealised open P&L, continuously**. An intraday
dip that recovers before the close is invisible to a closed-trade series.

Reuse `AnalyticsSeries.BuildDailyNetSeries`, **not** the existing `GetRiskAsync` predicate — that
one answers a different question (see the warning above).

**Claim boundary, to be enforced in the DTO and any UI copy**: it may say *"would not have breached
on closed-trade daily aggregates"*. It may never say *"would have passed"*.

## Question E — contract placement and shape

New Application-layer `IFundingObjectiveScorer`, implemented in Infrastructure, consuming
`PortfolioAnalyticsCalculator` outputs.

Two structurally distinct DTOs rather than one generic `Score`:
- `DarwinexZeroRatingProxyDto` — the weighted components, plus the non-comparability and
  track-record-unknown markers.
- `FtmoBreachSimulationDto` — a per-product pass/breach verdict framed as a closed-trade lower bound.

Recommend **one service-parameterized endpoint** (`?service=darwinexzero|ftmo`) with two
independently-nullable payload properties, rather than duplicate controllers.

## Question F — sizing

Backend only: **~250-400 changed lines** for the interface, DTOs, Infrastructure implementation, the
new windowed-drawdown helper and the endpoint — **likely over the 400-line review budget once
strict-TDD tests are counted**.

Recommend one change delivered as two chained slices:
1. windowed max drawdown + the Darwinex Rating proxy
2. the FTMO breach simulation + the endpoint

**Frontend stays out of this change.** The existing guardrail surface in `portfolio-detail` and
`risk-limits-modal` is a plausible future home, not this change's scope.

## Risks

- The Darwinex Rating proxy must never be presented as vendor-comparable.
- The track-record bonus is not computable and must read "unknown", never `0`.
- The FTMO simulation must never claim "would have passed", only "did not breach on closed-trade
  aggregates".
- The 400-line review budget is likely exceeded once tests are counted — needs an explicit delivery
  decision before `sdd-apply`.
- The new capability must call `PortfolioAnalyticsCalculator` once per already-known group and
  compose at its own layer, never looping inside `backtest-portfolio-analytics`'s one-group entry
  points.
- Placing a real breach verdict beside the existing, misleadingly-named `DailyBreached` compounds an
  existing confusion. The proposal must decide whether that field is renamed here or left alone.

## Open for the proposal phase

1. Calendar-month versus fixed-day boundary for the 6-month drawdown window.
2. Single parameterized endpoint versus two (recommendation: single).
3. Whether to rename the existing `DailyBreached` / `DailyHeadroomPct` pair.
