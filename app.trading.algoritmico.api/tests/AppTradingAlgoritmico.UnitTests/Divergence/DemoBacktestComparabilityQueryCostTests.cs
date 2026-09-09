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
/// Task 5.2 cost fence — the read path issues AT MOST THREE database commands (run lookup, demo
/// projection, backtest projection) and materializes no entity, whatever the strategy holds.
/// <para>
/// Runs on real SQLite, exercising <see cref="DemoBacktestComparabilityReadService"/>'s
/// <c>internal static</c> query builders directly against a minimal context, because the full
/// <c>AppDbContext</c> cannot be created on SQLite (unrelated configurations declare
/// <c>nvarchar(max)</c>) — the same documented trade-off <c>BacktestReadinessQueryCostTests</c>
/// already makes for a sibling read path. The VALUES this path produces are asserted separately,
/// through the real service on EF InMemory, in <see cref="DemoBacktestComparabilityReadServiceTests"/>.
/// </para>
/// </summary>
public class DemoBacktestComparabilityQueryCostTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CountingCommandInterceptor _interceptor = new();
    private readonly DbContextOptions<ComparabilityQueryTestDbContext> _options;

    public DemoBacktestComparabilityQueryCostTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ComparabilityQueryTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        using var db = new ComparabilityQueryTestDbContext(_options);
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

        await using (var seed = new ComparabilityQueryTestDbContext(_options))
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

        await using var db = new ComparabilityQueryTestDbContext(_options);
        _interceptor.Reset();

        var runId2 = await DemoBacktestComparabilityReadService
            .RunIdQuery(db.BacktestRuns.AsNoTracking(), strategyId, BacktestRunKind.Deploy)
            .FirstOrDefaultAsync();

        runId2.Should().Be(runId);

        var demoOpens = await DemoBacktestComparabilityReadService
            .DemoOpensQuery(db.StrategyTrades.AsNoTracking(), strategyId)
            .ToListAsync();

        var backtestOpens = await DemoBacktestComparabilityReadService
            .BacktestOpensQuery(db.BacktestTrades.AsNoTracking(), runId2!.Value)
            .ToListAsync();

        _interceptor.Count.Should().BeLessThanOrEqualTo(
            3, "run lookup + demo projection + backtest projection — never a per-row query");
        demoOpens.Should().HaveCount(5);
        backtestOpens.Should().HaveCount(5);
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
/// Minimal DbContext for the query-cost fence above — excludes Identity/unrelated tables so it can
/// be created on real SQLite. Pattern mirrors <c>BacktestTestDbContext</c>
/// (<c>BacktestSchemaTests.cs</c>), extended with <c>StrategyTrades</c> since this read path needs
/// both sides.
/// </summary>
public class ComparabilityQueryTestDbContext : DbContext
{
    public ComparabilityQueryTestDbContext(DbContextOptions<ComparabilityQueryTestDbContext> options) : base(options) { }

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
