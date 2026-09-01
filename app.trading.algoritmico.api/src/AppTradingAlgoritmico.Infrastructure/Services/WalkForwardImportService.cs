using System.Security.Cryptography;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Imports one SQX Optimizer walk-forward export for one strategy.
/// <para>
/// Separate from <see cref="BacktestImportService"/> for the same reason the parsers are separate:
/// the two artifacts are different documents answering different questions. What they DO share is
/// the write shape — a fresh context per retry attempt from
/// <see cref="IBacktestDbContextFactory"/>, the decision re-derived inside the attempt from
/// committed state, one transaction, and an exception boundary that turns a persistence failure
/// into a named rejection instead of an unhandled 500 (design.md D2/D6).
/// </para>
/// </summary>
public sealed class WalkForwardImportService(
    IBacktestDbContext db,
    IBacktestDbContextFactory dbFactory,
    IWalkForwardExportParser parser) : IWalkForwardImportService
{
    public async Task<WalkForwardImportResultDto> ImportAsync(
        Guid strategyId, BacktestFileUploadDto file, CancellationToken ct)
    {
        try
        {
            return await ImportCoreAsync(strategyId, file, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            return new WalkForwardImportResultDto(
                Path.GetFileName(file.FileName), BacktestImportOutcome.Rejected, null, null,
                $"walk-forward import failed for '{Path.GetFileName(file.FileName)}': {root.GetType().Name}: {root.Message}");
        }
    }

    private async Task<WalkForwardImportResultDto> ImportCoreAsync(
        Guid strategyId, BacktestFileUploadDto file, CancellationToken ct)
    {
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await file.Content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        var contentHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        ParsedWalkForwardExportDto parsed;
        using (var parseStream = new MemoryStream(bytes))
        {
            parsed = await parser.ParseAsync(parseStream, file.FileName, ct);
        }

        if (parsed.IsRejected)
        {
            return new WalkForwardImportResultDto(
                parsed.FileName, BacktestImportOutcome.Rejected, null, null, parsed.RejectionReason);
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token => await PersistAsync(strategyId, parsed, contentHash, token),
            ct);
    }

    /// <summary>
    /// ONE attempt of the retryable write unit. Safe to invoke twice against the same starting
    /// state: the second attempt re-reads committed state, sees the same content hash, and settles
    /// as <see cref="BacktestImportOutcome.Unchanged"/> without writing.
    /// </summary>
    private async Task<WalkForwardImportResultDto> PersistAsync(
        Guid strategyId, ParsedWalkForwardExportDto parsed, string contentHash, CancellationToken ct)
    {
        await using var attemptDb = dbFactory.Create();

        var oosFromDate = OosFromDateOf(parsed);
        var deployParameters = parsed.Windows[^1].Parameters;
        var evaluationParameters = parsed.Windows[^2].Parameters;

        var existing = await attemptDb.StrategyWalkForwardExports
            .FirstOrDefaultAsync(e => e.StrategyId == strategyId, ct);

        if (existing is null)
        {
            await CreateAsync(attemptDb, strategyId, parsed, contentHash, oosFromDate, deployParameters, evaluationParameters, ct);
            return Ok(parsed, BacktestImportOutcome.Imported, oosFromDate);
        }

        if (existing.ContentHash == contentHash)
        {
            // Identical bytes. No write — re-importing the same export must not churn every window
            // row, both because it is pointless and because row churn is what makes a retried
            // attempt differ from a single one.
            return Ok(parsed, BacktestImportOutcome.Unchanged, existing.OosFromDate);
        }

        await ReplaceAsync(attemptDb, existing, parsed, contentHash, oosFromDate, deployParameters, evaluationParameters, ct);
        return Ok(parsed, BacktestImportOutcome.Replaced, oosFromDate);
    }

    /// <summary>
    /// THE boundary rule (design.md D10): the OOS start of the SECOND-TO-LAST window.
    /// <para>
    /// Positional because the user's process is positional — the parameters actually deployed come
    /// from the last row, so a run built with them is in-sample almost to the end of the data
    /// (measured: 3 of 329 trades). The parameters from the row before it move the boundary back to
    /// a point where a meaningful number of trades genuinely follow it. The parser has already
    /// refused any file with fewer than two windows, so the index is safe.
    /// </para>
    /// </summary>
    private static DateTime OosFromDateOf(ParsedWalkForwardExportDto parsed) => parsed.Windows[^2].PeriodOosStart;

    private static WalkForwardImportResultDto Ok(
        ParsedWalkForwardExportDto parsed, BacktestImportOutcome outcome, DateTime oosFromDate)
        => new(parsed.FileName, outcome, parsed.Windows.Count, oosFromDate, null);

    private static async Task CreateAsync(
        IBacktestDbContext attemptDb, Guid strategyId, ParsedWalkForwardExportDto parsed, string contentHash,
        DateTime oosFromDate, string deployParameters, string evaluationParameters, CancellationToken ct)
    {
        await using var tx = await attemptDb.Database.BeginTransactionAsync(ct);

        var export = new StrategyWalkForwardExport
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            OosFromDate = oosFromDate,
            DeployParameters = deployParameters,
            EvaluationParameters = evaluationParameters,
            ContentHash = contentHash,
            SourceFileName = parsed.FileName,
            CreatedAt = DateTime.UtcNow,
        };
        attemptDb.StrategyWalkForwardExports.Add(export);

        foreach (var window in parsed.Windows)
            attemptDb.WalkForwardWindows.Add(MapWindow(export.Id, window));

        await attemptDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task ReplaceAsync(
        IBacktestDbContext attemptDb, StrategyWalkForwardExport export, ParsedWalkForwardExportDto parsed,
        string contentHash, DateTime oosFromDate, string deployParameters, string evaluationParameters, CancellationToken ct)
    {
        await using var tx = await attemptDb.Database.BeginTransactionAsync(ct);

        // Delete flushed BEFORE the insert, same reasoning as the trade-list replace path:
        // (ExportId, RowIndex) is unique, so an interleaved batch would collide.
        var oldWindows = await attemptDb.WalkForwardWindows.Where(w => w.ExportId == export.Id).ToListAsync(ct);
        attemptDb.WalkForwardWindows.RemoveRange(oldWindows);
        await attemptDb.SaveChangesAsync(ct);

        // The export row is REUSED rather than deleted and recreated. Its identity is the strategy,
        // and every run that has been evaluated against it keeps pointing at the same row while the
        // boundary it carries is recomputed from the new file.
        export.OosFromDate = oosFromDate;
        export.DeployParameters = deployParameters;
        export.EvaluationParameters = evaluationParameters;
        export.ContentHash = contentHash;
        export.SourceFileName = parsed.FileName;
        export.UpdatedAt = DateTime.UtcNow;

        foreach (var window in parsed.Windows)
            attemptDb.WalkForwardWindows.Add(MapWindow(export.Id, window));

        await attemptDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static WalkForwardWindow MapWindow(Guid exportId, ParsedWalkForwardWindowDto w) => new()
    {
        Id = Guid.NewGuid(),
        ExportId = exportId,
        RowIndex = w.RowIndex,
        PeriodIsStart = w.PeriodIsStart,
        PeriodIsEnd = w.PeriodIsEnd,
        PeriodOosStart = w.PeriodOosStart,
        PeriodOosEnd = w.PeriodOosEnd,
        DaysIs = w.DaysIs,
        DaysOos = w.DaysOos,
        NetProfitIs = w.NetProfitIs,
        RetDdRatioIs = w.RetDdRatioIs,
        DrawdownIs = w.DrawdownIs,
        AvgTradesPerMonthIs = w.AvgTradesPerMonthIs,
        NetProfitOos = w.NetProfitOos,
        RetDdRatioOos = w.RetDdRatioOos,
        DrawdownOos = w.DrawdownOos,
        AvgTradesPerMonthOos = w.AvgTradesPerMonthOos,
        Parameters = w.Parameters,
        IsFutureWindow = w.IsFutureWindow,
        CreatedAt = DateTime.UtcNow,
    };
}
