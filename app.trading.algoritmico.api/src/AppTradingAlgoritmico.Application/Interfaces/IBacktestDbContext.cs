using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Narrow persistence surface for the SQX backtest importer. It exposes no <c>StrategyTrades</c>
/// DbSet and no <c>Strategies</c> DbSet, and the entities it does expose declare no navigation
/// property to <c>Strategy</c> — so code written against this interface has no way to reach live
/// trade storage or to mutate a strategy. See design.md D2 and
/// <c>BacktestDbContextIsolationTests</c>.
/// <para>
/// <see cref="IAsyncDisposable"/> because a retried unit of work owns a context created by
/// <see cref="IBacktestDbContextFactory"/> and must dispose it — see that interface for why the
/// retry cannot reuse a shared one. Every <c>DbContext</c> already implements it, so this widens
/// nothing that was not already there.
/// </para>
/// </summary>
public interface IBacktestDbContext : IAsyncDisposable
{
    DbSet<BacktestRun> BacktestRuns { get; }
    DbSet<BacktestTrade> BacktestTrades { get; }
    DbSet<SymbolCalibration> SymbolCalibrations { get; }
    DbSet<StrategyWalkForwardExport> StrategyWalkForwardExports { get; }
    DbSet<WalkForwardWindow> WalkForwardWindows { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);

    DatabaseFacade Database { get; }
}
