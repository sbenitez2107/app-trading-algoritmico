using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Read model for imported backtest data. Takes the full <see cref="AppDbContext"/> rather than
/// the importer's narrow <c>IBacktestDbContext</c> because showing WHOSE run a row is requires
/// joining <c>Strategies</c> — and the point of the narrow interface is to keep that join out of
/// the WRITE path, not to forbid it everywhere. Splitting the two is what lets design.md D2 stay
/// literally true instead of being widened for a display concern.
/// </summary>
public sealed class BacktestReadService(AppDbContext db) : IBacktestReadService
{
    public async Task<PagedResult<BacktestRunDto>> GetRunsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.BacktestRuns.AsNoTracking();
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                db.Strategies.AsNoTracking(),
                r => r.StrategyId,
                s => s.Id,
                (r, s) => new BacktestRunDto(
                    r.Id,
                    r.SourceFileName,
                    r.Symbol,
                    s.Id,
                    s.Name,
                    r.Kind,
                    r.Trades.Count,
                    r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<BacktestRunDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<BacktestTradeDto>> GetTradesByRunAsync(
        Guid runId, BacktestSegment? segment, int page, int pageSize, CancellationToken ct)
    {
        var query = db.BacktestTrades.AsNoTracking().Where(t => t.BacktestRunId == runId);
        if (segment is not null)
            query = query.Where(t => t.Segment == segment);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new BacktestTradeDto(
                t.Id, t.RowIndex, t.Ticket, t.Symbol, t.Type, t.OpenTime, t.OpenPrice, t.Size,
                t.CloseTime, t.ClosePrice, t.Profit, t.Balance, t.SampleTypeRaw, t.Segment,
                t.SegmentIndex, t.CloseType, t.RealizedRisk, t.StopLoss, t.Comment))
            .ToListAsync(ct);

        return new PagedResult<BacktestTradeDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<SymbolCalibrationDto>> GetCalibrationsAsync(CancellationToken ct)
        => await db.SymbolCalibrations
            .AsNoTracking()
            .OrderBy(c => c.Symbol)
            .Select(c => new SymbolCalibrationDto(
                c.Symbol, c.PointValue, c.SampleCount, c.MinObserved, c.MaxObserved, c.Status, c.CalibratedAt))
            .ToListAsync(ct);

    public async Task<StrategyBacktestsDto> GetByStrategyAsync(Guid strategyId, CancellationToken ct)
    {
        var runs = await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.StrategyId == strategyId)
            .Select(r => new BacktestRunSummaryDto(
                r.Id, r.SourceFileName, r.Symbol, r.Kind, r.Trades.Count, r.CreatedAt))
            .ToListAsync(ct);

        var export = await db.StrategyWalkForwardExports
            .AsNoTracking()
            .Where(e => e.StrategyId == strategyId)
            .Select(e => new WalkForwardExportSummaryDto(
                e.Id, e.SourceFileName, e.OosFromDate, e.Windows.Count,
                e.DeployParameters, e.EvaluationParameters, e.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return new StrategyBacktestsDto(
            strategyId,
            runs.FirstOrDefault(r => r.Kind == BacktestRunKind.Deploy),
            runs.FirstOrDefault(r => r.Kind == BacktestRunKind.Evaluation),
            export);
    }
}
