using AppTradingAlgoritmico.Application.DTOs.Batches;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class BatchService(AppDbContext db, ISqxParserService parser) : IBatchService
{
    public async Task<IEnumerable<BatchDto>> GetAllAsync(
        Guid? assetId = null, Timeframe? timeframe = null, CancellationToken ct = default)
    {
        var query = db.Batches.AsNoTracking();

        if (assetId.HasValue)
            query = query.Where(b => b.AssetId == assetId.Value);

        if (timeframe.HasValue)
            query = query.Where(b => b.Timeframe == timeframe.Value);

        // Project directly to DTO to avoid cartesian explosion from Include on Stages.
        // This generates efficient SQL with only needed columns (no pseudocode/xmlconfig).
        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BatchDto(
                b.Id,
                b.Name,
                b.AssetId,
                b.Asset.Name,
                b.Asset.Symbol,
                b.Timeframe,
                b.BuildingBlockId,
                b.BuildingBlock.Name,
                b.Stages
                    .OrderBy(s => s.Order)
                    .Select(s => new BatchStageSummaryDto(s.Id, s.StageType, s.Status, s.InputCount, s.OutputCount, s.RunningStartedAt, s.UpdatedAt)),
                b.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<BatchDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await db.Batches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BatchDto(
                b.Id,
                b.Name,
                b.AssetId,
                b.Asset.Name,
                b.Asset.Symbol,
                b.Timeframe,
                b.BuildingBlockId,
                b.BuildingBlock.Name,
                b.Stages
                    .OrderBy(s => s.Order)
                    .Select(s => new BatchStageSummaryDto(s.Id, s.StageType, s.Status, s.InputCount, s.OutputCount, s.RunningStartedAt, s.UpdatedAt)),
                b.CreatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Batch {id} not found.");

        return dto;
    }

    /// <summary>
    /// Creates a new batch in the Builder stage.
    /// Either a ZIP with .sqx files or a manual strategy count must be provided.
    /// </summary>
    public async Task<BatchDto> CreateAsync(
        CreateBatchDto dto, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default)
    {
        var assetExists = await db.Assets.AnyAsync(a => a.Id == dto.AssetId, ct);
        if (!assetExists)
            throw new KeyNotFoundException($"Asset {dto.AssetId} not found.");

        var bbExists = await db.BuildingBlocks.AnyAsync(bb => bb.Id == dto.BuildingBlockId, ct);
        if (!bbExists)
            throw new KeyNotFoundException($"BuildingBlock {dto.BuildingBlockId} not found.");

        // Parse ZIP or use manual count
        var (count, strategies) = await ResolveStrategies(zipFile, strategyCount, ct);

        var batch = new Batch
        {
            Name = dto.Name,
            AssetId = dto.AssetId,
            Timeframe = dto.Timeframe,
            BuildingBlockId = dto.BuildingBlockId,
            CreatedAt = DateTime.UtcNow
        };

        var builderStage = new BatchStage
        {
            StageType = PipelineStageType.Builder,
            Status = PipelineStageStatus.Pending,
            InputCount = count,
            OutputCount = count,
            Order = (int)PipelineStageType.Builder,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var s in strategies)
        {
            builderStage.Strategies.Add(new Strategy
            {
                Name = s.Name,
                Pseudocode = s.Pseudocode,
                CreatedAt = DateTime.UtcNow
            });
        }

        batch.Stages.Add(builderStage);
        db.Batches.Add(batch);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batch.Id, ct);
    }

    /// <summary>
    /// Advances a batch to the next pipeline stage.
    /// Either a ZIP with .sqx files or a manual strategy count must be provided.
    /// </summary>
    public async Task<BatchDto> AdvanceAsync(
        Guid batchId, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        var currentStage = batch.Stages.OrderByDescending(s => s.Order).FirstOrDefault()
            ?? throw new InvalidOperationException("Batch has no stages.");

        var nextStageType = GetNextStageType(currentStage.StageType)
            ?? throw new InvalidOperationException($"Batch is already at the final stage ({currentStage.StageType}).");

        if (batch.Stages.Any(s => s.StageType == nextStageType))
            throw new InvalidOperationException($"Batch already has a {nextStageType} stage.");

        var (count, strategies) = await ResolveStrategies(zipFile, strategyCount, ct);

        // Demo stage must be completed explicitly before advancing to Live
        if (currentStage.StageType == PipelineStageType.Demo)
        {
            if (currentStage.Status != PipelineStageStatus.Completed)
                throw new InvalidOperationException("The Demo stage must be marked as completed before advancing to Live.");
            currentStage.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            currentStage.Status = PipelineStageStatus.Completed;
            currentStage.OutputCount = count;
            currentStage.UpdatedAt = DateTime.UtcNow;
        }

        // Create new stage
        var newStage = new BatchStage
        {
            BatchId = batchId,
            StageType = nextStageType,
            Status = PipelineStageStatus.Pending,
            InputCount = count,
            OutputCount = count,
            Order = (int)nextStageType,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var s in strategies)
        {
            newStage.Strategies.Add(new Strategy
            {
                Name = s.Name,
                Pseudocode = s.Pseudocode,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.BatchStages.Add(newStage);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batchId, ct);
    }

    /// <summary>
    /// Resolves strategies from ZIP file or manual count. At least one must be provided.
    /// </summary>
    private async Task<(int count, IList<ParsedStrategyDto> strategies)> ResolveStrategies(
        Stream? zipFile, int? strategyCount, CancellationToken ct)
    {
        if (zipFile is not null)
        {
            var parsed = await parser.ParseZipAsync(zipFile, ct);
            if (parsed.Count == 0)
                throw new ArgumentException("ZIP file contains no valid .sqx strategy files.");
            return (parsed.Count, parsed);
        }

        if (strategyCount.HasValue && strategyCount.Value >= 0)
            return (strategyCount.Value, []);

        throw new ArgumentException("Either a ZIP file with strategies or a strategy count must be provided.");
    }

    /// <summary>
    /// Deletes a stage and reverts to the previous one. Only if stage is not Completed.
    /// Cannot delete the Builder stage (first stage).
    /// </summary>
    public async Task<BatchDto> RollbackStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .Include(b => b.Stages).ThenInclude(s => s.Strategies)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        var stage = batch.Stages.FirstOrDefault(s => s.Id == stageId)
            ?? throw new KeyNotFoundException($"Stage {stageId} not found.");

        if (stage.StageType == PipelineStageType.Builder)
            throw new InvalidOperationException("Cannot delete the Builder stage.");

        // Find previous stage and revert it from Completed to Pending
        var prevStage = batch.Stages
            .Where(s => s.Order < stage.Order)
            .OrderByDescending(s => s.Order)
            .FirstOrDefault();

        if (prevStage is not null)
        {
            prevStage.Status = PipelineStageStatus.Pending;
            prevStage.UpdatedAt = DateTime.UtcNow;
        }

        // Remove the stage and its strategies
        db.Strategies.RemoveRange(stage.Strategies);
        db.BatchStages.Remove(stage);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batchId, ct);
    }

    /// <summary>
    /// Deletes an entire batch including all its stages and strategies.
    /// </summary>
    public async Task DeleteAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .Include(b => b.Stages).ThenInclude(s => s.Strategies)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        foreach (var stage in batch.Stages)
            db.Strategies.RemoveRange(stage.Strategies);
        db.BatchStages.RemoveRange(batch.Stages);
        db.Batches.Remove(batch);
        await db.SaveChangesAsync(ct);
    }

    private static PipelineStageType? GetNextStageType(PipelineStageType current)
    {
        return current switch
        {
            PipelineStageType.Builder => PipelineStageType.Retester,
            PipelineStageType.Retester => PipelineStageType.Optimizer,
            PipelineStageType.Optimizer => PipelineStageType.Demo,
            PipelineStageType.Demo => PipelineStageType.Live,
            PipelineStageType.Live => null,
            _ => null
        };
    }

}
