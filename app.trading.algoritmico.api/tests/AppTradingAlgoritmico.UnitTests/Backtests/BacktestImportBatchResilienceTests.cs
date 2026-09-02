using System.Text;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Failure isolation for <see cref="BacktestImportService.ImportTradeListAsync"/>.
/// <para>
/// The contract is that the caller ALWAYS gets an answer naming the file. An exception raised
/// while persisting used to escape as an unhandled failure, taking the whole report with it while
/// anything already committed stayed committed. The batch that made this visible is gone — import
/// is one file per slot now — but the property survives at the new granularity: the import modal
/// submits up to three slots, and one slot failing must neither abort the others nor hide which
/// one failed.
/// </para>
/// <para>
/// The concrete trigger needed no adversary — CSV text was copied verbatim into length-bounded
/// columns, so one over-length Comment raised "String or binary data would be truncated". That
/// class is now caught in the parser (see <see cref="SqxTradeListParserTests"/>), and this file
/// covers both halves: the length path end-to-end, and the boundary itself against an injected
/// persistence failure.
/// </para>
/// </summary>
public class BacktestImportBatchResilienceTests
{
    private DbContextOptions<AppDbContext> _options = default!;

    private AppDbContext CreateDb()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(_options);
    }

    private BacktestImportService CreateSut(AppDbContext db, IBacktestDbContextFactory factory)
        => new(db, factory, new SqxTradeListParserService());

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db, string name)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    // ---- Length violations reach the caller as data, never as an exception ----

    [Fact]
    public async Task ImportTradeListAsync_FileHasAnOverLengthComment_ImportsItAndCountsTheRejectedRow()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db, new BacktestDbContextFactory(_options));
        var overLength = new string('c', BacktestFieldLengths.Comment + 1);

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy,
            Upload("TooLong.csv", Row(2), Row(3, comment: overLength)),
            CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Imported);
        result.TradeCount.Should().Be(1);
        result.RejectedRowCount.Should().Be(1, "the over-length row is data the importer refused, not a crash");

        (await db.BacktestRuns.CountAsync()).Should().Be(1);
        (await db.BacktestTrades.CountAsync()).Should().Be(1);
    }

    // ---- Exception boundary: one slot failing does not abort the others ----

    [Fact]
    public async Task ImportTradeListAsync_OneSlotThrowsWhilePersisting_ReportsItAndLeavesTheOtherSlotsImported()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var otherStrategyId = await SeedStrategyAsync(db, "S2");
        // The SECOND persistence attempt fails, the way a non-transient provider error
        // (truncation, deadlock victim, constraint) lands in the middle of the modal's submission.
        var sut = CreateSut(db, new FailOnNthAttemptFactory(_options, failOnAttempt: 2));

        var first = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, Upload("Good1.csv", Row(1)), CancellationToken.None);
        var failing = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Evaluation, Upload("Boom.csv", Row(2)), CancellationToken.None);
        var third = await sut.ImportTradeListAsync(
            otherStrategyId, BacktestRunKind.Deploy, Upload("Good2.csv", Row(3)), CancellationToken.None);

        first.Outcome.Should().Be(BacktestImportOutcome.Imported);
        third.Outcome.Should().Be(
            BacktestImportOutcome.Imported,
            "one slot's failure must not abort the slots after it — each is its own transaction by design (D6)");

        failing.Outcome.Should().Be(BacktestImportOutcome.Rejected);
        failing.Reason.Should().NotBeNullOrWhiteSpace();
        failing.Reason.Should().Contain("Boom.csv", "a report that says only \"error\" is unusable");
        failing.Reason.Should().Contain(
            "would be truncated",
            "the reason must carry the provider's own diagnosis, otherwise it is not actionable");

        (await db.BacktestRuns.CountAsync()).Should().Be(2, "the failing slot's transaction must not have committed");
    }

    [Fact]
    public async Task ImportTradeListAsync_PersistenceAlwaysThrows_StillReturnsAResultInsteadOfPropagating()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db, new FailOnNthAttemptFactory(_options, failOnAttempt: null));

        var a = await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, Upload("A.csv", Row(1)), CancellationToken.None);
        var b = await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Evaluation, Upload("B.csv", Row(2)), CancellationToken.None);

        a.Outcome.Should().Be(BacktestImportOutcome.Rejected);
        b.Outcome.Should().Be(BacktestImportOutcome.Rejected);
        (await db.BacktestRuns.CountAsync()).Should().Be(0);
    }

    // ---- Harness ----
    // Deliberately self-contained (it duplicates a few lines of BacktestImportRetrySafetyTests'
    // failure injection) so this file can be reverted independently of the retry-safety work.

    private const string Header =
        "\"Ticket\";\"Symbol\";\"Type\";\"Open time\";\"Open price\";\"Size\";\"Close time\";\"Close price\";" +
        "\"Profit/Loss\";\"Balance\";\"Sample type\";\"Close type\";\"MAE ($)\";\"MFE ($)\";\"Time in trade\";\"Comment\"";

    private static string Row(long ticket, string comment = "")
        => $"\"{ticket}\";\"XAUUSD_M1_UTC02\";\"Buy\";\"2016.01.04 07:16:00\";\"1066.19\";\"0,44000\";"
           + $"\"2016.01.04 15:25:00\";\"1077.86\";\"511,13\";\"100511,13\";\"IST\";\"PT\";\"-27,37\";\"513,48\";\"8h 9m\";\"{comment}\"";

    private static BacktestFileUploadDto Upload(string fileName, params string[] rows)
    {
        var lines = new List<string> { Header };
        lines.AddRange(rows);
        return new BacktestFileUploadDto(fileName, new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))));
    }

    /// <summary>
    /// Hands out contexts whose <c>SaveChangesAsync</c> throws on the nth PERSISTENCE attempt (all
    /// of them when null).
    /// <para>
    /// The victim is chosen by counting attempts that actually write a run or its trades, NOT by
    /// counting contexts handed out. Those were the same number until the per-symbol calibration
    /// upsert also began taking a fresh context per attempt from this factory; after that, an
    /// ordinal over <c>Create()</c> silently retargeted the injected fault onto the PREVIOUS
    /// import's calibration, and the slot that was supposed to fail while persisting quietly
    /// succeeded. Counting the thing the test names — a persistence attempt — is what makes the
    /// targeting survive a change in who else borrows the factory.
    /// </para>
    /// </summary>
    private sealed class FailOnNthAttemptFactory(DbContextOptions<AppDbContext> options, int? failOnAttempt)
        : IBacktestDbContextFactory
    {
        private readonly int[] _persistAttempts = [0];

        public IBacktestDbContext Create()
            => new ThrowOnPersistDbContext(new AppDbContext(options), _persistAttempts, failOnAttempt);
    }

    private sealed class ThrowOnPersistDbContext(
        AppDbContext inner, int[] persistAttempts, int? failOnAttempt) : IBacktestDbContext
    {
        private bool _counted;

        public DbSet<BacktestRun> BacktestRuns => inner.BacktestRuns;

        public DbSet<BacktestTrade> BacktestTrades => inner.BacktestTrades;

        public DbSet<SymbolCalibration> SymbolCalibrations => inner.SymbolCalibrations;

        public DbSet<StrategyWalkForwardExport> StrategyWalkForwardExports => inner.StrategyWalkForwardExports;

        public DbSet<WalkForwardWindow> WalkForwardWindows => inner.WalkForwardWindows;

        public DatabaseFacade Database => inner.Database;

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            var persistsTheRun = inner.ChangeTracker.Entries<BacktestRun>().Any()
                || inner.ChangeTracker.Entries<BacktestTrade>().Any();

            if (!persistsTheRun)
                return inner.SaveChangesAsync(ct);

            // One count per context: ReplaceAsync flushes twice inside a single attempt, and that
            // is still one attempt.
            if (!_counted)
            {
                _counted = true;
                persistAttempts[0]++;
            }

            if (failOnAttempt is null || persistAttempts[0] == failOnAttempt)
            {
                throw new DbUpdateException(
                    "An error occurred while saving the entity changes. See the inner exception for details.",
                    new InvalidOperationException("String or binary data would be truncated in table 'BacktestTrades', column 'Comment'."));
            }

            return inner.SaveChangesAsync(ct);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
