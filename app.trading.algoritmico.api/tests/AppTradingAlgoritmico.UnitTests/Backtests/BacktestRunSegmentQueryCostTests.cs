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
/// Task 3.9 — deriving every member's run segment costs ONE database command for the whole group,
/// whatever the group holds (the <c>ReadinessRows</c> precedent).
/// <para>
/// Runs on real SQLite because only a relational provider issues countable commands; EF InMemory
/// would let an N+1 pass in silence. The rule the rows feed is asserted separately, as a pure
/// function, in <see cref="BacktestRunSelectionTests"/>.
/// </para>
/// </summary>
public class BacktestRunSegmentQueryCostTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CountingCommandInterceptor _interceptor = new();
    private readonly DbContextOptions<BacktestTestDbContext> _options;

    public BacktestRunSegmentQueryCostTests()
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

    private async Task<List<Guid>> SeedAsync(int strategies, BacktestSegment segment, bool withTrades = true)
    {
        await using var db = new BacktestTestDbContext(_options);
        var ids = new List<Guid>(strategies);

        for (var i = 0; i < strategies; i++)
        {
            var strategy = new Strategy { Id = Guid.NewGuid(), Name = $"S{i}", CreatedAt = DateTime.UtcNow };
            db.Strategies.Add(strategy);
            await db.SaveChangesAsync();

            var run = new BacktestRun
            {
                Id = Guid.NewGuid(),
                SourceFileName = "deploy.csv",
                ContentHash = Guid.NewGuid().ToString("N"),
                StrategyId = strategy.Id,
                Kind = BacktestRunKind.Deploy,
                Symbol = "XAUUSD_M1_UTC02",
                CreatedAt = DateTime.UtcNow,
            };
            db.BacktestRuns.Add(run);
            await db.SaveChangesAsync();

            if (withTrades)
            {
                for (var t = 0; t < 5; t++)
                    db.BacktestTrades.Add(Trade(run.Id, t, segment));

                await db.SaveChangesAsync();
            }

            ids.Add(strategy.Id);
        }

        return ids;
    }

    private static BacktestTrade Trade(Guid runId, int rowIndex, BacktestSegment segment) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = runId,
        RowIndex = rowIndex,
        Ticket = rowIndex + 1,
        Symbol = "XAUUSD_M1_UTC02",
        Type = "Buy",
        OpenTime = new DateTime(2024, 1, 1).AddDays(rowIndex),
        OpenPrice = 1000m,
        Size = 0.1m,
        CloseTime = new DateTime(2024, 1, 2).AddDays(rowIndex),
        ClosePrice = 1010m,
        Profit = 10m,
        Balance = 1010m,
        SampleTypeRaw = "IST",
        Segment = segment,
        CloseType = "PT",
        CreatedAt = DateTime.UtcNow,
    };

    private async Task<(int Commands, List<BacktestRunSegmentRow> Rows)> ResolveAsync(IReadOnlyCollection<Guid> ids)
    {
        await using var db = new BacktestTestDbContext(_options);
        _interceptor.Reset();

        var rows = await RunSegmentSelection
            .SegmentRows(db.BacktestRuns, db.BacktestTrades, ids)
            .ToListAsync();

        return (_interceptor.Count, rows);
    }

    [Fact]
    public async Task SegmentRows_ForOneStrategy_IsASingleCommandAndResolvesTheSegment()
    {
        var ids = await SeedAsync(1, BacktestSegment.InSampleTest);

        var (commands, rows) = await ResolveAsync(ids);

        commands.Should().Be(1);
        rows.Should().ContainSingle();
        rows[0].State.Should().Be(BacktestRunSegmentState.Resolved);
        rows[0].Segment.Should().Be(BacktestSegment.InSampleTest);
    }

    [Fact]
    public async Task SegmentRows_ForThirtyStrategies_IsStillASingleCommand()
    {
        var ids = await SeedAsync(30, BacktestSegment.OutOfSample);

        var (commands, rows) = await ResolveAsync(ids);

        commands.Should().Be(
            1,
            "the segment of every run in the group comes from ONE grouped aggregate — a per-member "
            + "lookup would be one round-trip per strategy on every analysis");
        rows.Should().HaveCount(30);
        rows.Should().OnlyContain(r => r.Segment == BacktestSegment.OutOfSample);
    }

    [Fact]
    public async Task SegmentRows_ForATradelessRun_ProjectsNullRatherThanZero()
    {
        var ids = await SeedAsync(1, BacktestSegment.InSampleTest, withTrades: false);

        var (_, rows) = await ResolveAsync(ids);

        rows.Should().ContainSingle();
        rows[0].MinSegment.Should().BeNull(
            "Min over an empty set is null, and (int?) is what keeps it null instead of "
            + "collapsing onto 0 — which IS BacktestSegment.Unknown");
        rows[0].State.Should().Be(BacktestRunSegmentState.NoTrades);
        rows[0].Segment.Should().BeNull();
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
