namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Result returned by the POST /api/trading-accounts/{id}/trades/import endpoint.
/// </summary>
public sealed record TradeImportResultDto(
    int Imported,
    int Updated,
    int Skipped,
    IReadOnlyList<OrphanMagicNumberDto> Orphans,
    IReadOnlyList<AutoAssignedStrategyDto> AutoAssigned,
    IReadOnlyList<AvailableStrategyDto> AvailableStrategies,
    SnapshotDto Snapshot);
