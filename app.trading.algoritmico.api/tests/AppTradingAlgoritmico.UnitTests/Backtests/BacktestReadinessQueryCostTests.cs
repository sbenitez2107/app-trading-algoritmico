using System.Data.Common;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// AS-1 cost fence: resolving the grid's readiness marker for a whole page costs ONE database
/// command, whatever the page holds.
/// <para>
/// Runs on real SQLite because only a relational provider issues countable commands — EF InMemory
/// would let an N+1 pass in silence, which is precisely the defect being fenced. The grid fetches
/// every strategy of an account in one call (<c>pageSize 500</c>, 123 rows on the real account), so
/// a per-row lookup here would be 123 extra round-trips per page load.
/// </para>
/// <para>
/// The readiness VALUES are asserted separately, through <c>StrategyService</c>, in
/// <c>StrategyServiceBacktestReadinessTests</c>. They are split because the full
/// <c>AppDbContext</c> cannot be created on SQLite — four unrelated configurations declare
/// <c>nvarchar(max)</c> — and changing production mappings to suit a test would be the wrong trade.
/// </para>
/// </summary>
public class BacktestReadinessQueryCostTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CountingCommandInterceptor _interceptor = new();
    private readonly DbContextOptions<BacktestTestDbContext> _options;

    private static readonly DateTime Boundary = new(2025, 5, 26);

    public BacktestReadinessQueryCostTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BacktestTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        using var db = new BacktestTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<List<Guid>> SeedEvaluableStrategiesAsync(int count)
    {
        await using var db = new BacktestTestDbContext(_options);
        var ids = new List<Guid>(count);

        for (var i = 0; i < count; i++)
        {
            var strategy = new Strategy { Id = Guid.NewGuid(), Name = $"S{i}", CreatedAt = DateTime.UtcNow };
            db.Strategies.Add(strategy);
            await db.SaveChangesAsync();

            var run = new BacktestRun
            {
                Id = Guid.NewGuid(),
                SourceFileName = "eval.csv",
                ContentHash = Guid.NewGuid().ToString("N"),
                StrategyId = strategy.Id,
                Kind = BacktestRunKind.Evaluation,
                Symbol = "XAUUSD_M1_UTC02",
                CreatedAt = DateTime.UtcNow,
            };
            db.BacktestRuns.Add(run);

            var export = new StrategyWalkForwardExport
            {
                Id = Guid.NewGuid(),
                StrategyId = strategy.Id,
                OosFromDate = Boundary,
                DeployParameters = "TEMAPeriod1=32,",
                EvaluationParameters = "TEMAPeriod1=35,",
                ContentHash = Guid.NewGuid().ToString("N"),
                SourceFileName = "wf.csv",
                CreatedAt = DateTime.UtcNow,
            };
            db.StrategyWalkForwardExports.Add(export);
            await db.SaveChangesAsync();

            for (var t = 0; t < 20; t++)
                db.BacktestTrades.Add(Trade(run.Id, t, Boundary.AddDays(t - 10)));

            await db.SaveChangesAsync();
            ids.Add(strategy.Id);
        }

        return ids;
    }

    private static BacktestTrade Trade(Guid runId, int rowIndex, DateTime closeTime) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = runId,
        RowIndex = rowIndex,
        Ticket = rowIndex + 1,
        Symbol = "XAUUSD_M1_UTC02",
        Type = "Buy",
        OpenTime = closeTime.AddDays(-1),
        OpenPrice = 1000m,
        Size = 0.1m,
        CloseTime = closeTime,
        ClosePrice = 1010m,
        Profit = 10m,
        Balance = 1010m,
        SampleTypeRaw = "IST",
        Segment = BacktestSegment.InSampleTest,
        CloseType = "PT",
        CreatedAt = DateTime.UtcNow,
    };

    private async Task<(int Commands, List<BacktestReadinessRow> Rows)> ResolveAsync(IReadOnlyCollection<Guid> ids)
    {
        await using var db = new BacktestTestDbContext(_options);
        _interceptor.Reset();

        var rows = await OosWindow.Resolver
            .ReadinessRows(db.Strategies, db.BacktestRuns, db.BacktestTrades, db.StrategyWalkForwardExports, ids)
            .ToListAsync();

        return (_interceptor.Count, rows);
    }

    [Fact]
    public async Task ReadinessRows_ForOneStrategy_IsASingleCommand()
    {
        var ids = await SeedEvaluableStrategiesAsync(1);

        var (commands, rows) = await ResolveAsync(ids);

        commands.Should().Be(1);
        rows.Should().ContainSingle();
        rows[0].HasAnyRun.Should().BeTrue();
        rows[0].HasOosEvidence.Should().BeTrue();
        rows[0].Readiness.Should().Be(BacktestReadiness.Evaluable);
    }

    [Fact]
    public async Task ReadinessRows_ForThirtyStrategies_IsStillASingleCommand()
    {
        var ids = await SeedEvaluableStrategiesAsync(30);

        var (commands, rows) = await ResolveAsync(ids);

        commands.Should().Be(
            1,
            "readiness is one grouped aggregate keyed by the page's ids — the cost must not grow with the page");
        rows.Should().HaveCount(30);
        rows.Should().OnlyContain(r => r.Readiness == BacktestReadiness.Evaluable);
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
