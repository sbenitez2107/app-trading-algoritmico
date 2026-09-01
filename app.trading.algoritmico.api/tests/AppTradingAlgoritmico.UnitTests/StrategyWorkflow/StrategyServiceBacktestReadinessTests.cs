using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// AS-1: the backtest-readiness marker returned by <c>StrategyService.GetByAccountAsync</c>.
/// <para>
/// The marker answers "which of these can I actually use?", not "which have I touched". White means
/// no evidence at all; amber means the strategy can be SIZED but not honestly EVALUATED; green
/// means an evaluation run, its walk-forward boundary and at least one trade past that boundary all
/// exist. It is derived on every read and has no column behind it — there is deliberately nothing a
/// user could flip to call an overfitted strategy evaluable (design.md D14).
/// </para>
/// <para>
/// These are value tests on EF InMemory. The COST claim — one command per page regardless of page
/// size — needs a relational provider to be measurable at all and lives in
/// <c>BacktestReadinessQueryCostTests</c>.
/// </para>
/// </summary>
public class StrategyServiceBacktestReadinessTests
{
    private static readonly DateTime Boundary = new(2025, 5, 26);

    private static StrategyService CreateSut(AppDbContext db)
        => new(db, new Mock<ISqxParserService>().Object, new Mock<IHtmlReportParserService>().Object);

    private static TradingAccount NewAccount(Guid id) => new()
    {
        Id = id,
        Name = "Acc",
        Broker = "Darwinex",
        AccountNumber = 1,
        Login = 1,
        PasswordEncrypted = "e",
        Server = "s",
        InitialBalance = 100_000m,
        CreatedAt = DateTime.UtcNow,
    };

    private static Guid AddStrategy(AppDbContext db, Guid accountId, string name)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = name, TradingAccountId = accountId, CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        return strategy.Id;
    }

    private static Guid AddRun(AppDbContext db, Guid strategyId, BacktestRunKind kind)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = $"{kind}.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = kind,
            Symbol = "XAUUSD_M1_UTC02",
            CreatedAt = DateTime.UtcNow,
        };
        db.BacktestRuns.Add(run);
        return run.Id;
    }

    private static void AddTrade(AppDbContext db, Guid runId, int rowIndex, DateTime closeTime)
        => db.BacktestTrades.Add(new BacktestTrade
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
        });

    private static void AddExport(AppDbContext db, Guid strategyId)
        => db.StrategyWalkForwardExports.Add(new StrategyWalkForwardExport
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            OosFromDate = Boundary,
            DeployParameters = "TEMAPeriod1=32,",
            EvaluationParameters = "TEMAPeriod1=35,",
            ContentHash = Guid.NewGuid().ToString("N"),
            SourceFileName = "WFParamsExport_XAUUSD_H1.csv",
            CreatedAt = DateTime.UtcNow,
        });

    private static async Task<BacktestReadiness> ReadinessAsync(Action<AppDbContext, Guid> arrange)
    {
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var db = InMemoryDbContextFactory.Create(dbName))
        {
            db.TradingAccounts.Add(NewAccount(accountId));
            await db.SaveChangesAsync();
            arrange(db, accountId);
            await db.SaveChangesAsync();
        }

        using var readDb = InMemoryDbContextFactory.Create(dbName);
        var page = await CreateSut(readDb).GetByAccountAsync(accountId, 1, 500);
        return page.Items.Single().BacktestReadiness;
    }

    [Fact]
    public async Task GetByAccountAsync_StrategyWithNoRun_IsNone()
    {
        var readiness = await ReadinessAsync((db, accountId) => AddStrategy(db, accountId, "NoRun"));

        readiness.Should().Be(BacktestReadiness.None);
    }

    [Fact]
    public async Task GetByAccountAsync_DeployRunOnly_IsSizingOnly()
    {
        var readiness = await ReadinessAsync((db, accountId) =>
        {
            var id = AddStrategy(db, accountId, "DeployOnly");
            var runId = AddRun(db, id, BacktestRunKind.Deploy);
            AddTrade(db, runId, 0, Boundary.AddYears(1));
        });

        readiness.Should().Be(
            BacktestReadiness.SizingOnly,
            "a Deploy run's trades are in-sample however late they close — sizing yes, evaluation no");
    }

    [Fact]
    public async Task GetByAccountAsync_EvaluationRunWithoutItsExport_IsStillSizingOnly()
    {
        var readiness = await ReadinessAsync((db, accountId) =>
        {
            var id = AddStrategy(db, accountId, "EvalNoExport");
            var runId = AddRun(db, id, BacktestRunKind.Evaluation);
            AddTrade(db, runId, 0, Boundary.AddYears(1));
        });

        readiness.Should().Be(
            BacktestReadiness.SizingOnly,
            "the boundary is unavailable, not assumed satisfied");
    }

    [Fact]
    public async Task GetByAccountAsync_EvaluationRunAndExportButNoTradeAfterTheBoundary_IsSizingOnly()
    {
        var readiness = await ReadinessAsync((db, accountId) =>
        {
            var id = AddStrategy(db, accountId, "NoOosTrade");
            var runId = AddRun(db, id, BacktestRunKind.Evaluation);
            AddTrade(db, runId, 0, Boundary.AddDays(-1));
            AddExport(db, id);
        });

        readiness.Should().Be(BacktestReadiness.SizingOnly);
    }

    [Fact]
    public async Task GetByAccountAsync_EvaluationRunExportAndAnOosTrade_IsEvaluable()
    {
        var readiness = await ReadinessAsync((db, accountId) =>
        {
            var id = AddStrategy(db, accountId, "Evaluable");
            var runId = AddRun(db, id, BacktestRunKind.Evaluation);
            AddTrade(db, runId, 0, Boundary.AddDays(-1));
            AddTrade(db, runId, 1, Boundary);
            AddExport(db, id);
        });

        readiness.Should().Be(
            BacktestReadiness.Evaluable,
            "a trade closing exactly on the boundary is already out of sample");
    }

    [Fact]
    public async Task GetByAccountAsync_DeployRunAlongsideAnEvaluableEvaluationRun_DoesNotDowngradeIt()
    {
        var readiness = await ReadinessAsync((db, accountId) =>
        {
            var id = AddStrategy(db, accountId, "Both");
            AddRun(db, id, BacktestRunKind.Deploy);
            var evalRunId = AddRun(db, id, BacktestRunKind.Evaluation);
            AddTrade(db, evalRunId, 0, Boundary.AddDays(10));
            AddExport(db, id);
        });

        readiness.Should().Be(BacktestReadiness.Evaluable);
    }

    [Fact]
    public async Task GetByAccountAsync_OneStrategysEvidenceDoesNotLeakOntoAnother()
    {
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var db = InMemoryDbContextFactory.Create(dbName))
        {
            db.TradingAccounts.Add(NewAccount(accountId));
            await db.SaveChangesAsync();

            var evaluable = AddStrategy(db, accountId, "AAA Evaluable");
            var bare = AddStrategy(db, accountId, "BBB Bare");
            await db.SaveChangesAsync();

            var runId = AddRun(db, evaluable, BacktestRunKind.Evaluation);
            AddTrade(db, runId, 0, Boundary.AddDays(3));
            AddExport(db, evaluable);
            await db.SaveChangesAsync();

            bare.Should().NotBe(evaluable);
        }

        using var readDb = InMemoryDbContextFactory.Create(dbName);
        var page = await CreateSut(readDb).GetByAccountAsync(accountId, 1, 500);

        page.Items.Should().HaveCount(2);
        page.Items.Single(i => i.Name == "AAA Evaluable").BacktestReadiness.Should().Be(BacktestReadiness.Evaluable);
        page.Items.Single(i => i.Name == "BBB Bare").BacktestReadiness.Should().Be(BacktestReadiness.None);
    }
}
