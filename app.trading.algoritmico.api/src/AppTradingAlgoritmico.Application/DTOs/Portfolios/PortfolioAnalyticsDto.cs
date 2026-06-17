namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>
/// Combined performance KPIs for a portfolio, computed on the merged WEIGHTED trade stream of its
/// members (not by averaging per-strategy KPIs). Money fields are in the portfolio base currency;
/// percentages are decimals (0.05 = 5%). The not-naively-summable metrics (drawdown, Sharpe, CAGR,
/// profit factor, SQN, exposure, streaks, Z-score) are recomputed from the merged stream so a
/// portfolio's offsetting drawdowns and diversification are captured — which is the whole point.
/// </summary>
public sealed record PortfolioAnalyticsDto(
    // ---- Context ----
    decimal InitialCapital,
    DateTime? FirstTradeAt,
    DateTime? LastTradeAt,
    int DaysSpanned,
    int MemberCount,

    // ---- Trade counts (closed trades across all members) ----
    int TradeCount,
    int WinCount,
    int LossCount,
    int BreakevenCount,

    // ---- Money (weighted) ----
    decimal NetProfit,
    decimal GrossProfit,
    decimal GrossLoss,

    // ---- Per-trade aggregates (on weighted nets) ----
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
    decimal Ahpr,

    // ---- Streaks ----
    int MaxConsecutiveWins,
    int MaxConsecutiveLosses,
    decimal AverageConsecutiveWins,
    decimal AverageConsecutiveLosses,

    // ---- Returns ----
    decimal TotalReturn,
    decimal Cagr,
    decimal DailyAvgProfit,
    decimal MonthlyAvgProfit,
    decimal YearlyAvgProfit,

    // ---- Drawdown / risk-adjusted ----
    decimal MaxDrawdownAmount,
    decimal MaxDrawdownPercent,
    decimal ReturnDrawdownRatio,
    decimal AnnualReturnMaxDdRatio,
    int StagnationInDays,
    decimal SharpeRatio,
    decimal Sqn,
    decimal Exposure,
    decimal ZScore,
    decimal ZProbability,

    decimal FinalEquity,

    // ---- Per-member contribution breakdown ----
    IReadOnlyList<PortfolioMemberContributionDto> Members,

    // ---- Per-symbol profit composition ----
    IReadOnlyList<SymbolBreakdownDto> BySymbol);

/// <summary>Net profit and trade count contributed by one instrument (symbol) in the portfolio.</summary>
public sealed record SymbolBreakdownDto(
    string Symbol,
    decimal NetProfit,
    decimal ReturnPercent,
    int TradeCount);

/// <summary>
/// Pearson correlation matrix between member strategies' daily NET series (over the union of
/// trading days). Values in [-1, 1]; lower = better diversified. <see cref="AverageCorrelation"/>
/// is the mean of the off-diagonal pairs — a single diversification gauge.
/// </summary>
public sealed record PortfolioCorrelationDto(
    IReadOnlyList<string> Labels,
    IReadOnlyList<IReadOnlyList<decimal>> Matrix,
    int ObservationDays,
    decimal AverageCorrelation);

/// <summary>How a single member strategy contributes to its portfolio.</summary>
public sealed record PortfolioMemberContributionDto(
    Guid StrategyId,
    string StrategyName,
    decimal RawWeight,
    decimal NormalizedWeight,
    int TradeCount,
    decimal NetProfit,
    decimal WeightedNetProfit,
    decimal ContributionPercent);

/// <summary>One point on a portfolio equity curve (running combined equity + drawdown from peak).</summary>
public sealed record PortfolioEquityPointDto(
    DateTime Date,
    decimal Equity,
    decimal Drawdown,
    decimal DrawdownPercent);

/// <summary>
/// Portfolio Value-at-Risk (Historical method) over the rolling daily NET-P/L series. VaR figures
/// are positive LOSS magnitudes (in base currency) and as % of <see cref="InitialCapital"/>.
/// IMPORTANT: this is REALIZED close-by-close daily risk, NOT intraday mark-to-market — the data
/// has no intra-trade equity snapshots, so it cannot reconstruct a true intraday peak-to-trough.
/// </summary>
public sealed record PortfolioRiskDto(
    decimal InitialCapital,
    string Method,
    int WindowDays,
    int ObservationDays,
    decimal Var95,
    decimal Var95Percent,
    decimal Var99,
    decimal Var99Percent,
    decimal WorstDay,
    decimal BestDay,
    decimal MaxDrawdownPercent,
    IReadOnlyList<ServiceRiskDto> ByService,
    IReadOnlyList<ServiceGuardrailDto> Guardrails);

/// <summary>Standalone VaR contribution of one funding service (broker) within the portfolio.</summary>
public sealed record ServiceRiskDto(
    string Service,
    int StrategyCount,
    decimal NetProfit,
    decimal Var95,
    decimal Var95Percent);

/// <summary>
/// A prop-firm guardrail check for one funding service: the user-configured limits vs the portfolio's
/// risk for that service. <see cref="Configured"/> is false when no limits have been set yet.
/// </summary>
public sealed record ServiceGuardrailDto(
    string Service,
    Domain.Enums.FundingService FundingService,
    bool Configured,
    bool Verified,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    Domain.Enums.DrawdownModel? DrawdownModel,
    decimal ServiceVar95Percent,
    decimal? DailyHeadroomPct,
    bool DailyBreached);
