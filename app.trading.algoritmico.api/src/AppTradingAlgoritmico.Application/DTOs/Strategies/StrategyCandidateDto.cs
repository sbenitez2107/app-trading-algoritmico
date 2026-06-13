namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

/// <summary>
/// A strategy eligible to join a portfolio. Carries both the SQX backtest KPIs (stored on the
/// strategy) and the MT4 live KPIs (computed from imported trades), so the portfolio builder grid
/// can mirror the strategies grid's two column groups: SQX (Backtest) + MT4 (Live). KPI fields are
/// null when not available (e.g. no live trades imported).
/// </summary>
public sealed record StrategyCandidateDto(
    Guid Id,
    string Name,
    string? Symbol,
    string? Timeframe,
    int? MagicNumber,
    Guid AccountId,
    string AccountName,
    string Broker,

    // ---- SQX (Backtest) — stored on the strategy ----
    decimal? TotalProfit,
    int? NumberOfTrades,
    decimal? SharpeRatio,
    decimal? ProfitFactor,
    decimal? WinningPercentage,
    decimal? Drawdown,

    // ---- MT4 (Live) — computed from imported trades ----
    int LiveTradeCount,
    decimal? LiveNetProfit,
    decimal? LiveTotalReturn,
    decimal? LiveWinRate,
    decimal? LiveProfitFactor,
    decimal? LiveMaxDrawdownPercent,
    decimal? LiveSharpeRatio);
