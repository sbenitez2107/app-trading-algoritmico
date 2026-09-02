using System.Security.Cryptography;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Imports one SQX/AlgoWizard trade list into one <c>(StrategyId, Kind)</c> slot: parse → slot
/// decision (design.md D3) → single transaction (D6) → recalibrate the touched symbol (D4/CAL-4).
/// <para>
/// Both persistence dependencies are <see cref="IBacktestDbContext"/>-shaped, so design.md D2
/// still holds — this service structurally cannot reach <c>StrategyTrades</c> or a tracked
/// <c>Strategy</c>. They differ in lifetime, and the difference is deliberate:
/// <paramref name="db"/> is the request-scoped context used to obtain the execution strategy, while
/// <paramref name="dbFactory"/> supplies a fresh context per ATTEMPT — of the retryable write unit,
/// and of the calibration upsert, which is retried for its own reason (see
/// <c>RecalibrateSymbolAsync</c>).
/// </para>
/// </summary>
public sealed class BacktestImportService(
    IBacktestDbContext db,
    IBacktestDbContextFactory dbFactory,
    ISqxTradeListParser parser) : IBacktestImportService
{
    public async Task<BacktestImportResultDto> ImportTradeListAsync(
        Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file, CancellationToken ct)
    {
        BacktestImportResultDto result;
        string? symbol;

        try
        {
            (result, symbol) = await ImportOneFileAsync(strategyId, kind, file, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The CALLER abandoned the request. That is not this file's failure and must not be
            // reported as one — let it abort, as requested.
            throw;
        }
        catch (Exception ex)
        {
            // EXCEPTION BOUNDARY. The contract is that the caller always gets an answer naming the
            // file. Without this, one non-transient persistence error (for example "String or
            // binary data would be truncated") surfaces as an unhandled 500 with nothing
            // identifying which slot failed or why — and, when the modal submits three slots, it
            // silently strands the ones that had already committed.
            return new BacktestImportResultDto(
                Path.GetFileName(file.FileName), BacktestImportOutcome.Rejected, null, null, DescribeFailure(file, ex));
        }

        if (symbol is null)
            return result;

        // CALIBRATION BOUNDARY. Calibration runs at the END, over ALL persisted SL-closed trades
        // for the symbol — never from the incoming file — so import order never changes the result
        // (CAL-4). By the time it runs the run and its trades are already COMMITTED, which is why
        // its failure is reported rather than raised or swallowed:
        //   * letting it escape turned a request whose data landed into a bare 500 — the user is
        //     told the slot failed while the rows sit in the database;
        //   * reporting Rejected would be the same lie in the other direction;
        //   * catching it silently would leave a stale per-symbol point value with nothing said.
        // So the outcome stays true and the calibration failure is named alongside it.
        try
        {
            await RecalibrateSymbolAsync(symbol, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            return result with
            {
                Reason = $"imported, but the calibration of '{symbol}' failed and its stored point value may be stale: "
                    + $"{root.GetType().Name}: {root.Message}",
            };
        }

        return result;
    }

    /// <summary>
    /// Failure reason for one file. Names the file and carries the provider's own diagnosis: a
    /// report that says only "error" tells the operator nothing about what to fix.
    /// <c>GetBaseException</c> unwraps EF's generic <c>DbUpdateException</c> wrapper down to the
    /// message that actually identifies the table and column.
    /// </summary>
    private static string DescribeFailure(BacktestFileUploadDto file, Exception ex)
    {
        var root = ex.GetBaseException();
        return $"import failed for '{Path.GetFileName(file.FileName)}': {root.GetType().Name}: {root.Message}";
    }

    private async Task<(BacktestImportResultDto Result, string? Symbol)> ImportOneFileAsync(
        Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file, CancellationToken ct)
    {
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await file.Content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        var contentHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        ParsedBacktestFileDto parsed;
        using (var parseStream = new MemoryStream(bytes))
        {
            parsed = await parser.ParseAsync(parseStream, file.FileName, ct);
        }

        if (parsed.IsRejected)
        {
            return (new BacktestImportResultDto(parsed.FileName, BacktestImportOutcome.Rejected, null, null, parsed.RejectionReason), null);
        }

        // ---- RETRY BOUNDARY ----
        // SQL Server's EnableRetryOnFailure resiliency strategy forbids ad-hoc user transactions
        // unless the whole begin-to-commit unit runs inside CreateExecutionStrategy().ExecuteAsync
        // (caught by a runtime harness, not by any provider-agnostic unit test). But wrapping is
        // only half the contract: the strategy RE-INVOKES this delegate after a transient failure,
        // so the delegate must also be IDEMPOTENT. Two things make it so, and both are
        // load-bearing (see BacktestImportRetrySafetyTests):
        //
        //  1. Every attempt runs on a FRESH context from IBacktestDbContextFactory — EF Core's own
        //     connection-resiliency guidance. Reusing the request-scoped context carries the failed
        //     attempt's change tracker into the retry: neither RollbackAsync nor disposing the
        //     transaction clears it, and SaveChangesAsync accepts changes on SUCCESS only.
        //  2. The slot decision (design.md D3) is re-derived INSIDE the attempt, from committed
        //     state. A retry after a commit that actually landed but was never acknowledged
        //     therefore settles as Unchanged instead of writing a second time.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token => await PersistOneFileAsync(strategyId, kind, parsed, contentHash, token),
            ct);
    }

    /// <summary>
    /// ONE attempt of the retryable write unit: read the slot's current state, apply the slot
    /// decision, write inside a single transaction. Safe to invoke twice in a row against the same
    /// starting database state — that is the property, not an incidental.
    /// </summary>
    private async Task<(BacktestImportResultDto Result, string? Symbol)> PersistOneFileAsync(
        Guid strategyId, BacktestRunKind kind, ParsedBacktestFileDto parsed, string contentHash, CancellationToken ct)
    {
        await using var attemptDb = dbFactory.Create();

        // THE slot decision (design.md D3). Identity is the pair, so there are exactly three
        // states and no ambiguity: the previous revision's `Conflict` outcome existed because a
        // file could claim one run by its hash and a different run by its filename at the same
        // time, which cannot happen when the caller names the slot.
        var existing = await attemptDb.BacktestRuns
            .FirstOrDefaultAsync(r => r.StrategyId == strategyId && r.Kind == kind, ct);

        if (existing is null)
        {
            await CreateNewRunAsync(attemptDb, strategyId, kind, parsed, contentHash, ct);
            return (Ok(parsed, BacktestImportOutcome.Imported), parsed.Symbol);
        }

        if (existing.ContentHash == contentHash)
        {
            // Identical bytes already occupy this slot. NO WRITE — this is the property the retry
            // safety rests on: a transient failure after a commit that actually landed re-enters
            // here, reads committed state, and settles without writing a second time.
            return (Ok(parsed, BacktestImportOutcome.Unchanged), parsed.Symbol);
        }

        await ReplaceAsync(attemptDb, existing, parsed, contentHash, ct);
        return (Ok(parsed, BacktestImportOutcome.Replaced), parsed.Symbol);
    }

    private static BacktestImportResultDto Ok(ParsedBacktestFileDto parsed, BacktestImportOutcome outcome)
        => new(parsed.FileName, outcome, parsed.Trades.Count, parsed.RejectedRows.Count, null);

    private static async Task CreateNewRunAsync(
        IBacktestDbContext attemptDb, Guid strategyId, BacktestRunKind kind,
        ParsedBacktestFileDto parsed, string contentHash, CancellationToken ct)
    {
        await using var tx = await attemptDb.Database.BeginTransactionAsync(ct);

        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = parsed.FileName,
            ContentHash = contentHash,
            StrategyId = strategyId,
            Kind = kind,
            Symbol = parsed.Symbol,
            CreatedAt = DateTime.UtcNow,
        };
        attemptDb.BacktestRuns.Add(run);

        foreach (var trade in parsed.Trades)
            attemptDb.BacktestTrades.Add(MapTrade(run.Id, trade));

        await attemptDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task ReplaceAsync(
        IBacktestDbContext attemptDb, BacktestRun run, ParsedBacktestFileDto parsed, string contentHash, CancellationToken ct)
    {
        await using var tx = await attemptDb.Database.BeginTransactionAsync(ct);

        // Deliberate deviation from design.md D6's literal "ExecuteDeleteAsync": tracked
        // RemoveRange, flushed in its own SaveChangesAsync BEFORE the new AddRange, guarantees the
        // delete lands before the insert regardless of provider batching — portable across EF
        // InMemory (used by unit tests) and SQL Server (production) without behavior drift, and it
        // is what keeps (BacktestRunId, RowIndex) unique through the swap.
        var oldTrades = await attemptDb.BacktestTrades.Where(t => t.BacktestRunId == run.Id).ToListAsync(ct);
        attemptDb.BacktestTrades.RemoveRange(oldTrades);
        await attemptDb.SaveChangesAsync(ct);

        run.ContentHash = contentHash;
        run.SourceFileName = parsed.FileName;
        run.Symbol = parsed.Symbol;
        run.UpdatedAt = DateTime.UtcNow;

        foreach (var trade in parsed.Trades)
            attemptDb.BacktestTrades.Add(MapTrade(run.Id, trade));

        await attemptDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static BacktestTrade MapTrade(Guid runId, ParsedBacktestTradeDto t) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = runId,
        RowIndex = t.RowIndex,
        Ticket = t.Ticket,
        Symbol = t.Symbol,
        Type = t.Type,
        OpenTime = t.OpenTime,
        OpenPrice = t.OpenPrice,
        Size = t.Size,
        CloseTime = t.CloseTime,
        ClosePrice = t.ClosePrice,
        Profit = t.Profit,
        Balance = t.Balance,
        SampleTypeRaw = t.SampleTypeRaw,
        Segment = t.Segment,
        SegmentIndex = t.SegmentIndex,
        CloseType = t.CloseType,
        RealizedRisk = t.RealizedRisk,
        StopLoss = t.StopLoss,
        Comment = t.Comment,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Recomputes and stores the symbol's calibration, retrying ONCE on a persistence conflict.
    /// <para>
    /// The upsert is a read-then-write against <c>SymbolCalibrations.Symbol</c>, which carries a
    /// UNIQUE index, and concurrent imports for one symbol are the normal case rather than an edge:
    /// the import modal submits Deploy and Evaluation as two independent requests naming the same
    /// strategy, therefore the same symbol. On that symbol's FIRST import both requests can observe
    /// no row and both insert; the loser's INSERT is refused, and a duplicate key is not transient,
    /// so no execution strategy retries it away.
    /// </para>
    /// <para>
    /// The retry runs on a FRESH context for the same reason the write unit does: a
    /// <c>SaveChangesAsync</c> that threw leaves the failed entity <c>Added</c> in the change
    /// tracker, so re-saving would re-issue the very INSERT that was just refused. On the second
    /// attempt the read finds the winner's row and takes the UPDATE branch, so the two writers
    /// converge on one row instead of one of them failing. A second conflict is not retried — that
    /// is no longer a race, and hiding it behind a loop would hide a real fault.
    /// </para>
    /// </summary>
    private async Task RecalibrateSymbolAsync(string symbol, CancellationToken ct)
    {
        const int MaxAttempts = 2;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await UpsertCalibrationAsync(symbol, ct);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Lost the insert race. The next attempt reads committed state and updates.
            }
        }
    }

    private async Task UpsertCalibrationAsync(string symbol, CancellationToken ct)
    {
        // A FRESH context per attempt: see RecalibrateSymbolAsync for why a retry cannot reuse one.
        await using var calibrationDb = dbFactory.Create();

        // RUN SELECTION FIRST, then trades. One run per distinct ContentHash — see
        // SymbolPointValueCalibrator.SelectDistinctContentRuns for why counting every run would
        // make SampleCount report double the sample it actually has.
        var runsForSymbol = await calibrationDb.BacktestRuns
            .AsNoTracking()
            .Where(r => r.Symbol == symbol)
            .Select(r => new { r.Id, r.ContentHash })
            .ToListAsync(ct);

        var runIds = SymbolPointValueCalibrator
            .SelectDistinctContentRuns(runsForSymbol.Select(r => (r.Id, r.ContentHash)))
            .ToList();

        var slClosedTrades = await calibrationDb.BacktestTrades
            .Where(t => t.Symbol == symbol && t.CloseType == "SL" && runIds.Contains(t.BacktestRunId))
            .ToListAsync(ct);

        var result = SymbolPointValueCalibrator.Calibrate(symbol, slClosedTrades, DateTime.UtcNow);

        var existing = await calibrationDb.SymbolCalibrations.FirstOrDefaultAsync(c => c.Symbol == symbol, ct);
        if (existing is null)
        {
            calibrationDb.SymbolCalibrations.Add(new SymbolCalibration
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                PointValue = result.PointValue,
                SampleCount = result.SampleCount,
                MinObserved = result.MinObserved,
                MaxObserved = result.MaxObserved,
                Status = result.Status,
                CalibratedAt = result.CalibratedAt,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.PointValue = result.PointValue;
            existing.SampleCount = result.SampleCount;
            existing.MinObserved = result.MinObserved;
            existing.MaxObserved = result.MaxObserved;
            existing.Status = result.Status;
            existing.CalibratedAt = result.CalibratedAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await calibrationDb.SaveChangesAsync(ct);
    }
}
