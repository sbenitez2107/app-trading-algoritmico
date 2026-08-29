using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>
/// Flat projection combining a portfolio's header fields with its combined analytics KPIs,
/// designed for single-request grid display. All analytics are computed on demand from the
/// current member trades (never cached) via <see cref="PortfolioAnalyticsCalculator"/>.
/// Money fields are expressed in <see cref="BaseCurrency"/>.
/// </summary>
public sealed record PortfolioSummaryDto(
    Guid Id,
    string Name,
    string Broker,
    AccountType AccountType,
    decimal InitialCapital,
    string BaseCurrency,
    int MemberCount,
    DateTime CreatedAt,
    decimal FinalEquity,
    decimal NetProfit,
    decimal TotalReturn,
    decimal ReturnDrawdownRatio,
    decimal ProfitFactor,
    decimal SharpeRatio,
    decimal Cagr,
    decimal MaxDrawdownPercent,
    decimal Sqn,
    decimal Exposure,
    int TradeCount,
    int WinCount,
    int LossCount,
    decimal WinRate,
    decimal MonthlyAvgProfit,
    decimal DailyAvgProfit);
