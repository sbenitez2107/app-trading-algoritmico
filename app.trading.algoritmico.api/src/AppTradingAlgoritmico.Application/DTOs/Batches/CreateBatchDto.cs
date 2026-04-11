using System.ComponentModel.DataAnnotations;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Batches;

public record CreateBatchDto(
    [Required] Guid AssetId,
    [Required] Timeframe Timeframe,
    [Required] Guid BuildingBlockId,
    string? Name
);
