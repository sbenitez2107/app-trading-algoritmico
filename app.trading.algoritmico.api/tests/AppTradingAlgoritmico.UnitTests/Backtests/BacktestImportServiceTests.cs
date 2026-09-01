using System.Text;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Slot idempotency and non-contamination for the trade-list importer (SBI-1, SBI-4, SBI-5, SM-1,
/// CAL-4, CAL-6). Uses the FULL <see cref="AppDbContext"/> (EF InMemory) rather than the isolated
/// <see cref="IBacktestDbContext"/> surface, because the non-contamination regression must assert
/// against the real <c>StrategyTrades</c> table, which <see cref="IBacktestDbContext"/>
/// deliberately cannot see. <c>TransactionIgnoredWarning</c> is suppressed because EF InMemory does
/// not implement real transactions; the service's Begin/Commit calls become harmless no-ops here.
/// <para>
/// EF InMemory enforces NO unique index, so the <c>(StrategyId, Kind)</c> constraint itself is
/// fenced in <see cref="BacktestSchemaTests"/> against real SQLite. What THIS file proves is that
/// the service never relies on the constraint to keep a slot single-occupancy — it decides.
/// </para>
/// </summary>
public class BacktestImportServiceTests
{
    private const string F1Name = "ListOfTrades_XAUUSD_H1_IST.csv";
    private const string Symbol = "XAUUSD_M1_UTC02";

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private DbContextOptions<AppDbContext> _options = default!;

    private AppDbContext CreateDb()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(_options);
    }

    // The REAL BacktestDbContextFactory, over the same options: each retried attempt gets its own
    // change tracker while sharing this test's in-memory store — the production shape.
    private BacktestImportService CreateSut(AppDbContext db)
        => new(db, new BacktestDbContextFactory(_options), new SqxTradeListParserService());

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db, string name)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    private static async Task<BacktestFileUploadDto> FixtureUploadAsync(string name)
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath(name));
        return new BacktestFileUploadDto(name, new MemoryStream(bytes));
    }

    private const string Header =
        "\"Ticket\";\"Symbol\";\"Type\";\"Open time\";\"Open price\";\"Size\";\"Close time\";\"Close price\";" +
        "\"Profit/Loss\";\"Balance\";\"Sample type\";\"Close type\";\"MAE ($)\";\"MFE ($)\";\"Time in trade\";\"Comment\"";

    private static string Row(
        long ticket = 1, string symbol = "XAUUSD_M1_UTC02", string closeType = "PT",
        string openPrice = "1066.19", string closePrice = "1077.86")
        => $"\"{ticket}\";\"{symbol}\";\"Buy\";\"2016.01.04 07:16:00\";\"{openPrice}\";\"0,44000\";" +
           "\"2016.01.04 15:25:00\";\"" + closePrice + "\";\"511,13\";\"100511,13\";\"IST\";\"" + closeType + "\";\"-27,37\";\"513,48\";\"8h 9m\";\"\"";

    private static BacktestFileUploadDto SyntheticUpload(string fileName, params long[] tickets)
    {
        var lines = new List<string> { Header };
        lines.AddRange(tickets.Select(t => Row(ticket: t)));
        return new BacktestFileUploadDto(fileName, new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))));
    }

    // ---- Regression gate: the importer never reaches live trade storage ----

    [Fact]
    public async Task ImportTradeListAsync_ImportingAFixture_NeverTouchesStrategyTrades()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "Unrelated Live Strategy");
        for (var i = 0; i < 10; i++)
        {
            db.StrategyTrades.Add(new StrategyTrade
            {
                Id = Guid.NewGuid(),
                StrategyId = strategyId,
                Ticket = i + 1,
                OpenTime = DateTime.UtcNow,
                Type = "buy",
                Item = "EURUSD",
                OpenPrice = 1.1m,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var beforeCount = await db.StrategyTrades.CountAsync();
        var beforeTickets = await db.StrategyTrades.Select(t => t.Ticket).OrderBy(t => t).ToListAsync();

        var sut = CreateSut(db);
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        (await db.StrategyTrades.CountAsync()).Should().Be(beforeCount);
        (await db.StrategyTrades.Select(t => t.Ticket).OrderBy(t => t).ToListAsync()).Should().BeEquivalentTo(beforeTickets);
    }

    // ---- SBI-1: the run is attributed from the route, at creation ----

    [Fact]
    public async Task ImportTradeListAsync_EmptySlot_CreatesTheRunAttributedToTheRouteStrategy()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Imported);
        var run = await db.BacktestRuns.AsNoTracking().SingleAsync();
        run.StrategyId.Should().Be(strategyId);
        run.Kind.Should().Be(BacktestRunKind.Deploy);
        (await db.BacktestTrades.CountAsync(t => t.BacktestRunId == run.Id)).Should().Be(329);
    }

    // ---- SBI-4: slot idempotency, three outcomes ----

    [Fact]
    public async Task ImportTradeListAsync_IdenticalBytesIntoAnOccupiedSlot_IsUnchangedAndWritesNothing()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);
        var runBefore = await db.BacktestRuns.AsNoTracking().SingleAsync();

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Unchanged);
        (await db.BacktestRuns.CountAsync()).Should().Be(1);
        (await db.BacktestTrades.CountAsync()).Should().Be(329);
        var runAfter = await db.BacktestRuns.AsNoTracking().SingleAsync();
        runAfter.Id.Should().Be(runBefore.Id);
        runAfter.UpdatedAt.Should().BeNull("Unchanged means no write at all, not a write that happens to be equal");
    }

    [Fact]
    public async Task ImportTradeListAsync_DifferentBytesIntoAnOccupiedSlot_ReplacesTheRunInPlace()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);
        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, SyntheticUpload("v1.csv", 1, 2, 3), CancellationToken.None);
        var runBefore = await db.BacktestRuns.AsNoTracking().SingleAsync();

        var result = await sut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, SyntheticUpload("v2.csv", 10, 20, 30, 40), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Replaced);
        (await db.BacktestRuns.CountAsync()).Should().Be(1, "replace must reuse the slot, never create a second run");
        var runAfter = await db.BacktestRuns.AsNoTracking().SingleAsync();
        runAfter.Id.Should().Be(runBefore.Id);
        runAfter.SourceFileName.Should().Be("v2.csv");
        var tickets = await db.BacktestTrades.Where(t => t.BacktestRunId == runAfter.Id).Select(t => t.Ticket).ToListAsync();
        tickets.Should().BeEquivalentTo([10L, 20L, 30L, 40L], "the prior trades are gone and the new ones are in");
    }

    [Fact]
    public async Task ImportTradeListAsync_TheTwoSlotsOfOneStrategy_AreIndependent()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        var deploy = await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, SyntheticUpload("d.csv", 1, 2), CancellationToken.None);
        var evaluation = await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Evaluation, SyntheticUpload("e.csv", 3, 4, 5), CancellationToken.None);

        deploy.Outcome.Should().Be(BacktestImportOutcome.Imported);
        evaluation.Outcome.Should().Be(BacktestImportOutcome.Imported);
        var runs = await db.BacktestRuns.AsNoTracking().ToListAsync();
        runs.Should().HaveCount(2);
        runs.Select(r => r.Kind).Should().BeEquivalentTo([BacktestRunKind.Deploy, BacktestRunKind.Evaluation]);
        var deployRunId = runs.Single(r => r.Kind == BacktestRunKind.Deploy).Id;
        var evaluationRunId = runs.Single(r => r.Kind == BacktestRunKind.Evaluation).Id;
        (await db.BacktestTrades.CountAsync(t => t.BacktestRunId == deployRunId)).Should().Be(2);
        (await db.BacktestTrades.CountAsync(t => t.BacktestRunId == evaluationRunId)).Should().Be(3);
    }

    // ---- SBI-4: anti-regression for the dropped unique ContentHash index ----

    [Fact]
    public async Task ImportTradeListAsync_IdenticalBytesForTwoStrategies_BothImportAndShareOneContentHash()
    {
        using var db = CreateDb();
        var s1 = await SeedStrategyAsync(db, "Deployed on FTMO-Demo2");
        var s2 = await SeedStrategyAsync(db, "Deployed on SBDEMO2");
        var sut = CreateSut(db);

        var first = await sut.ImportTradeListAsync(s1, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);
        var second = await sut.ImportTradeListAsync(s2, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        first.Outcome.Should().Be(BacktestImportOutcome.Imported);
        second.Outcome.Should().Be(
            BacktestImportOutcome.Imported,
            "identity is the slot, so the same bytes under a different strategy are a genuinely new run");
        var runs = await db.BacktestRuns.AsNoTracking().ToListAsync();
        runs.Should().HaveCount(2);
        runs.Select(r => r.ContentHash).Distinct().Should().ContainSingle("both runs came from the same file");
        runs.Select(r => r.StrategyId).Should().BeEquivalentTo([s1, s2]);
    }

    // ---- SBI-5: the declared kind is stored, never verified ----

    [Fact]
    public async Task ImportTradeListAsync_ADeployFileDeclaredAsEvaluation_IsStoredWithNoWarningAndNoContentCheck()
    {
        // Nothing in a 16-column trade list identifies the parameters that produced it, so a
        // mislabeled file is undetectable BY CONSTRUCTION. This test pins that the system does not
        // pretend otherwise: the same bytes accepted as Deploy are accepted as Evaluation, with an
        // identical result shape and no reason text.
        using var db = CreateDb();
        var s1 = await SeedStrategyAsync(db, "S1");
        var s2 = await SeedStrategyAsync(db, "S2");
        var sut = CreateSut(db);

        var asDeploy = await sut.ImportTradeListAsync(s1, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);
        var asEvaluation = await sut.ImportTradeListAsync(s2, BacktestRunKind.Evaluation, await FixtureUploadAsync(F1Name), CancellationToken.None);

        asEvaluation.Outcome.Should().Be(BacktestImportOutcome.Imported);
        asEvaluation.Reason.Should().BeNull("there is no warning to raise — the mislabeling is not observable");
        asEvaluation.TradeCount.Should().Be(asDeploy.TradeCount);
        var evaluationRun = await db.BacktestRuns.AsNoTracking().SingleAsync(r => r.StrategyId == s2);
        evaluationRun.Kind.Should().Be(BacktestRunKind.Evaluation, "the declared kind is stored as given");
    }

    // ---- CAL-4 / CAL-6: calibration from persisted trades, deduplicated by content hash ----

    [Fact]
    public async Task ImportTradeListAsync_OneFixture_CalibratesTheSymbolFromPersistedTrades()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        await sut.ImportTradeListAsync(strategyId, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        var calibration = await db.SymbolCalibrations.SingleAsync(c => c.Symbol == Symbol);
        calibration.SampleCount.Should().Be(90);
        calibration.PointValue.Should().Be(100.000m);
    }

    [Fact]
    public async Task ImportTradeListAsync_SameFileForTwoStrategies_DoesNotDoubleTheCalibrationSample()
    {
        // End-to-end wiring for CAL-6: the de-duplication rule is proven pure in
        // SymbolPointValueCalibratorTests; this proves the import path actually applies it.
        using var db = CreateDb();
        var s1 = await SeedStrategyAsync(db, "Deployed on FTMO-Demo2");
        var s2 = await SeedStrategyAsync(db, "Deployed on SBDEMO2");
        var sut = CreateSut(db);

        await sut.ImportTradeListAsync(s1, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);
        await sut.ImportTradeListAsync(s2, BacktestRunKind.Deploy, await FixtureUploadAsync(F1Name), CancellationToken.None);

        (await db.BacktestTrades.CountAsync()).Should().Be(658, "both runs really do store their own trades");
        var calibration = await db.SymbolCalibrations.SingleAsync(c => c.Symbol == Symbol);
        calibration.SampleCount.Should().Be(90, "one file, one contribution — not 180");
        calibration.PointValue.Should().Be(100.000m);
    }
}
