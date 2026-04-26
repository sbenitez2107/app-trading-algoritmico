namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Aggregated KPIs computed across all imported trades of a single strategy.
/// Computed in SQL — never depends on the frontend pagination window.
/// </summary>
/// <param name="TradeCount">Total number of trades, open + closed.</param>
/// <param name="ClosedCount">Number of closed trades (used as denominator of <see cref="WinRate"/>).</param>
/// <param name="WinCount">Closed trades with Profit &gt; 0.</param>
/// <param name="LossCount">Closed trades with Profit &lt; 0.</param>
/// <param name="BreakevenCount">Closed trades with Profit == 0.</param>
/// <param name="WinRate">Wins / closed (0..1). Zero when there are no closed trades.</param>
/// <param name="TotalProfit">Sum of <c>Profit</c> across every trade — broker-reported P/L only, excludes fees.</param>
/// <param name="TotalCommission">Sum of <c>Commission</c>. Typically negative.</param>
/// <param name="TotalSwap">Sum of <c>Swap</c>.</param>
/// <param name="TotalTaxes">Sum of <c>Taxes</c>.</param>
/// <param name="NetProfit">TotalProfit + TotalCommission + TotalSwap + TotalTaxes — true cash impact.</param>
public sealed record StrategyTradeSummaryDto(
    int TradeCount,
    int ClosedCount,
    int WinCount,
    int LossCount,
    int BreakevenCount,
    decimal WinRate,
    decimal TotalProfit,
    decimal TotalCommission,
    decimal TotalSwap,
    decimal TotalTaxes,
    decimal NetProfit);
