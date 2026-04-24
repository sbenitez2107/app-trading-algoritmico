namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Display DTO for a single StrategyTrade — returned by GET /api/strategies/{id}/trades.
/// </summary>
public sealed record StrategyTradeDto(
    Guid Id,
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
