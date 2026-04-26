namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Reports a strategy whose MagicNumber was auto-assigned during import,
/// because its Name matched the StrategyNameHint extracted from the statement.
/// </summary>
public sealed record AutoAssignedStrategyDto(
    Guid StrategyId,
    string StrategyName,
    int MagicNumber,
    int TradeCount);
