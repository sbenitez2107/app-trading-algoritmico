using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Batches;

public record BatchDto(
    Guid Id,
    string? Name,
    Guid AssetId,
    string AssetName,
    string AssetSymbol,
    Timeframe Timeframe,
    Guid BuildingBlockId,
    string BuildingBlockName,
    IEnumerable<BatchStageSummaryDto> Stages,
    DateTime CreatedAt
);
