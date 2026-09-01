using System.Text;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Retry-safety gate for <see cref="BacktestImportService"/>'s per-file write unit.
/// <para>
/// <c>CreateExecutionStrategy().ExecuteAsync(...)</c> makes a transient failure RETRYABLE; it does
/// NOT make the retried delegate IDEMPOTENT. SQL Server's <c>EnableRetryOnFailure</c> re-invokes
/// the delegate, so the delegate must be safe to run twice against the same starting state.
/// </para>
/// <para>
/// No unit test can trigger a real retry — EF InMemory never retries and SQLite does not use
/// <c>SqlServerRetryingExecutionStrategy</c> — which is exactly why both defects survived a fully
/// green suite. These tests assert the PROPERTY instead: invoking the retried work twice in a row
/// must leave the same end state as invoking it once. The harness is the real SQLite provider
/// (<see cref="BacktestTestDbContext"/>) because EF InMemory enforces none of the unique indexes
/// that a duplicated insert violates.
/// </para>
/// </summary>
public class BacktestImportRetrySafetyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BacktestTestDbContext> _options;

    /// <summary>Number of upcoming <c>SaveChangesAsync</c> calls that must fail, shared by every context this test creates.</summary>
    private readonly int[] _pendingSaveFailures = [0];

    public BacktestImportRetrySafetyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BacktestTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new BacktestTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- Work Unit 1, defect A: a retry after an ACCEPTED SaveChanges whose Commit was lost ----

    [Fact]
    public async Task RetriedWriteUnit_SecondAttemptAfterALostCommit_StillPersistsTheNewContentHash()
    {
        var sut = CreateSut();
        var strategyId = await SeedStrategyAsync();

        // Starting state: v1 committed.
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("Same.csv", 1, 2, 3), CancellationToken.None);
        var hashV1 = await ReadContentHashAsync();

        // Attempt 1 of the REPLACE unit. Its SaveChangesAsync is accepted (AcceptAllChanges sets
        // originals := current), and then the commit is lost. A rolled-back transaction leaves the
        // DATABASE at its pre-attempt value while the change tracker keeps the accepted state, so
        // rewind the column out-of-band to reproduce exactly that split.
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("Same.csv", 10, 20, 30, 40), CancellationToken.None);
        var hashV2 = await ReadContentHashAsync();
        hashV2.Should().NotBe(hashV1);
        await RewindContentHashAsync(hashV1);

        // Attempt 2 — the retry: same parsed input, same hash, same service instance.
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("Same.csv", 10, 20, 30, 40), CancellationToken.None);

        (await ReadContentHashAsync()).Should().Be(
            hashV2,
            "a retried attempt must re-read the run it mutates; if it inherits the previous attempt's "
            + "accepted originals then re-assigning an equal ContentHash is not detected as a change, no "
            + "UPDATE column is emitted, and the row silently keeps the OLD hash while holding the NEW trades");
    }

    // ---- Work Unit 1, defect B: a retry after a FAILED SaveChanges leaves the Added graph behind ----

    [Fact]
    public async Task RetriedWriteUnit_SecondAttemptAfterAFailedSave_CreatesExactlyOneRun()
    {
        // The next SaveChangesAsync fails, like a transient error landing mid-unit.
        var strategyId = await SeedStrategyAsync();
        _pendingSaveFailures[0] = 1;
        var sut = CreateSut();

        // Attempt 1 dies at SaveChangesAsync. The transaction rolls back; the change tracker does
        // not — SaveChangesAsync only calls AcceptAllChanges on SUCCESS, so the graph stays Added.
        await IgnoringFailureAsync(() => sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("Same.csv", 1, 2, 3), CancellationToken.None));

        (await CountRunsAsync()).Should().Be(0, "the failed attempt's transaction must have rolled back");

        // Attempt 2 — the retry, against that same starting state.
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("Same.csv", 1, 2, 3), CancellationToken.None);

        (await CountRunsAsync()).Should().Be(
            1,
            "a retried attempt that inherits the failed attempt's Added graph re-Adds a second run with "
            + "fresh Guids, and the duplicate INSERT violates the unique (StrategyId, Kind) index — which "
            + "is not transient, so it propagates instead of being retried away");
        (await CountTradesAsync()).Should().Be(3, "the retry must persist the run's trades exactly once");
    }

    // ---- Harness ----

    private BacktestImportService CreateSut()
        => new(
            new FailableBacktestDbContext(new BacktestTestDbContext(_options), _pendingSaveFailures),
            new FailableBacktestDbContextFactory(_options, _pendingSaveFailures),
            new SqxTradeListParserService());

    private async Task<Guid> SeedStrategyAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        var strategy = new Domain.Entities.Strategy { Id = Guid.NewGuid(), Name = "S1", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync(CancellationToken.None);
        return strategy.Id;
    }

    private static async Task IgnoringFailureAsync(Func<Task> act)
    {
        try
        {
            await act();
        }
        catch (Exception)
        {
            // Before the per-file exception boundary exists this propagates; after it, the batch
            // reports the failure instead. Either shape is a valid "attempt 1 did not succeed".
        }
    }

    private async Task<string> ReadContentHashAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        return await db.BacktestRuns.AsNoTracking().Select(r => r.ContentHash).SingleAsync();
    }

    private async Task RewindContentHashAsync(string hash)
    {
        await using var db = new BacktestTestDbContext(_options);
        await db.Database.ExecuteSqlRawAsync("UPDATE BacktestRuns SET ContentHash = {0}", hash);
    }

    private async Task<int> CountRunsAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        return await db.BacktestRuns.AsNoTracking().CountAsync();
    }

    private async Task<int> CountTradesAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        return await db.BacktestTrades.AsNoTracking().CountAsync();
    }

    private const string Header =
        "\"Ticket\";\"Symbol\";\"Type\";\"Open time\";\"Open price\";\"Size\";\"Close time\";\"Close price\";" +
        "\"Profit/Loss\";\"Balance\";\"Sample type\";\"Close type\";\"MAE ($)\";\"MFE ($)\";\"Time in trade\";\"Comment\"";

    private static string Row(long ticket)
        => $"\"{ticket}\";\"XAUUSD_M1_UTC02\";\"Buy\";\"2016.01.04 07:16:00\";\"1066.19\";\"0,44000\";"
           + "\"2016.01.04 15:25:00\";\"1077.86\";\"511,13\";\"100511,13\";\"IST\";\"PT\";\"-27,37\";\"513,48\";\"8h 9m\";\"\"";

    private static BacktestFileUploadDto Upload(string fileName, params long[] tickets)
    {
        var lines = new List<string> { Header };
        lines.AddRange(tickets.Select(Row));
        return new BacktestFileUploadDto(fileName, new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))));
    }

    /// <summary>Creates a fresh <see cref="FailableBacktestDbContext"/> per attempt, all on the same SQLite database.</summary>
    private sealed class FailableBacktestDbContextFactory(
        DbContextOptions<BacktestTestDbContext> options,
        int[] pendingSaveFailures) : IBacktestDbContextFactory
    {
        public IBacktestDbContext Create()
            => new FailableBacktestDbContext(new BacktestTestDbContext(options), pendingSaveFailures);
    }

    /// <summary>
    /// Real SQLite context (real transactions, real unique indexes) whose <c>SaveChangesAsync</c>
    /// can be made to fail on demand — the only way to leave a change tracker holding an Added
    /// graph, since <c>AcceptAllChanges</c> runs on success only.
    /// </summary>
    private sealed class FailableBacktestDbContext(BacktestTestDbContext inner, int[] pendingSaveFailures)
        : IBacktestDbContext
    {
        public DbSet<Domain.Entities.BacktestRun> BacktestRuns => inner.BacktestRuns;

        public DbSet<Domain.Entities.BacktestTrade> BacktestTrades => inner.BacktestTrades;

        public DbSet<Domain.Entities.SymbolCalibration> SymbolCalibrations => inner.SymbolCalibrations;

        public DbSet<Domain.Entities.StrategyWalkForwardExport> StrategyWalkForwardExports => inner.StrategyWalkForwardExports;

        public DbSet<Domain.Entities.WalkForwardWindow> WalkForwardWindows => inner.WalkForwardWindows;

        public DatabaseFacade Database => inner.Database;

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            if (pendingSaveFailures[0] > 0)
            {
                pendingSaveFailures[0]--;
                throw new InvalidOperationException("simulated transient failure at SaveChangesAsync");
            }

            return inner.SaveChangesAsync(ct);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
