namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Performance KPIs computed from imported trades and the account's initial balance.
/// All money fields are in account currency. All percentages are decimals (e.g. 0.05 = 5%).
/// </summary>
/// <remarks>
/// Sharpe Ratio is computed over a synthetic daily-return series (days with no trades
/// contribute a zero return) and annualised with sqrt(252). It is intentionally NOT
/// trade-by-trade — this matches industry convention but will diverge from SQX's value.
/// </remarks>
public sealed record StrategyAnalyticsDto(
    // ---- Context ----
    decimal InitialBalance,
    DateTime? FirstTradeAt,
    DateTime? LastTradeAt,
    int DaysSpanned,

    // ---- Trade counts ----
    int TradeCount,
    int ClosedCount,
    int OpenCount,
    int WinCount,
    int LossCount,
    int BreakevenCount,

    // ---- Money sums (net = profit + commission + swap + taxes) ----
    decimal TotalProfit,
    decimal TotalCommission,
    decimal TotalSwap,
    decimal TotalTaxes,
    decimal NetProfit,
    decimal GrossProfit,
    decimal GrossLoss,

    // ---- Per-trade aggregates ----
    decimal AverageTrade,
    decimal AverageWin,
    decimal AverageLoss,
    decimal LargestWin,
    decimal LargestLoss,
    decimal StandardDeviation,

    // ---- Ratios ----
    decimal WinRate,
    decimal ProfitFactor,
    decimal PayoutRatio,
    decimal WinsLossesRatio,
    decimal Expectancy,
    decimal RExpectancy,

    // ---- Streaks ----
    int MaxConsecutiveWins,
    int MaxConsecutiveLosses,
    decimal AverageConsecutiveWins,
    decimal AverageConsecutiveLosses,

    // ---- Returns ----
    decimal TotalReturn,
    decimal Cagr,
    decimal YearlyAvgProfit,
    decimal MonthlyAvgProfit,
    decimal DailyAvgProfit,
    decimal Ahpr,

    // ---- Drawdown / risk-adjusted ----
    decimal MaxDrawdownAmount,
    decimal MaxDrawdownPercent,
    decimal ReturnDrawdownRatio,
    decimal AnnualReturnMaxDdRatio,
    int StagnationInDays,
    decimal SharpeRatio,
    decimal Sqn,

    // ---- Other ----
    decimal Exposure,
    decimal ZScore,
    decimal ZProbability);
