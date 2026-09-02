using System.Text;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// The per-symbol calibration upsert under CONCURRENCY, and its failure boundary.
/// <para>
/// The import modal submits Deploy and Evaluation as two unsequenced requests. They name the same
/// strategy, therefore the same symbol, and both end in <c>RecalibrateSymbolAsync</c> — an
/// unguarded read-then-insert against <c>SymbolCalibrations.Symbol</c>, which carries a UNIQUE
/// index. On a symbol's first import both requests observe no row and both insert; the loser gets a
/// duplicate-key <see cref="DbUpdateException"/>, and a duplicate key is NOT transient, so no
/// execution strategy retries it away.
/// </para>
/// <para>
/// Real SQLite, not EF InMemory, because InMemory enforces no unique index at all — the constraint
/// being violated has to actually exist for the race to be reproducible rather than simulated.
/// </para>
/// </summary>
public class BacktestCalibrationConcurrencyTests : IDisposable
{
    private const string Symbol = "XAUUSD_M1_UTC02";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BacktestTestDbContext> _options;

    /// <summary>When true, the next calibration INSERT loses the race exactly once.</summary>
    private readonly bool[] _raceOnce = [false];

    /// <summary>When true, every calibration write fails — a fault that no retry can clear.</summary>
    private readonly bool[] _failCalibrationAlways = [false];

    public BacktestCalibrationConcurrencyTests()
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

    // ---- The race ----

    [Fact]
    public async Task ImportTradeListAsync_LosingTheCalibrationInsertRace_StillCalibratesAndDoesNotFailTheImport()
    {
        var strategyId = await SeedStrategyAsync();
        _raceOnce[0] = true;
        var sut = CreateSut();

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, Upload("F.csv", 1, 2, 3), CancellationToken.None);

        result.Outcome.Should().Be(
            BacktestImportOutcome.Imported,
            "the run and its trades committed — losing a race on a derived per-symbol value cannot un-import them");
        result.Reason.Should().BeNull();

        var calibrations = await ReadCalibrationsAsync();
        calibrations.Should().ContainSingle("the unique index permits exactly one row per symbol");
        calibrations[0].SampleCount.Should().Be(
            3,
            "the retry must RE-READ and update the winner's row, not skip the recomputation");
    }

    // ---- The boundary ----

    [Fact]
    public async Task ImportTradeListAsync_WhenCalibrationFailsOutright_StillReportsTheImportThatCommitted()
    {
        // Calibration runs AFTER the run and its trades are committed. Letting its exception escape
        // turns a request whose data landed into a bare 500: the user is told the slot failed while
        // the rows sit in the database. Reporting Rejected would be the same lie in the other
        // direction, so the outcome stays true and the failure is named alongside it.
        var strategyId = await SeedStrategyAsync();
        _failCalibrationAlways[0] = true;
        var sut = CreateSut();

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, Upload("F.csv", 1, 2, 3), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Imported);
        result.TradeCount.Should().Be(3);
        result.Reason.Should().Contain("calibration");
        result.Reason.Should().Contain(Symbol);

        (await CountRunsAsync()).Should().Be(1, "the import itself committed and must stay committed");
        (await CountTradesAsync()).Should().Be(3);
    }

    // ---- Harness ----

    private BacktestImportService CreateSut()
        => new(
            new CalibrationFaultingContext(new BacktestTestDbContext(_options), _options, _raceOnce, _failCalibrationAlways),
            new CalibrationFaultingContextFactory(_options, _raceOnce, _failCalibrationAlways),
            new SqxTradeListParserService());

    private async Task<Guid> SeedStrategyAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "S1", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync(CancellationToken.None);
        return strategy.Id;
    }

    private async Task<List<SymbolCalibration>> ReadCalibrationsAsync()
    {
        await using var db = new BacktestTestDbContext(_options);
        return await db.SymbolCalibrations.AsNoTracking().ToListAsync();
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

    /// <summary>SL closes, so the calibrator has a real sample to count rather than zero.</summary>
    private static string Row(long ticket)
        => $"\"{ticket}\";\"{Symbol}\";\"Buy\";\"2016.01.04 07:16:00\";\"1066.19\";\"0,44000\";"
           + "\"2016.01.04 15:25:00\";\"1077.86\";\"511,13\";\"100511,13\";\"IST\";\"SL\";\"-27,37\";\"513,48\";\"8h 9m\";\"\"";

    private static BacktestFileUploadDto Upload(string fileName, params long[] tickets)
    {
        var lines = new List<string> { Header };
        lines.AddRange(tickets.Select(Row));
        return new BacktestFileUploadDto(fileName, new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))));
    }

    private sealed class CalibrationFaultingContextFactory(
        DbContextOptions<BacktestTestDbContext> options,
        bool[] raceOnce,
        bool[] failAlways) : IBacktestDbContextFactory
    {
        public IBacktestDbContext Create()
            => new CalibrationFaultingContext(new BacktestTestDbContext(options), options, raceOnce, failAlways);
    }

    /// <summary>
    /// A real SQLite context that can be made to lose the calibration insert race: on the first
    /// save carrying an ADDED <see cref="SymbolCalibration"/>, a competing writer inserts the row
    /// for that symbol out-of-band, and the real unique index then refuses ours. That is the
    /// production failure exactly — a genuine constraint violation, not a thrown stand-in.
    /// </summary>
    private sealed class CalibrationFaultingContext(
        BacktestTestDbContext inner,
        DbContextOptions<BacktestTestDbContext> options,
        bool[] raceOnce,
        bool[] failAlways) : IBacktestDbContext
    {
        public DbSet<BacktestRun> BacktestRuns => inner.BacktestRuns;

        public DbSet<BacktestTrade> BacktestTrades => inner.BacktestTrades;

        public DbSet<SymbolCalibration> SymbolCalibrations => inner.SymbolCalibrations;

        public DbSet<StrategyWalkForwardExport> StrategyWalkForwardExports => inner.StrategyWalkForwardExports;

        public DbSet<WalkForwardWindow> WalkForwardWindows => inner.WalkForwardWindows;

        public DatabaseFacade Database => inner.Database;

        public async Task<int> SaveChangesAsync(CancellationToken ct)
        {
            var touchesCalibration = inner.ChangeTracker
                .Entries<SymbolCalibration>()
                .Any(e => e.State is EntityState.Added or EntityState.Modified);

            if (touchesCalibration && failAlways[0])
                throw new DbUpdateException("simulated non-recoverable calibration write failure");

            if (touchesCalibration
                && raceOnce[0]
                && inner.ChangeTracker.Entries<SymbolCalibration>().Any(e => e.State == EntityState.Added))
            {
                raceOnce[0] = false;
                await InsertCompetingRowAsync(ct);
            }

            return await inner.SaveChangesAsync(ct);
        }

        /// <summary>The other request, landing between our read and our write.</summary>
        private async Task InsertCompetingRowAsync(CancellationToken ct)
        {
            await using var competitor = new BacktestTestDbContext(options);
            competitor.SymbolCalibrations.Add(new SymbolCalibration
            {
                Id = Guid.NewGuid(),
                Symbol = Symbol,
                PointValue = null,
                SampleCount = 0,
                Status = CalibrationStatus.InsufficientSamples,
                CalibratedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            await competitor.SaveChangesAsync(ct);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
