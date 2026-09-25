# Exploration mirror: FTMO 2-Step Swing breach simulation (roadmap layer 1)

> Mirror written by the `sdd-propose` executor because `sdd-explore` has no write tool.
> **Provenance caveat**: no Engram topic `sdd/ftmo-breach-simulation/explore` was found at proposal
> time. This mirror is assembled from Engram #2746 (existing `DailyBreached` defect), #2747 (10k
> account decision) and the orchestrator's verified-facts brief. Sections marked
> **[Orchestrator-verified]** were confirmed by the orchestrator against code/KB; sections marked
> **[Propose-executor check]** were re-checked while writing the proposal.

## FTMO 2-Step loss rules [Orchestrator-verified; `SERVICE_FTMO.md:59-66`, HIGH]

- Daily loss: 5% of Initial Capital. Reference = previous midnight **balance** − 5% of initial;
  day 1 uses Initial Capital. Trigger = real-time **equity** (balance + open P/L ± swap − commission).
- Reset 00:00 CE(S)T.
- Max loss 10% **static** from Initial Capital, never adjusts.
- Loss limits are stage-invariant (Challenge, Verification, funded). Profit target, minimum days and
  Standard/Swing behaviour are out of scope.

## Account size [Orchestrator-verified; Engram #2747]

10k: daily limit US$500, static floor US$9,000. Lot-grid nonlinearity bites hardest here;
`ResizedTradeSeries` counts (`RaisedToMinimumCount`, `CappedAtMaximumCount`, `UnscalableCount`) must
travel with every verdict. FTMO per-symbol min lot / step: **UNKNOWN** (`LotGrid.cs:17-18`).

## Existing `DailyBreached` [Orchestrator-verified; Engram #2746]

`PortfolioService.cs:458-459`: `DailyBreached = Var95Percent > DailyLossLimitPct`, labelled
`BreachBasis = ClosedTradeLowerBound`. VaR95 discards the tail; a breach is a tail event. The label
is also wrong — VaR is a quantile, not a lower bound. Inherited ("byte-identical to the pre-existing
behaviour").

**[Propose-executor check] — correction to the brief**: `WorstDay` at
`PortfolioAnalyticsCalculator.cs:358` is **not** a drop-in replacement. It is the **portfolio-wide**
worst day over the **250-day window** of the **weighted live-portfolio** series (`ComputeVaR`,
lines 305-321), while `DailyBreached` is computed **per service** from `ServiceRiskDto.Var95Percent`
(lines 323-338). No per-service worst day exists. It is also an end-of-day total, not
reference-point-aware and not an intraday running minimum. And the whole path runs on portfolio
member trades, not on `BacktestNetSeries`.

## Data limits [Orchestrator-verified]

- No general MAE/MFE. `BacktestTrade.RealizedRisk` = |MAE| only when `CloseType == "SL"`.
- Data timezone is in the symbol name (`XAUUSD_M1_UTC02` = UTC+2); nothing parses it. CE(S)T is
  UTC+1 winter / UTC+2 summer. Demo-trade timezone: **NOT established**.
- `BacktestNetSeries` is gross of every unmodelled cost; no swap (gold swap measured at 16.7% of
  gross); `SourcePlatform` exists but no consumer corrects MT4/MT5 commission timing.
- **[Propose-executor check]**: `BacktestNetSeries.Nets` are `DatedNet(When = CloseTime, Net)`
  (`BacktestNetSeries.cs:7-12,183`) — the open time is dropped, so the series alone cannot tell
  whether positions overlapped. `BacktestTrade.OpenTime` exists and must be read alongside.

## Asymmetry check [Propose-executor check]

"A closed-trade breach is a real breach" holds only under conditions. Counter-cases found:

1. **Concurrent positions.** At the instant a losing trade closes, real equity = balance + floating
   P/L of other open positions. If another position is floating in profit, the closed-trade balance
   can cross the floor while real equity does not. A strategy that can hold overlapping positions
   breaks the asymmetry.
2. **Day bucketing.** A wrong day boundary (winter one-hour drift, DST transition days, unknown
   data-offset behaviour) can merge losses from two real FTMO days into one simulated day, producing
   a breach that did not exist.
3. **Commission timing.** If embedded commission is charged at entry (MT4), part of a trade's loss
   belongs to the open day; for a trade spanning midnight, attributing it all to the close day can
   manufacture a day-D breach.
4. **Sizing.** The breach is real only for the simulated lot grid; with FTMO's grid UNKNOWN, a
   breach caused by min-lot pinning is conditional on the supplied grid.

Swap omission and intraday excursions both push the "safer than reality" way, as briefed.
