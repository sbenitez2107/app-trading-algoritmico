namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// The density evidence published beside every backtest correlation and VaR figure — including,
/// and especially, beside a WITHHELD one: the operator has to be able to see why a figure is
/// absent, not merely that it is (design.md D4/0.1, P3).
/// <para>
/// <b>THIS DTO IS MIXED-PROVENANCE, DELIBERATELY, AND THE COST IS LEGIBILITY.</b> Its six counts
/// have TWO origins and a reader who assumes one will write the wrong assertion over them:
/// </para>
/// <list type="table">
/// <item>
/// <term><see cref="DenseDayCount"/>, <see cref="NegativeDayCount"/>,
/// <see cref="NonZeroDayCount"/>, <see cref="NegativeWindowCount"/></term>
/// <description>
/// DAY-level, measured ONCE by the Infrastructure density measurement that the gates themselves
/// consume. These are the four GATING counts: what is reported here is what gated. A
/// single-derivation assertion is legitimate over these four and ONLY these four.
/// </description>
/// </item>
/// <item>
/// <term><see cref="TradeCount"/>, <see cref="ExcludedUnscalableCount"/></term>
/// <description>
/// TRADE-level, sourced from <see cref="BacktestNetSeries"/> (the bridge). The day-level
/// measurement cannot recover either: many trades collapse into one calendar day, and it never
/// sees which rows the resizer could not scale. Do NOT extend the single-derivation assertion to
/// these two — it would fail for the wrong reason. Their own invariant is
/// <c>TradeCount - ExcludedUnscalableCount == Nets.Count</c>.
/// </description>
/// </item>
/// </list>
/// <para>
/// Only <see cref="NegativeDayCount"/> and <see cref="NegativeWindowCount"/> GATE.
/// <see cref="NonZeroDayCount"/> is disclosure and must never enter a predicate: a non-zero-day
/// share gate clears on both committed fixtures and would publish a figure measured to be exactly
/// <c>0.00</c>.
/// </para>
/// </summary>
/// <param name="TradeCount">Resized rows offered to the bridge — the denominator of the disclosure.</param>
/// <param name="ExcludedUnscalableCount">Rows excluded from the series because they could not be scaled.</param>
/// <param name="DenseDayCount">Elements in the dense first-to-last calendar-day series.</param>
/// <param name="NegativeDayCount">Days whose net is strictly negative — the daily GATING count.</param>
/// <param name="NonZeroDayCount">Days on which anything closed. Reported, never gating.</param>
/// <param name="NegativeWindowCount">Strictly-negative rolling 30-day window sums — the monthly GATING count.</param>
public sealed record SeriesDensityDto(
    int TradeCount,
    int ExcludedUnscalableCount,
    int DenseDayCount,
    int NegativeDayCount,
    int NonZeroDayCount,
    int NegativeWindowCount);
