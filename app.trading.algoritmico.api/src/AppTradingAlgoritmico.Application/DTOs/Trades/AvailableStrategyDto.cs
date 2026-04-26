namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Lightweight strategy summary returned alongside an MT trade import result so the
/// frontend can render a "Assign to strategy" combo for orphan magic numbers without
/// a second round-trip.
/// </summary>
public sealed record AvailableStrategyDto(
    Guid Id,
    string Name,
    int? MagicNumber);
