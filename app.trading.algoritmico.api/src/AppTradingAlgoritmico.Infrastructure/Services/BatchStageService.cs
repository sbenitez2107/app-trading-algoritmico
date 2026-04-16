using AppTradingAlgoritmico.Application.DTOs.BatchStages;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class BatchStageService(AppDbContext db) : IBatchStageService
{
    public async Task<BatchStageDetailDto> GetDetailAsync(Guid batchId, Guid stageId, CancellationToken ct = default)
    {
        var stage = await db.BatchStages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == stageId && x.BatchId == batchId, ct)
            ?? throw new KeyNotFoundException($"BatchStage {stageId} not found for batch {batchId}.");

        return ToDto(stage);
    }

    public async Task<BatchStageDetailDto> UpdateAsync(Guid batchId, Guid stageId, UpdateBatchStageDto dto, CancellationToken ct = default)
    {
        var stage = await db.BatchStages
            .FirstOrDefaultAsync(x => x.Id == stageId && x.BatchId == batchId, ct)
            ?? throw new KeyNotFoundException($"BatchStage {stageId} not found for batch {batchId}.");

        if (dto.Status.HasValue)
        {
            var newStatus = dto.Status.Value;

            // Track RunningStartedAt
            if (newStatus == PipelineStageStatus.Running && stage.Status == PipelineStageStatus.Pending)
                stage.RunningStartedAt = DateTime.UtcNow;
            else if (newStatus == PipelineStageStatus.Pending)
                stage.RunningStartedAt = null;

            stage.Status = newStatus;
        }

        if (dto.Notes is not null)
            stage.Notes = dto.Notes;

        if (dto.InputCount.HasValue)
            stage.InputCount = dto.InputCount.Value;

        if (dto.OutputCount.HasValue)
            stage.OutputCount = dto.OutputCount.Value;

        stage.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(stage);
    }

    private static BatchStageDetailDto ToDto(Domain.Entities.BatchStage stage) =>
        new(stage.Id, stage.StageType, stage.Status,
            stage.InputCount, stage.OutputCount, stage.Notes,
            stage.RunningStartedAt, stage.CreatedAt);
}
