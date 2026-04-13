using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Batches;

public record BatchStageSummaryDto(
    Guid Id,
    PipelineStageType StageType,
    PipelineStageStatus Status,
    int InputCount,
    int OutputCount,
    DateTime? RunningStartedAt
);
