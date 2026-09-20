using System.Data.Common;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Task 5.6 — pins the DB command budget for B1: run lookup + demo <see cref="CostObservation"/>
/// query + backtest <see cref="CostObservation"/> query. No calibration query is needed for
/// coverage alone (design D3 flags the exact 4-command list as open for B2's calibration query).
/// The count must not grow with strategy trade count.
/// </summary>
public class CostDecompositionQueryCostTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CountingCommandInterceptor _interceptor = new();
    private readonly DbContextOptions<CostDecompositionQueryTestDbContext> _options;

    public CostDecompositionQueryCostTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<CostDecompositionQueryTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        using var db = new CostDecompositionQueryTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAsync_AnyStrategy_IssuesAtMostThreeQueriesAndMaterializesNoEntities()
    {
        var strategyId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var openTime = new DateTime(2026, 4, 21, 10, 15, 0);

        await using (var seed = new CostDecompositionQueryTestDbContext(_options))
        {
            seed.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
            seed.BacktestRuns.Add(new BacktestRun
            {
                Id = runId,
                StrategyId = strategyId,
                Kind = BacktestRunKind.Deploy,
                SourceFileName = "deploy.csv",
                ContentHash = Guid.NewGuid().ToString("N"),
            });

            for (var i = 0; i < 5; i++)
            {
                seed.StrategyTrades.Add(new StrategyTrade
                {
                    Id = Guid.NewGuid(),
                    StrategyId = strategyId,
                    Ticket = i + 1,
                    OpenTime = openTime.AddMinutes(i),
                    Type = "buy",
                    Size = 0.1m,
                    Item = "NDX",
                    OpenPrice = 15000m + i,
                });
                seed.BacktestTrades.Add(new BacktestTrade
                {
                    Id = Guid.NewGuid(),
                    BacktestRunId = runId,
                    RowIndex = i,
                    Ticket = i + 1,
                    Symbol = "NDX_DARWINEX",
                    Type = "Buy",
                    OpenTime = openTime.AddMinutes(i),
                    OpenPrice = 15000m + i,
                    Size = 0.1m,
                    CloseTime = openTime.AddHours(1),
                    ClosePrice = 15010m,
                    SampleTypeRaw = "IST",
                    CloseType = "PT",
                });
            }

            await seed.SaveChangesAsync();
        }

        await using var db = new CostDecompositionQueryTestDbContext(_options);
        _interceptor.Reset();

        var runId2 = await CostDecompositionReadService
            .RunIdQuery(db.BacktestRuns.AsNoTracking(), strategyId, BacktestRunKind.Deploy)
            .FirstOrDefaultAsync();

        runId2.Should().Be(runId);

        var demoObservations = await CostDecompositionReadService
            .DemoObservationsQuery(db.StrategyTrades.AsNoTracking(), strategyId)
            .ToListAsync();

        var backtestObservations = await CostDecompositionReadService
            .BacktestObservationsQuery(db.BacktestTrades.AsNoTracking(), runId2!.Value)
            .ToListAsync();

        _interceptor.Count.Should().BeLessThanOrEqualTo(
            3, "run lookup + demo projection + backtest projection — no calibration query needed for coverage alone");
        demoObservations.Should().HaveCount(5);
        backtestObservations.Should().HaveCount(5);
        db.ChangeTracker.Entries().Should().BeEmpty("the projections must never materialize StrategyTrade/BacktestTrade entities");
    }

    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => _count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

/// <summary>
/// Minimal DbContext for the query-cost fence above — mirrors
/// <c>ComparabilityQueryTestDbContext</c>, excluding Identity/unrelated tables so it can be created
/// on real SQLite.
/// </summary>
public class CostDecompositionQueryTestDbContext : DbContext
{
    public CostDecompositionQueryTestDbContext(DbContextOptions<CostDecompositionQueryTestDbContext> options) : base(options) { }

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyTrade> StrategyTrades => Set<StrategyTrade>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<BacktestTrade> BacktestTrades => Set<BacktestTrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Strategy>(b =>
        {
            b.ToTable("Strategies");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Ignore(x => x.MonthlyPerformance);
            b.Ignore(x => x.Comments);
            b.Ignore(x => x.BatchStage);
            b.Ignore(x => x.TradingAccount);
        });

        modelBuilder.ApplyConfiguration(new StrategyTradeConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestRunConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestTradeConfiguration());
    }
}
