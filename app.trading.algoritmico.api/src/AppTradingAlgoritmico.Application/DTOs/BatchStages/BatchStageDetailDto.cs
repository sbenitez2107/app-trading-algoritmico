using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.BatchStages;

public record BatchStageDetailDto(
    Guid Id,
    PipelineStageType StageType,
    PipelineStageStatus Status,
    int InputCount,
    int OutputCount,
    string? Notes,
    DateTime? RunningStartedAt,
    DateTime CreatedAt
);
