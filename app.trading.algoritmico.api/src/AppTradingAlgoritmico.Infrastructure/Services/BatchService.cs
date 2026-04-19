using System.IO.Compression;
using AppTradingAlgoritmico.Application.DTOs.Batches;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class BatchService(
    AppDbContext db,
    ISqxParserService sqxParser,
    IHtmlReportParserService htmlParser) : IBatchService
{
    public async Task<IEnumerable<BatchDto>> GetAllAsync(
        Guid? assetId = null, Timeframe? timeframe = null, CancellationToken ct = default)
    {
        var query = db.Batches.AsNoTracking();

        if (assetId.HasValue)
            query = query.Where(b => b.AssetId == assetId.Value);

        if (timeframe.HasValue)
            query = query.Where(b => b.Timeframe == timeframe.Value);

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

    public async Task<BatchDto> CreateAsync(
        CreateBatchDto dto, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default)
    {
        var assetExists = await db.Assets.AnyAsync(a => a.Id == dto.AssetId, ct);
        if (!assetExists)
            throw new KeyNotFoundException($"Asset {dto.AssetId} not found.");

        var bbExists = await db.BuildingBlocks.AnyAsync(bb => bb.Id == dto.BuildingBlockId, ct);
        if (!bbExists)
            throw new KeyNotFoundException($"BuildingBlock {dto.BuildingBlockId} not found.");

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

        foreach (var imported in strategies)
            builderStage.Strategies.Add(HydrateStrategy(imported));

        batch.Stages.Add(builderStage);
        db.Batches.Add(batch);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batch.Id, ct);
    }

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

        foreach (var imported in strategies)
            newStage.Strategies.Add(HydrateStrategy(imported));

        db.BatchStages.Add(newStage);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batchId, ct);
    }

    /// <summary>
    /// Resolves strategies from ZIP file or manual count. At least one must be provided.
    /// ZIP is expected to contain .sqx + .html pairs matched by base filename.
    /// </summary>
    private async Task<(int count, IList<ImportedStrategyDto> strategies)> ResolveStrategies(
        Stream? zipFile, int? strategyCount, CancellationToken ct)
    {
        if (zipFile is not null)
        {
            var imported = await ImportFromZipAsync(zipFile, ct);
            if (imported.Count == 0)
                throw new ArgumentException("ZIP file contains no valid .sqx strategy files.");
            return (imported.Count, imported);
        }

        if (strategyCount.HasValue && strategyCount.Value >= 0)
            return (strategyCount.Value, []);

        throw new ArgumentException("Either a ZIP file with strategies or a strategy count must be provided.");
    }

    private async Task<IList<ImportedStrategyDto>> ImportFromZipAsync(Stream zipStream, CancellationToken ct)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var htmlByName = archive.Entries
            .Where(e => e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => Path.GetFileNameWithoutExtension(e.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var results = new List<ImportedStrategyDto>();

        foreach (var sqxEntry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (!sqxEntry.FullName.EndsWith(".sqx", StringComparison.OrdinalIgnoreCase))
                continue;

            var baseName = Path.GetFileNameWithoutExtension(sqxEntry.Name);

            string? pseudocode;
            using (var sqxStream = sqxEntry.Open())
            {
                pseudocode = await sqxParser.ExtractPseudocodeAsync(sqxStream, ct);
            }

            ParsedReportDto? report = null;
            if (htmlByName.TryGetValue(baseName, out var htmlEntry))
            {
                using var htmlStream = htmlEntry.Open();
                report = await htmlParser.ParseAsync(htmlStream, ct);
            }

            results.Add(new ImportedStrategyDto(baseName, pseudocode, report));
        }

        return results;
    }

    private static Strategy HydrateStrategy(ImportedStrategyDto imported)
    {
        var entity = new Strategy
        {
            Name = imported.Name,
            Pseudocode = imported.Pseudocode,
            CreatedAt = DateTime.UtcNow
        };

        if (imported.Report is { } report)
        {
            entity.Symbol = report.Symbol;
            entity.Timeframe = report.Timeframe;
            entity.BacktestFrom = report.BacktestFrom;
            entity.BacktestTo = report.BacktestTo;

            StrategyKpiMapper.ApplyKpis(entity, report.Kpis);

            foreach (var mp in report.MonthlyPerformance)
            {
                entity.MonthlyPerformance.Add(new StrategyMonthlyPerformance
                {
                    Year = mp.Year,
                    Month = mp.Month,
                    Profit = mp.Profit,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return entity;
    }

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

        var prevStage = batch.Stages
            .Where(s => s.Order < stage.Order)
            .OrderByDescending(s => s.Order)
            .FirstOrDefault();

        if (prevStage is not null)
        {
            prevStage.Status = PipelineStageStatus.Pending;
            prevStage.UpdatedAt = DateTime.UtcNow;
        }

        // Only remove pipeline-only strategies — dual-linked strategies (with TradingAccountId set)
        // are preserved; their BatchStageId will be set to null by EF SetNull behavior at DB level.
        db.Strategies.RemoveRange(stage.Strategies.Where(s => s.TradingAccountId == null));
        db.BatchStages.Remove(stage);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(batchId, ct);
    }

    public async Task DeleteAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await db.Batches
            .Include(b => b.Stages).ThenInclude(s => s.Strategies)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException($"Batch {batchId} not found.");

        foreach (var stage in batch.Stages)
            // Only remove pipeline-only strategies — dual-linked strategies are preserved (SetNull at DB level)
            db.Strategies.RemoveRange(stage.Strategies.Where(s => s.TradingAccountId == null));
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
