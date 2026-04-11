namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record UpdateStrategyKpisDto(
    decimal? SharpeRatio,
    decimal? ReturnDrawdownRatio,
    decimal? WinRate,
    decimal? ProfitFactor,
    int? TotalTrades,
    decimal? NetProfit,
    decimal? MaxDrawdown
);
