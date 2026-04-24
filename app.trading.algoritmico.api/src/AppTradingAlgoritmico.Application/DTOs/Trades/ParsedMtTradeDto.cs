namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Represents a single trade row parsed from an MT4 HTML statement.
/// Produced by the parser; not persisted directly.
/// </summary>
public sealed record ParsedMtTradeDto(
    long Ticket,
    int MagicNumber,
    string StrategyNameHint,
    string? CloseReason,
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
    bool IsOpen);
