using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Resolves the caller-named <see cref="BacktestRunKind"/> slot, then projects both sides' opens
/// with a narrow <c>.Select(...)</c> — no entity materialization — before handing them to the pure
/// <see cref="DemoBacktestComparabilityCalculator"/> (design.md Data Flow, "Cost of evaluation").
/// At most three database commands total: the run lookup, the demo projection, and the backtest
/// projection — never a per-row query.
/// <para>
/// Takes the full <see cref="AppDbContext"/> rather than the importer's narrow
/// <c>IBacktestDbContext</c> because this read needs BOTH <c>StrategyTrades</c> (deliberately
/// invisible to that interface) and <c>BacktestRuns</c>/<c>BacktestTrades</c> — the same
/// "read is allowed to join, the WRITE path is not" trade-off <see cref="BacktestReadService"/>
/// already makes.
/// </para>
/// <para>
/// The three query builders below are <c>internal static</c> and take <see cref="IQueryable{T}"/>
/// parameters (not a concrete context type) so they can be exercised directly against a minimal
/// SQLite-backed test context for a real command-count assertion — <see cref="AppDbContext"/>
/// itself cannot be created on SQLite (unrelated configurations declare <c>nvarchar(max)</c>), the
/// same documented trade-off <c>BacktestReadinessQueryCostTests</c> already makes.
/// </para>
/// </summary>
public sealed class DemoBacktestComparabilityReadService(AppDbContext db) : IDemoBacktestComparabilityReadService
{
    public async Task<PriceOffsetComparabilityDto> GetAsync(Guid strategyId, BacktestRunKind kind, CancellationToken ct)
    {
        var runId = await RunIdQuery(db.BacktestRuns.AsNoTracking(), strategyId, kind).FirstOrDefaultAsync(ct);

        if (runId is null)
            return NoFigures(strategyId, kind, ComparabilityReadoutStatus.NoRunForKind);

        var demoOpens = await DemoOpensQuery(db.StrategyTrades.AsNoTracking(), strategyId).ToListAsync(ct);

        if (demoOpens.Count == 0)
            return NoFigures(strategyId, kind, ComparabilityReadoutStatus.NoDemoTrades);

        var backtestOpens = await BacktestOpensQuery(db.BacktestTrades.AsNoTracking(), runId.Value).ToListAsync(ct);

        return DemoBacktestComparabilityCalculator.Measure(strategyId, kind, demoOpens, backtestOpens);
    }

    /// <summary>The (StrategyId, Kind) slot's run id, or none — never a fallback to the other slot (D3).</summary>
    internal static IQueryable<Guid?> RunIdQuery(IQueryable<BacktestRun> runs, Guid strategyId, BacktestRunKind kind)
        => runs.Where(r => r.StrategyId == strategyId && r.Kind == kind).Select(r => (Guid?)r.Id);

    /// <summary>
    /// Demo net P/L is <c>Profit + Commission + Swap + Taxes</c> — the cost columns are signed and
    /// <c>Profit</c> is the pure price move — matching <see cref="AnalyticsSeries.NetOf"/>. This
    /// side's basis is <see cref="NetPlBasis.DemoProfitPlusCommissionSwapTaxes"/>, and it is
    /// deliberately NOT the backtest side's; see <see cref="BacktestOpensQuery"/>.
    /// </summary>
    internal static IQueryable<OpenObservation> DemoOpensQuery(IQueryable<StrategyTrade> trades, Guid strategyId)
        => trades
            .Where(t => t.StrategyId == strategyId)
            .Select(t => new OpenObservation(
                t.OpenTime,
                t.OpenPrice,
                t.Type,
                (decimal?)(t.Profit + t.Commission + t.Swap + t.Taxes)));

    /// <summary>
    /// A backtest <c>Profit</c> is taken exactly as imported: it ALREADY includes commission and
    /// spread, and it EXCLUDES swap, which SQX does not model. Adding the demo side's cost columns
    /// here would double-count what SQX already deducted and invent a swap the run never had —
    /// hence <see cref="NetPlBasis.BacktestProfitInclusiveOfCommissionAndSpreadExcludingSwap"/>.
    /// </summary>
    internal static IQueryable<OpenObservation> BacktestOpensQuery(IQueryable<BacktestTrade> trades, Guid runId)
        => trades
            .Where(t => t.BacktestRunId == runId)
            .Select(t => new OpenObservation(t.OpenTime, t.OpenPrice, t.Type, (decimal?)t.Profit));

    private static PriceOffsetComparabilityDto NoFigures(Guid strategyId, BacktestRunKind kind, ComparabilityReadoutStatus status)
        => new(strategyId, kind, status, 0, 0, 0, 0, 0, []);
}
