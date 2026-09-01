using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Tests EF Core mapping for the four backtest-import tables. Uses SQLite in-memory because EF
/// InMemory does NOT enforce unique indexes. Pattern mirrors
/// <c>StrategyWorkflow/StrategyConfigurationTests.cs</c>.
/// </summary>
public class BacktestSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BacktestTestDbContext> _options;

    public BacktestSchemaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BacktestTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new BacktestTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static Strategy NewStrategy(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CreatedAt = DateTime.UtcNow,
    };

    private static BacktestRun NewRun(string hash, Guid strategyId, BacktestRunKind kind = BacktestRunKind.Deploy) => new()
    {
        Id = Guid.NewGuid(),
        SourceFileName = $"{hash}.csv",
        ContentHash = hash,
        StrategyId = strategyId,
        Kind = kind,
        Symbol = "XAUUSD_M1_UTC02",
        CreatedAt = DateTime.UtcNow,
    };

    private static BacktestTrade NewTrade(Guid runId, int rowIndex, long ticket) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = runId,
        RowIndex = rowIndex,
        Ticket = ticket,
        Symbol = "XAUUSD_M1_UTC02",
        Type = "Buy",
        OpenTime = DateTime.UtcNow,
        OpenPrice = 1.0m,
        Size = 0.1m,
        CloseTime = DateTime.UtcNow,
        ClosePrice = 2.0m,
        Profit = 100m,
        Balance = 1000m,
        SampleTypeRaw = "IST",
        Segment = BacktestSegment.InSampleTest,
        CloseType = "PT",
        CreatedAt = DateTime.UtcNow,
    };

    private static StrategyWalkForwardExport NewExport(Guid strategyId) => new()
    {
        Id = Guid.NewGuid(),
        StrategyId = strategyId,
        OosFromDate = new DateTime(2025, 5, 26),
        DeployParameters = "TEMAPeriod1=32,",
        EvaluationParameters = "TEMAPeriod1=35,",
        ContentHash = "wf-hash",
        SourceFileName = "WFParamsExport.csv",
        CreatedAt = DateTime.UtcNow,
    };

    private static WalkForwardWindow NewWindow(Guid exportId, int rowIndex) => new()
    {
        Id = Guid.NewGuid(),
        ExportId = exportId,
        RowIndex = rowIndex,
        PeriodIsStart = new DateTime(2016, 1, 1),
        PeriodIsEnd = new DateTime(2021, 3, 19),
        PeriodOosStart = new DateTime(2021, 3, 20),
        PeriodOosEnd = new DateTime(2022, 4, 5),
        DaysIs = 1904,
        DaysOos = 381,
        NetProfitIs = 15239.94m,
        RetDdRatioIs = 20.68m,
        DrawdownIs = 736.89m,
        AvgTradesPerMonthIs = 2.58m,
        Parameters = "TEMAPeriod1=32,",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task BacktestTrade_DuplicateRowIndexForSameRun_ThrowsUniqueConstraintViolation()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("Strat");
        db.Strategies.Add(strategy);
        var run = NewRun("hash-1", strategy.Id);
        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();

        db.BacktestTrades.Add(NewTrade(run.Id, rowIndex: 0, ticket: 5));
        await db.SaveChangesAsync();

        db.BacktestTrades.Add(NewTrade(run.Id, rowIndex: 0, ticket: 6));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task BacktestTrade_SameTicketAcrossRuns_DoesNotThrow()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("Strat");
        db.Strategies.Add(strategy);
        var runA = NewRun("hash-a", strategy.Id, BacktestRunKind.Deploy);
        var runB = NewRun("hash-b", strategy.Id, BacktestRunKind.Evaluation);
        db.BacktestRuns.AddRange(runA, runB);
        await db.SaveChangesAsync();

        db.BacktestTrades.Add(NewTrade(runA.Id, rowIndex: 0, ticket: 5));
        db.BacktestTrades.Add(NewTrade(runB.Id, rowIndex: 0, ticket: 5));

        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync("Ticket collisions across runs are expected and must not be unique");
    }

    [Fact]
    public async Task BacktestRun_SameContentHashUnderTwoStrategies_BothPersist()
    {
        // The same SQX strategy deployed on two accounts is two Strategy rows, and the user imports
        // the SAME exported file for each. Under the previous revision ContentHash was globally
        // unique, so the second import died on an opaque constraint violation.
        using var db = new BacktestTestDbContext(_options);
        var s1 = NewStrategy("Deployed on FTMO-Demo2");
        var s2 = NewStrategy("Deployed on SBDEMO2");
        db.Strategies.AddRange(s1, s2);
        db.BacktestRuns.Add(NewRun("shared-hash", s1.Id));
        await db.SaveChangesAsync();

        db.BacktestRuns.Add(NewRun("shared-hash", s2.Id));
        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync("ContentHash is a de-dup key, not identity");
        (await db.BacktestRuns.AsNoTracking().CountAsync(r => r.ContentHash == "shared-hash")).Should().Be(2);
    }

    [Fact]
    public async Task BacktestRun_SecondRunInTheSameSlot_ThrowsUniqueConstraintViolation()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(NewRun("hash-x", strategy.Id, BacktestRunKind.Deploy));
        await db.SaveChangesAsync();

        db.BacktestRuns.Add(NewRun("hash-y", strategy.Id, BacktestRunKind.Deploy));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("(StrategyId, Kind) is the run identity");
    }

    [Fact]
    public async Task BacktestRun_TheTwoKindsOfOneStrategy_AreSeparateSlots()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(NewRun("hash-deploy", strategy.Id, BacktestRunKind.Deploy));
        await db.SaveChangesAsync();

        db.BacktestRuns.Add(NewRun("hash-eval", strategy.Id, BacktestRunKind.Evaluation));
        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
        (await db.BacktestRuns.AsNoTracking().CountAsync(r => r.StrategyId == strategy.Id)).Should().Be(2);
    }

    [Fact]
    public async Task SymbolCalibration_DuplicateSymbol_ThrowsUniqueConstraintViolation()
    {
        using var db = new BacktestTestDbContext(_options);
        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Id = Guid.NewGuid(),
            Symbol = "XAUUSD_M1_UTC02",
            SampleCount = 10,
            Status = CalibrationStatus.Calibrated,
            CalibratedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Id = Guid.NewGuid(),
            Symbol = "XAUUSD_M1_UTC02",
            SampleCount = 5,
            Status = CalibrationStatus.Calibrated,
            CalibratedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void BacktestTrade_HasNonUniqueIndexOnBacktestRunIdAndSegment()
    {
        using var db = new BacktestTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(BacktestTrade))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["BacktestRunId", "Segment"]));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void BacktestTrade_TicketIndex_IsNotUnique()
    {
        using var db = new BacktestTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(BacktestTrade))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["Ticket"]));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void TextColumnLengths_ComeFromTheSharedConstants()
    {
        // The parser rejects over-length values BEFORE they reach a length-bounded column. That
        // only works while both sides read the same number, so this fences the EF configuration
        // against a re-hardcoded literal drifting away from BacktestFieldLengths.
        using var db = new BacktestTestDbContext(_options);

        var trade = db.Model.FindEntityType(typeof(BacktestTrade))!;
        trade.FindProperty(nameof(BacktestTrade.Symbol))!.GetMaxLength().Should().Be(BacktestFieldLengths.Symbol);
        trade.FindProperty(nameof(BacktestTrade.Type))!.GetMaxLength().Should().Be(BacktestFieldLengths.TradeType);
        trade.FindProperty(nameof(BacktestTrade.SampleTypeRaw))!.GetMaxLength().Should().Be(BacktestFieldLengths.SampleTypeRaw);
        trade.FindProperty(nameof(BacktestTrade.CloseType))!.GetMaxLength().Should().Be(BacktestFieldLengths.CloseType);
        trade.FindProperty(nameof(BacktestTrade.Comment))!.GetMaxLength().Should().Be(BacktestFieldLengths.Comment);

        var run = db.Model.FindEntityType(typeof(BacktestRun))!;
        run.FindProperty(nameof(BacktestRun.SourceFileName))!.GetMaxLength().Should().Be(BacktestFieldLengths.FileNameOrKey);
        run.FindProperty(nameof(BacktestRun.Symbol))!.GetMaxLength().Should().Be(BacktestFieldLengths.Symbol);
        run.FindProperty(nameof(BacktestRun.ContentHash))!.GetMaxLength().Should().Be(BacktestFieldLengths.ContentHash);

        var export = db.Model.FindEntityType(typeof(StrategyWalkForwardExport))!;
        export.FindProperty(nameof(StrategyWalkForwardExport.SourceFileName))!.GetMaxLength().Should().Be(BacktestFieldLengths.FileNameOrKey);
        export.FindProperty(nameof(StrategyWalkForwardExport.ContentHash))!.GetMaxLength().Should().Be(BacktestFieldLengths.ContentHash);
        export.FindProperty(nameof(StrategyWalkForwardExport.DeployParameters))!.GetMaxLength().Should().Be(BacktestFieldLengths.WalkForwardParameters);
        export.FindProperty(nameof(StrategyWalkForwardExport.EvaluationParameters))!.GetMaxLength().Should().Be(BacktestFieldLengths.WalkForwardParameters);

        var window = db.Model.FindEntityType(typeof(WalkForwardWindow))!;
        window.FindProperty(nameof(WalkForwardWindow.Parameters))!.GetMaxLength().Should().Be(BacktestFieldLengths.WalkForwardParameters);
    }

    [Fact]
    public async Task DeletingAStrategy_DeletesItsRunsAndTheirTrades()
    {
        // BEHAVIOUR CHANGE from the previous revision, where a cascade removed only link rows and
        // left the run orphaned while still reporting itself as attributed.
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        var run = NewRun("hash-cascade", strategy.Id);
        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();

        db.BacktestTrades.Add(NewTrade(run.Id, rowIndex: 0, ticket: 1));
        db.BacktestTrades.Add(NewTrade(run.Id, rowIndex: 1, ticket: 2));
        await db.SaveChangesAsync();

        db.Strategies.Remove(strategy);
        await db.SaveChangesAsync();

        (await db.BacktestRuns.AsNoTracking().CountAsync()).Should().Be(0, "the run is owned by the strategy");
        (await db.BacktestTrades.AsNoTracking().CountAsync()).Should().Be(0, "its trades go with it");
    }

    [Fact]
    public async Task DeletingAStrategy_DeletesItsWalkForwardExportAndWindows()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        var export = NewExport(strategy.Id);
        db.StrategyWalkForwardExports.Add(export);
        await db.SaveChangesAsync();

        db.WalkForwardWindows.Add(NewWindow(export.Id, 0));
        db.WalkForwardWindows.Add(NewWindow(export.Id, 1));
        await db.SaveChangesAsync();

        db.Strategies.Remove(strategy);
        await db.SaveChangesAsync();

        (await db.StrategyWalkForwardExports.AsNoTracking().CountAsync()).Should().Be(0);
        (await db.WalkForwardWindows.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StrategyWalkForwardExport_SecondExportForOneStrategy_ThrowsUniqueConstraintViolation()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        db.StrategyWalkForwardExports.Add(NewExport(strategy.Id));
        await db.SaveChangesAsync();

        db.StrategyWalkForwardExports.Add(NewExport(strategy.Id));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("a strategy has at most one walk-forward export");
    }

    [Fact]
    public async Task WalkForwardWindow_DuplicateRowIndexForOneExport_ThrowsUniqueConstraintViolation()
    {
        using var db = new BacktestTestDbContext(_options);
        var strategy = NewStrategy("S1");
        db.Strategies.Add(strategy);
        var export = NewExport(strategy.Id);
        db.StrategyWalkForwardExports.Add(export);
        db.WalkForwardWindows.Add(NewWindow(export.Id, 0));
        await db.SaveChangesAsync();

        db.WalkForwardWindows.Add(NewWindow(export.Id, 0));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("row order is meaningful, so the ordinal is part of the key");
    }

    [Fact]
    public void BacktestTrade_HasNonUniqueIndexOnBacktestRunIdAndCloseTime()
    {
        // The readiness marker asks "any trade at or after the boundary?" for a whole page of
        // strategies in one query. Without this index that aggregate scans the trade table.
        using var db = new BacktestTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(BacktestTrade))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["BacktestRunId", "CloseTime"]));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void BacktestRun_ContentHashIndex_IsNotUnique()
    {
        using var db = new BacktestTestDbContext(_options);
        var entity = db.Model.FindEntityType(typeof(BacktestRun))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["ContentHash"]));

        index.Should().NotBeNull("calibration de-duplicates by content hash and needs the index");
        index!.IsUnique.Should().BeFalse("the same bytes legitimately back two runs under two strategies");
    }

    [Fact]
    public void Precision_MatchesDesignD1()
    {
        using var db = new BacktestTestDbContext(_options);

        var trade = db.Model.FindEntityType(typeof(BacktestTrade))!;
        trade.FindProperty(nameof(BacktestTrade.Size))!.GetPrecision().Should().Be(18);
        trade.FindProperty(nameof(BacktestTrade.Size))!.GetScale().Should().Be(5);
        trade.FindProperty(nameof(BacktestTrade.OpenPrice))!.GetPrecision().Should().Be(18);
        trade.FindProperty(nameof(BacktestTrade.OpenPrice))!.GetScale().Should().Be(5);
        trade.FindProperty(nameof(BacktestTrade.Profit))!.GetPrecision().Should().Be(18);
        trade.FindProperty(nameof(BacktestTrade.Profit))!.GetScale().Should().Be(2);

        var calibration = db.Model.FindEntityType(typeof(SymbolCalibration))!;
        calibration.FindProperty(nameof(SymbolCalibration.PointValue))!.GetPrecision().Should().Be(18);
        calibration.FindProperty(nameof(SymbolCalibration.PointValue))!.GetScale().Should().Be(6);
    }
}

/// <summary>
/// Minimal DbContext for EF configuration tests — excludes Identity/unrelated tables. Also
/// implements <c>IBacktestDbContext</c>, which is what lets
/// <see cref="BacktestImportRetrySafetyTests"/> drive the import service against REAL SQLite
/// transactions and REAL unique-index enforcement.
/// <para>
/// <see cref="BacktestImportServiceTests"/> does NOT use this context — it builds the full
/// <c>AppDbContext</c> on EF InMemory, because its non-contamination regression must assert
/// against <c>StrategyTrades</c>, which <c>IBacktestDbContext</c> deliberately cannot see. The
/// trade-off is real and worth stating: EF InMemory enforces NO unique index, so any defect whose
/// only symptom is a duplicate-key violation is invisible there and belongs in a SQLite test.
/// </para>
/// </summary>
public class BacktestTestDbContext : DbContext, AppTradingAlgoritmico.Application.Interfaces.IBacktestDbContext
{
    public BacktestTestDbContext(DbContextOptions<BacktestTestDbContext> options) : base(options) { }

    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<BacktestTrade> BacktestTrades => Set<BacktestTrade>();
    public DbSet<SymbolCalibration> SymbolCalibrations => Set<SymbolCalibration>();
    public DbSet<StrategyWalkForwardExport> StrategyWalkForwardExports => Set<StrategyWalkForwardExport>();
    public DbSet<WalkForwardWindow> WalkForwardWindows => Set<WalkForwardWindow>();
    public DbSet<Strategy> Strategies => Set<Strategy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new BacktestRunConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestTradeConfiguration());
        modelBuilder.ApplyConfiguration(new SymbolCalibrationConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyWalkForwardExportConfiguration());
        modelBuilder.ApplyConfiguration(new WalkForwardWindowConfiguration());

        // Minimal Strategy — only what the backtest foreign keys need.
        modelBuilder.Entity<Strategy>(b =>
        {
            b.ToTable("Strategies");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Ignore(x => x.MonthlyPerformance);
            b.Ignore(x => x.Comments);
            b.Ignore(x => x.Trades);
            b.Ignore(x => x.BatchStage);
            b.Ignore(x => x.TradingAccount);
        });
    }
}
