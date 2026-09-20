using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Resolves the caller-named <see cref="BacktestRunKind"/> slot, projects both sides into
/// <see cref="CostObservation"/> with its own narrow <c>.Select(...)</c> queries (design D2 — never
/// reusing slice A's <c>DemoOpensQuery</c>/<c>BacktestOpensQuery</c>, which drop the fields this
/// capability needs), maps to slice A's <c>OpenObservation</c> for the unchanged
/// <see cref="DemoBacktestComparabilityCalculator.Measure"/> call (design D3), and composes the B1
/// root DTO from that plus <see cref="DemoBacktestCoverageCalculator"/>.
/// <para>
/// <c>NoRunForKind</c>/<c>NoDemoTrades</c> are checked, and MUST be checked, before any window or
/// coverage derivation is attempted (spec "The Decomposition Status Discloses Which Component Was
/// Producible At All").
/// </para>
/// </summary>
public sealed class CostDecompositionReadService(AppDbContext db) : ICostDecompositionReadService
{
    public async Task<CostDecompositionDto> GetAsync(Guid strategyId, BacktestRunKind kind, CancellationToken ct)
    {
        var runId = await RunIdQuery(db.BacktestRuns.AsNoTracking(), strategyId, kind).FirstOrDefaultAsync(ct);

        if (runId is null)
            return NoFigures(strategyId, kind, CostDecompositionStatus.NoRunForKind);

        var demoObservations = await DemoObservationsQuery(db.StrategyTrades.AsNoTracking(), strategyId).ToListAsync(ct);

        if (demoObservations.Count == 0)
            return NoFigures(strategyId, kind, CostDecompositionStatus.NoDemoTrades);

        var backtestObservations = await BacktestObservationsQuery(db.BacktestTrades.AsNoTracking(), runId.Value).ToListAsync(ct);

        var comparability = DemoBacktestComparabilityCalculator.Measure(
            strategyId,
            kind,
            demoObservations.ToOpenObservations(),
            backtestObservations.ToOpenObservations());

        var coverage = DemoBacktestCoverageCalculator.Compute(demoObservations, backtestObservations);

        return new CostDecompositionDto(strategyId, kind, CostDecompositionStatus.CoverageComponentOnly, comparability, coverage);
    }

    /// <summary>The (StrategyId, Kind) slot's run id, or none — never a fallback to the other slot.</summary>
    internal static IQueryable<Guid?> RunIdQuery(IQueryable<BacktestRun> runs, Guid strategyId, BacktestRunKind kind)
        => runs.Where(r => r.StrategyId == strategyId && r.Kind == kind).Select(r => (Guid?)r.Id);

    /// <summary>
    /// Demo net P/L matches slice A's own basis (<c>Profit + Commission + Swap + Taxes</c>) so the
    /// comparability figures carried on the composed DTO agree with slice A's own readout, plus the
    /// fields <see cref="OpenObservation"/> cannot carry: <c>ClosePrice</c>, <c>Size</c>, <c>Swap</c>
    /// (design D2).
    /// </summary>
    internal static IQueryable<CostObservation> DemoObservationsQuery(IQueryable<StrategyTrade> trades, Guid strategyId)
        => trades
            .Where(t => t.StrategyId == strategyId)
            .Select(t => new CostObservation(
                t.OpenTime,
                t.OpenPrice,
                t.ClosePrice,
                t.Size,
                t.Type,
                (decimal?)(t.Profit + t.Commission + t.Swap + t.Taxes),
                (decimal?)t.Swap));

    /// <summary>Backtest side has no <c>Swap</c> column — every instance carries <c>Swap = null</c> (design D2).</summary>
    internal static IQueryable<CostObservation> BacktestObservationsQuery(IQueryable<BacktestTrade> trades, Guid runId)
        => trades
            .Where(t => t.BacktestRunId == runId)
            .Select(t => new CostObservation(
                t.OpenTime,
                t.OpenPrice,
                (decimal?)t.ClosePrice,
                t.Size,
                t.Type,
                (decimal?)t.Profit,
                null));

    private static CostDecompositionDto NoFigures(Guid strategyId, BacktestRunKind kind, CostDecompositionStatus status)
    {
        var comparabilityStatus = status == CostDecompositionStatus.NoDemoTrades
            ? ComparabilityReadoutStatus.NoDemoTrades
            : ComparabilityReadoutStatus.NoRunForKind;

        return new(
            strategyId,
            kind,
            status,
            new PriceOffsetComparabilityDto(strategyId, kind, comparabilityStatus, 0, 0, 0, 0, 0, []),
            new CoverageComponentDto([]));
    }
}
