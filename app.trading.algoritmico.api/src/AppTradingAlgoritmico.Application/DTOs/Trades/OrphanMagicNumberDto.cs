namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Represents a parsed trade magic number that could not be matched to any Strategy.
/// The user must assign the magic number to the correct strategy and re-import.
/// </summary>
public sealed record OrphanMagicNumberDto(
    int MagicNumber,
    string StrategyNameHint,
    int TradeCount);
