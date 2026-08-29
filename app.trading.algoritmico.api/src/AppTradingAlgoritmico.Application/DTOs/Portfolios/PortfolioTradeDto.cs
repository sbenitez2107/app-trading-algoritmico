namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>Display DTO for a single trade in a portfolio's combined trade list — returned by GET /api/portfolios/{id}/trades. Same shape as StrategyTradeDto plus the source strategy.</summary>
public sealed record PortfolioTradeDto(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    long Ticket,
    DateTime OpenTime,
    DateTime? CloseTime,
    string Type,
    decimal Size,
    string Item,
    decimal OpenPrice,
    decimal? ClosePrice,
    decimal StopLoss,
    decimal TakeProfit,
    decimal Commission,
    decimal Taxes,
    decimal Swap,
    decimal Profit,
    string? CloseReason,
    bool IsOpen);
