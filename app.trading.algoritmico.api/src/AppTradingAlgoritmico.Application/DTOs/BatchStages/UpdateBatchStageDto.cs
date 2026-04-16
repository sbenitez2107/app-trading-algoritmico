using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.BatchStages;

public record UpdateBatchStageDto(
    PipelineStageStatus? Status,
    string? Notes,
    int? InputCount,
    int? OutputCount
);
