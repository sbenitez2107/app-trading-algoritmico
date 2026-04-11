using AppTradingAlgoritmico.Application.DTOs.BatchStages;
using AppTradingAlgoritmico.Application.Interfaces;
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

        return new BatchStageDetailDto(
            stage.Id, stage.StageType, stage.Status,
            stage.InputCount, stage.OutputCount, stage.Notes, stage.CreatedAt);
    }

    public async Task<BatchStageDetailDto> UpdateAsync(Guid batchId, Guid stageId, UpdateBatchStageDto dto, CancellationToken ct = default)
    {
        var stage = await db.BatchStages
            .FirstOrDefaultAsync(x => x.Id == stageId && x.BatchId == batchId, ct)
            ?? throw new KeyNotFoundException($"BatchStage {stageId} not found for batch {batchId}.");

        if (dto.Status.HasValue)
            stage.Status = dto.Status.Value;

        if (dto.Notes is not null)
            stage.Notes = dto.Notes;

        if (dto.OutputCount.HasValue)
            stage.OutputCount = dto.OutputCount.Value;

        stage.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new BatchStageDetailDto(
            stage.Id, stage.StageType, stage.Status,
            stage.InputCount, stage.OutputCount, stage.Notes, stage.CreatedAt);
    }
}
