namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record StrategyDto(
    Guid Id,
    string Name,
    string? Pseudocode,
    decimal? SharpeRatio,
    decimal? ReturnDrawdownRatio,
    decimal? WinRate,
    decimal? ProfitFactor,
    int? TotalTrades,
    decimal? NetProfit,
    decimal? MaxDrawdown,
    DateTime CreatedAt
);
