using AppTradingAlgoritmico.Application.DTOs.Batches;
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
        var query = db.Batches
            .AsNoTracking()
            .Include(b => b.Asset)
            .Include(b => b.BuildingBlock)
            .Include(b => b.Stages)
            .AsQueryable();

        if (assetId.HasValue)
            query = query.Where(b => b.AssetId == assetId.Value);

        if (timeframe.HasValue)
            query = query.Where(b => b.Timeframe == timeframe.Value);

        var batches = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        return batches.Select(ToDto);
    }

    public async Task<BatchDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .AsNoTracking()
            .Include(b => b.Asset)
            .Include(b => b.BuildingBlock)
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"Batch {id} not found.");

        return ToDto(batch);
    }

    /// <summary>
    /// Creates a new batch in the Builder stage with strategies parsed from the uploaded ZIP.
    /// </summary>
    public async Task<BatchDto> CreateAsync(CreateBatchDto dto, Stream zipFile, CancellationToken ct = default)
    {
        // Validate references
        var assetExists = await db.Assets.AnyAsync(a => a.Id == dto.AssetId, ct);
        if (!assetExists)
            throw new KeyNotFoundException($"Asset {dto.AssetId} not found.");

        var bbExists = await db.BuildingBlocks.AnyAsync(bb => bb.Id == dto.BuildingBlockId, ct);
        if (!bbExists)
            throw new KeyNotFoundException($"BuildingBlock {dto.BuildingBlockId} not found.");

        // Parse strategies from ZIP
        var parsedStrategies = await parser.ParseZipAsync(zipFile, ct);
        if (parsedStrategies.Count == 0)
            throw new ArgumentException("ZIP file contains no valid .sqx strategy files.");

        // Create batch
        var batch = new Batch
        {
            Name = dto.Name,
            AssetId = dto.AssetId,
            Timeframe = dto.Timeframe,
            BuildingBlockId = dto.BuildingBlockId,
            CreatedAt = DateTime.UtcNow
        };

        // Create Builder stage with strategies
        var builderStage = new BatchStage
        {
            StageType = PipelineStageType.Builder,
            Status = PipelineStageStatus.InProgress,
            InputCount = parsedStrategies.Count,
            OutputCount = parsedStrategies.Count,
            Order = (int)PipelineStageType.Builder,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var parsed in parsedStrategies)
        {
            builderStage.Strategies.Add(new Strategy
            {
                Name = parsed.Name,
                Pseudocode = parsed.Pseudocode,
                CreatedAt = DateTime.UtcNow
            });
        }

        batch.Stages.Add(builderStage);
        db.Batches.Add(batch);
        await db.SaveChangesAsync(ct);

        // Reload with navigation properties
        return await GetByIdAsync(batch.Id, ct);
    }

    /// <summary>
    /// Advances a batch to the next pipeline stage with strategies from the uploaded ZIP.
    /// </summary>
    public async Task<BatchDto> AdvanceAsync(Guid batchId, Stream zipFile, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .Include(b => b.Stages)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        // Find the latest stage
        var currentStage = batch.Stages.OrderByDescending(s => s.Order).FirstOrDefault()
            ?? throw new InvalidOperationException("Batch has no stages.");

        // Determine next stage type
        var nextStageType = GetNextStageType(currentStage.StageType);
        if (nextStageType is null)
            throw new InvalidOperationException($"Batch is already at the final stage ({currentStage.StageType}).");

        // Check no duplicate stage
        if (batch.Stages.Any(s => s.StageType == nextStageType.Value))
            throw new InvalidOperationException($"Batch already has a {nextStageType.Value} stage.");

        // Parse strategies from ZIP
        var parsedStrategies = await parser.ParseZipAsync(zipFile, ct);
        if (parsedStrategies.Count == 0)
            throw new ArgumentException("ZIP file contains no valid .sqx strategy files.");

        // Mark current stage as completed
        currentStage.Status = PipelineStageStatus.Completed;
        currentStage.OutputCount = parsedStrategies.Count;
        currentStage.UpdatedAt = DateTime.UtcNow;

        // Create new stage
        var newStage = new BatchStage
        {
            BatchId = batchId,
            StageType = nextStageType.Value,
            Status = PipelineStageStatus.InProgress,
            InputCount = parsedStrategies.Count,
            OutputCount = parsedStrategies.Count,
            Order = (int)nextStageType.Value,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var parsed in parsedStrategies)
        {
            newStage.Strategies.Add(new Strategy
            {
                Name = parsed.Name,
                Pseudocode = parsed.Pseudocode,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.BatchStages.Add(newStage);
        await db.SaveChangesAsync(ct);

        // Reload with navigation properties
        return await GetByIdAsync(batchId, ct);
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

    private static BatchDto ToDto(Batch b)
    {
        return new BatchDto(
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
                .Select(s => new BatchStageSummaryDto(s.Id, s.StageType, s.Status, s.InputCount, s.OutputCount)),
            b.CreatedAt);
    }
}
