using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// WF-1, WF-5, WF-7, WF-8: persisting a walk-forward export, and the payoff of NOT copying its
/// boundary onto the run — a run imported before its export becomes evaluable later without being
/// touched.
/// </summary>
public class WalkForwardImportServiceTests
{
    private const string WfName = "WFParamsExport_XAUUSD_H1.csv";
    private const string TradeListName = "ListOfTrades_XAUUSD_H1_IST.csv";
    private static readonly DateTime ExpectedBoundary = new(2025, 5, 26);

    private DbContextOptions<AppDbContext> _options = default!;

    private AppDbContext CreateDb()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(_options);
    }

    private WalkForwardImportService CreateSut(AppDbContext db)
        => new(db, new BacktestDbContextFactory(_options), new WalkForwardExportParserService());

    private BacktestImportService CreateTradeListSut(AppDbContext db)
        => new(db, new BacktestDbContextFactory(_options), new SqxTradeListParserService());

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db, string name)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static async Task<BacktestFileUploadDto> UploadAsync(string name)
        => new(name, new MemoryStream(await File.ReadAllBytesAsync(FixturePath(name))));

    private static async Task<BacktestFileUploadDto> ModifiedWfUploadAsync(string name)
    {
        // A genuinely different export: the same file with one elapsed OOS figure changed, so the
        // content hash differs while the shape stays valid.
        var text = await File.ReadAllTextAsync(FixturePath(WfName));
        return new BacktestFileUploadDto(name, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text.Replace("\"1830,14\"", "\"1830,15\""))));
    }

    // ---- WF-1: first import ----

    [Fact]
    public async Task ImportAsync_FirstExportForAStrategy_PersistsSixWindowsAndTheBoundary()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        var result = await sut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Imported);
        result.WindowCount.Should().Be(6);
        result.OosFromDate.Should().Be(ExpectedBoundary);

        var export = await db.StrategyWalkForwardExports.AsNoTracking().SingleAsync();
        export.StrategyId.Should().Be(strategyId);
        export.OosFromDate.Should().Be(ExpectedBoundary, "the OOS start of the SECOND-TO-LAST row");
        export.EvaluationParameters.Should().Be(
            "TEMAPeriod1=35,ProfitTargetCoef1=5.96,StopLossCoef1=2.05,TrailingStopCoef1=2.76,EMAPeriod1=117,");
        export.DeployParameters.Should().Be(
            "TEMAPeriod1=32,ProfitTargetCoef1=5.96,StopLossCoef1=2.05,TrailingStopCoef1=3.06,EMAPeriod1=117,");

        var windows = await db.WalkForwardWindows.AsNoTracking().OrderBy(w => w.RowIndex).ToListAsync();
        windows.Should().HaveCount(6);
        windows.Count(w => w.IsFutureWindow).Should().Be(1);
        windows[^1].IsFutureWindow.Should().BeTrue();
        windows[^1].RetDdRatioOos.Should().BeNull();
    }

    [Fact]
    public async Task ImportAsync_IdenticalBytes_IsUnchangedAndWritesNothing()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);
        await sut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);
        var before = await db.WalkForwardWindows.AsNoTracking().Select(w => w.Id).OrderBy(id => id).ToListAsync();

        var result = await sut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Unchanged);
        (await db.StrategyWalkForwardExports.CountAsync()).Should().Be(1);
        var after = await db.WalkForwardWindows.AsNoTracking().Select(w => w.Id).OrderBy(id => id).ToListAsync();
        after.Should().Equal(before, "an identical re-import must not churn rows — that is what keeps a retry safe");
    }

    [Fact]
    public async Task ImportAsync_UpdatedExport_ReplacesTheWindowsAndKeepsOneExport()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);
        await sut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);
        var exportIdBefore = await db.StrategyWalkForwardExports.AsNoTracking().Select(e => e.Id).SingleAsync();

        var result = await sut.ImportAsync(strategyId, await ModifiedWfUploadAsync(WfName), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Replaced);
        (await db.StrategyWalkForwardExports.CountAsync()).Should().Be(1, "a strategy has at most one export");
        (await db.StrategyWalkForwardExports.AsNoTracking().Select(e => e.Id).SingleAsync()).Should().Be(exportIdBefore);
        (await db.WalkForwardWindows.CountAsync()).Should().Be(6, "the prior windows are gone, not accumulated");
        (await db.WalkForwardWindows.AsNoTracking().FirstAsync(w => w.RowIndex == 0)).NetProfitOos.Should().Be(1830.15m);
    }

    [Fact]
    public async Task ImportAsync_TwoStrategies_EachOwnsItsExport()
    {
        using var db = CreateDb();
        var s1 = await SeedStrategyAsync(db, "S1");
        var s2 = await SeedStrategyAsync(db, "S2");
        var sut = CreateSut(db);

        await sut.ImportAsync(s1, await UploadAsync(WfName), CancellationToken.None);
        var second = await sut.ImportAsync(s2, await UploadAsync(WfName), CancellationToken.None);

        second.Outcome.Should().Be(BacktestImportOutcome.Imported);
        (await db.StrategyWalkForwardExports.CountAsync()).Should().Be(2);
        (await db.WalkForwardWindows.CountAsync()).Should().Be(12);
    }

    [Fact]
    public async Task ImportAsync_ARejectedFile_ReportsTheParserReasonAndWritesNothing()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        var result = await sut.ImportAsync(strategyId, await UploadAsync(TradeListName), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Rejected);
        result.Reason.Should().Contain("walk-forward-export header");
        result.WindowCount.Should().BeNull();
        (await db.StrategyWalkForwardExports.CountAsync()).Should().Be(0);
    }

    // ---- WF-7 / WF-8: order independence ----

    [Fact]
    public async Task RunImportedBeforeItsExport_BecomesEvaluableWithNoReImportAndNoTradeRewritten()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var tradeListSut = CreateTradeListSut(db);
        var wfSut = CreateSut(db);

        await tradeListSut.ImportTradeListAsync(
            strategyId, BacktestRunKind.Evaluation, await UploadAsync(TradeListName), CancellationToken.None);

        var run = await db.BacktestRuns.AsNoTracking().SingleAsync();
        (await db.BacktestTrades.CountAsync(t => t.BacktestRunId == run.Id)).Should().Be(329);

        // No export yet: the boundary is not merely empty, it is unobtainable.
        OosWindow.Resolver.TryGetOosWindow(run, export: null, out var noWindow).Should().BeFalse();
        noWindow.Should().BeNull();

        var tradesBefore = await db.BacktestTrades.AsNoTracking()
            .Where(t => t.BacktestRunId == run.Id)
            .Select(t => new { t.Id, t.CreatedAt, t.UpdatedAt })
            .OrderBy(t => t.Id)
            .ToListAsync();

        await wfSut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        var export = await db.StrategyWalkForwardExports.AsNoTracking().SingleAsync();
        OosWindow.Resolver.TryGetOosWindow(run, export, out var window).Should().BeTrue();
        window!.FromInclusive.Should().Be(ExpectedBoundary);

        var tradesAfter = await db.BacktestTrades.AsNoTracking()
            .Where(t => t.BacktestRunId == run.Id)
            .Select(t => new { t.Id, t.CreatedAt, t.UpdatedAt })
            .OrderBy(t => t.Id)
            .ToListAsync();

        tradesAfter.Should().BeEquivalentTo(
            tradesBefore,
            "importing the export must make the boundary available without rewriting a single trade — "
            + "that is the whole payoff of the export owning OosFromDate instead of the run copying it");
        tradesAfter.Should().OnlyContain(t => t.UpdatedAt == null);

        var oosTrades = window.Filter(await db.BacktestTrades.AsNoTracking().Where(t => t.BacktestRunId == run.Id).ToListAsync()).ToList();
        oosTrades.Should().NotBeEmpty("the fixture carries trades after 2025-05-26");
        oosTrades.Should().OnlyContain(t => t.CloseTime >= ExpectedBoundary);
    }

    [Fact]
    public async Task ExportImportedWithNoRunYet_PersistsAndLeavesNothingEvaluable()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        var sut = CreateSut(db);

        var result = await sut.ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        result.Outcome.Should().Be(BacktestImportOutcome.Imported);
        result.OosFromDate.Should().Be(ExpectedBoundary);
        (await db.BacktestRuns.CountAsync()).Should().Be(0);

        var evaluable = await EvaluableStrategyIdsAsync(db, strategyId);
        evaluable.Should().BeEmpty("a boundary with no run to apply it to evaluates nothing");
    }

    [Fact]
    public async Task DeployRunPlusExport_IsNotEvaluableEvenThoughBothExist()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        await CreateTradeListSut(db).ImportTradeListAsync(
            strategyId, BacktestRunKind.Deploy, await UploadAsync(TradeListName), CancellationToken.None);
        await CreateSut(db).ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        var evaluable = await EvaluableStrategyIdsAsync(db, strategyId);

        evaluable.Should().BeEmpty("a Deploy run's trades are in-sample no matter how late they close");
    }

    [Fact]
    public async Task EvaluationRunPlusExport_IsEvaluable()
    {
        using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "S1");
        await CreateTradeListSut(db).ImportTradeListAsync(
            strategyId, BacktestRunKind.Evaluation, await UploadAsync(TradeListName), CancellationToken.None);
        await CreateSut(db).ImportAsync(strategyId, await UploadAsync(WfName), CancellationToken.None);

        var evaluable = await EvaluableStrategyIdsAsync(db, strategyId);

        evaluable.Should().Equal(strategyId);
    }

    /// <summary>
    /// The ids the grid would paint green, resolved through the ONE aggregate that owns the
    /// boundary comparison (see <c>OosWindow.Resolver</c>).
    /// </summary>
    private static async Task<List<Guid>> EvaluableStrategyIdsAsync(AppDbContext db, params Guid[] strategyIds)
        => (await OosWindow.Resolver
                .ReadinessRows(db.Strategies, db.BacktestRuns, db.BacktestTrades, db.StrategyWalkForwardExports, strategyIds)
                .ToListAsync())
            .Where(r => r.HasOosEvidence)
            .Select(r => r.StrategyId)
            .ToList();
}
