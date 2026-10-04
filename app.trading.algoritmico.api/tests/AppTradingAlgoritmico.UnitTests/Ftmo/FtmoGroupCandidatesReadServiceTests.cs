using System.Data.Common;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B3.2 (design.md D8) — the picker's candidates read, over the EF in-memory provider
/// (no real database is touched), except the query-count fence which needs a relational provider and runs on
/// in-memory SQLite.
/// </summary>
public class FtmoGroupCandidatesReadServiceTests
{
    private const string Gold = "XAUUSD_CND";
    private const string Dax = "GER40_CND";
    private const string NoSpec = "NOSPEC_CND";
    private const string NoPointValue = "NQ_CND";

    private static readonly Guid AccountX = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid AccountY = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    private static DateTime At(int day, int hour) => new(2026, 1, day, hour, 0, 0, DateTimeKind.Unspecified);

    // ---- fixtures ----

    private static async Task SeedStrategyAsync(AppDbContext db, Guid id, string name, Guid account, string? symbol = null)
    {
        db.Strategies.Add(new Strategy { Id = id, Name = name, CreatedAt = DateTime.UtcNow, TradingAccountId = account, Symbol = symbol });
        await db.SaveChangesAsync();
    }

    /// <summary>A run of <paramref name="tradeCount"/> trades: the i-th opens Jan (1+i) 08:00 and closes at 09:00.</summary>
    private static async Task<Guid> SeedRunAsync(
        AppDbContext db, Guid strategyId, BacktestRunKind kind, string? symbol, int tradeCount, int firstDay = 1)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "cand.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = kind,
            Symbol = symbol,
            CreatedAt = DateTime.UtcNow,
        };
        db.BacktestRuns.Add(run);
        for (var i = 0; i < tradeCount; i++)
        {
            db.BacktestTrades.Add(new BacktestTrade
            {
                BacktestRunId = run.Id,
                RowIndex = i,
                Ticket = i + 1,
                Symbol = symbol ?? string.Empty,
                Type = "Long",
                OpenTime = At(firstDay + i, 8),
                OpenPrice = 100m,
                Size = 1m,
                CloseTime = At(firstDay + i, 9),
                ClosePrice = 101m,
                Profit = 10m,
                Balance = 10_000m,
                SampleTypeRaw = "InSample",
                Segment = BacktestSegment.InSample,
                CloseType = "TP",
            });
        }

        await db.SaveChangesAsync();
        return run.Id;
    }

    private static async Task SeedSpecAsync(
        AppDbContext db, string symbol, string currency = "USD", string zone = "Asia/Jerusalem", decimal contractSize = 100m)
    {
        db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
        {
            SqxSymbol = symbol,
            FtmoSymbol = symbol,
            ContractSize = contractSize,
            ProfitCurrency = currency,
            SizeDecimals = 2,
            Step = 0.01m,
            MinLot = 0.01m,
            MaxLots = 1000m,
            SourceTimeZoneId = zone,
            Provenance = "test",
            CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCalibrationAsync(
        AppDbContext db, string symbol, decimal? pointValue = 100m, CalibrationStatus status = CalibrationStatus.Calibrated)
    {
        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Symbol = symbol,
            Status = status,
            PointValue = pointValue,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static Task<FtmoGroupCandidatesDto> Read(AppDbContext db, Guid account, CancellationToken ct = default)
        => new FtmoGroupCandidatesReadService(db).GetCandidatesAsync(account, ct);

    private static FtmoGroupCandidateDto Single(FtmoGroupCandidatesDto dto, Guid strategyId)
        => dto.Candidates.Single(c => c.StrategyId == strategyId);

    // ---- B3.2.1: scope, presence, facts ----

    [Fact]
    public async Task Candidates_AreScopedToTheRequestedAccountOnly_AndCarryTheAccountIdAndTheCap()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedStrategyAsync(db, Id(1), "x-one", AccountX);
        await SeedStrategyAsync(db, Id(2), "x-two", AccountX);
        await SeedStrategyAsync(db, Id(3), "y-one", AccountY);
        await SeedStrategyAsync(db, Id(4), "no-account", account: Guid.Empty);

        var dto = await Read(db, AccountX);

        dto.TradingAccountId.Should().Be(AccountX);
        dto.MaxMembers.Should().Be(FtmoGroupSimulationLimits.MaxMembers);
        dto.Candidates.Select(c => c.StrategyId).Should().BeEquivalentTo([Id(1), Id(2)]);
    }

    [Fact]
    public async Task Candidates_AnAccountWithNoStrategies_IsAnEmptyListNotAnError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedStrategyAsync(db, Id(3), "y-one", AccountY);

        var dto = await Read(db, AccountX);

        dto.Candidates.Should().BeEmpty();
        dto.TradingAccountId.Should().Be(AccountX);
    }

    [Fact]
    public async Task Candidates_ReportRunPresenceCountAndRangePerKind_AndAbsenceAsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedSpecAsync(db, Gold);
        await SeedCalibrationAsync(db, Gold);
        await SeedStrategyAsync(db, Id(1), "deploy-only", AccountX, symbol: Gold);
        var runId = await SeedRunAsync(db, Id(1), BacktestRunKind.Deploy, Gold, tradeCount: 5, firstDay: 3);
        await SeedStrategyAsync(db, Id(2), "none", AccountX);
        await SeedStrategyAsync(db, Id(3), "empty-run", AccountX);
        await SeedRunAsync(db, Id(3), BacktestRunKind.Evaluation, Gold, tradeCount: 0);

        var dto = await Read(db, AccountX);

        var deployOnly = Single(dto, Id(1));
        deployOnly.Symbol.Should().Be(Gold, "the strategy's own symbol, verbatim");
        deployOnly.Evaluation.Should().BeNull();
        deployOnly.Deploy.Should().NotBeNull();
        deployOnly.Deploy!.RunId.Should().Be(runId);
        deployOnly.Deploy.TradeCount.Should().Be(5);
        deployOnly.Deploy.FirstOpen.Should().Be(At(3, 8));
        deployOnly.Deploy.LastClose.Should().Be(At(7, 9));

        var none = Single(dto, Id(2));
        none.Deploy.Should().BeNull();
        none.Evaluation.Should().BeNull();

        var emptyRun = Single(dto, Id(3)).Evaluation!;
        emptyRun.TradeCount.Should().Be(0);
        emptyRun.FirstOpen.Should().BeNull();
        emptyRun.LastClose.Should().BeNull();
    }

    [Fact]
    public async Task Candidates_FlagSpecAndCalibrationPerRun_SoDeployAndEvaluationSymbolsCanDiffer()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedSpecAsync(db, Gold);
        await SeedCalibrationAsync(db, Gold);
        await SeedStrategyAsync(db, Id(1), "mixed", AccountX, symbol: Gold);
        await SeedRunAsync(db, Id(1), BacktestRunKind.Deploy, Gold, 2);
        await SeedRunAsync(db, Id(1), BacktestRunKind.Evaluation, NoSpec, 2);

        var candidate = Single(await Read(db, AccountX), Id(1));

        candidate.Deploy!.HasInstrumentSpec.Should().BeTrue();
        candidate.Deploy.IsCalibrated.Should().BeTrue();
        candidate.Evaluation!.HasInstrumentSpec.Should().BeFalse("no FTMO instrument spec for that run's symbol");
        candidate.Evaluation.IsCalibrated.Should().BeFalse();
        candidate.Evaluation.ProfitCurrency.Should().BeNull();
        candidate.Evaluation.SourceTimeZoneId.Should().BeNull();
        candidate.Evaluation.NeedsFxBand.Should().BeFalse("without a spec there is no currency to need a band for");
    }

    [Fact]
    public async Task Candidates_IsCalibrated_MirrorsTheSimulationRule_NullPointValueOrNotCalibratedOrNonPositiveIsFalse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        foreach (var symbol in new[] { Gold, NoPointValue, "INSUFF_CND", "ZERO_CND", "MISSING_CND" })
            await SeedSpecAsync(db, symbol);

        await SeedCalibrationAsync(db, Gold);
        await SeedCalibrationAsync(db, NoPointValue, pointValue: null);
        await SeedCalibrationAsync(db, "INSUFF_CND", status: CalibrationStatus.InsufficientSamples);
        await SeedCalibrationAsync(db, "ZERO_CND", pointValue: 0m);

        var symbols = new[] { Gold, NoPointValue, "INSUFF_CND", "ZERO_CND", "MISSING_CND" };
        for (var i = 0; i < symbols.Length; i++)
        {
            await SeedStrategyAsync(db, Id(i + 1), $"s{i}", AccountX);
            await SeedRunAsync(db, Id(i + 1), BacktestRunKind.Deploy, symbols[i], 1);
        }

        var dto = await Read(db, AccountX);

        Single(dto, Id(1)).Deploy!.IsCalibrated.Should().BeTrue();
        foreach (var i in new[] { 2, 3, 4, 5 })
        {
            var run = Single(dto, Id(i)).Deploy!;
            run.HasInstrumentSpec.Should().BeTrue();
            run.IsCalibrated.Should().BeFalse($"{symbols[i - 1]} has no usable calibration");
        }
    }

    [Fact]
    public async Task Candidates_AnUnusableSpec_CountsAsNoSpec_ExactlyAsTheSimulationRefusesIt()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedSpecAsync(db, Gold, contractSize: 0m);
        await SeedCalibrationAsync(db, Gold);
        await SeedStrategyAsync(db, Id(1), "bad-spec", AccountX);
        await SeedRunAsync(db, Id(1), BacktestRunKind.Deploy, Gold, 1);

        var run = Single(await Read(db, AccountX), Id(1)).Deploy!;

        run.HasInstrumentSpec.Should().BeFalse();
        run.ProfitCurrency.Should().BeNull();
        run.NeedsFxBand.Should().BeFalse();
    }

    [Fact]
    public async Task Candidates_NeedsFxBandFollowsTheSettlementCurrency_AndTheZoneAndCurrencyAreEchoed()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedSpecAsync(db, Gold, currency: "USD", zone: "Asia/Jerusalem");
        await SeedSpecAsync(db, Dax, currency: "EUR", zone: "Europe/Athens");
        await SeedCalibrationAsync(db, Gold);
        await SeedCalibrationAsync(db, Dax);
        await SeedStrategyAsync(db, Id(1), "usd", AccountX);
        await SeedRunAsync(db, Id(1), BacktestRunKind.Deploy, Gold, 1);
        await SeedStrategyAsync(db, Id(2), "eur", AccountX);
        await SeedRunAsync(db, Id(2), BacktestRunKind.Deploy, Dax, 1);

        var dto = await Read(db, AccountX);

        var usd = Single(dto, Id(1)).Deploy!;
        usd.ProfitCurrency.Should().Be("USD");
        usd.NeedsFxBand.Should().BeFalse();
        usd.SourceTimeZoneId.Should().Be("Asia/Jerusalem");
        var eur = Single(dto, Id(2)).Deploy!;
        eur.ProfitCurrency.Should().Be("EUR");
        eur.NeedsFxBand.Should().BeTrue();
        eur.SourceTimeZoneId.Should().Be("Europe/Athens");
    }

    [Fact]
    public async Task Candidates_NameExistsOnOtherAccount_IsCaseInsensitive_AndIgnoresTheSameAccount()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedStrategyAsync(db, Id(1), "Alpha", AccountX);
        await SeedStrategyAsync(db, Id(2), "Beta", AccountX);
        await SeedStrategyAsync(db, Id(3), "Gamma", AccountX);
        await SeedStrategyAsync(db, Id(4), "Gamma", AccountX);
        await SeedStrategyAsync(db, Id(5), "ALPHA", AccountY);
        await SeedStrategyAsync(db, Id(6), "Delta", AccountY);

        var dto = await Read(db, AccountX);

        Single(dto, Id(1)).NameExistsOnOtherAccount.Should().BeTrue("the same name in another case on another account");
        Single(dto, Id(2)).NameExistsOnOtherAccount.Should().BeFalse();
        Single(dto, Id(3)).NameExistsOnOtherAccount.Should().BeFalse("a duplicate on the SAME account is not 'another account'");
        Single(dto, Id(4)).NameExistsOnOtherAccount.Should().BeFalse();
    }

    [Fact]
    public async Task Candidates_ACancelledToken_Throws()
    {
        await using var db = InMemoryDbContextFactory.Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => Read(db, AccountX, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- fixed number of queries ----

    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (string.Equals(property.GetColumnType(), "nvarchar(max)", StringComparison.OrdinalIgnoreCase))
                    property.SetColumnType(null);
            }
        }
    }

    private sealed class CountingInterceptor : DbCommandInterceptor
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
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task<(int Commands, FtmoGroupCandidatesDto Dto)> CountCommandsAsync(int strategyCount)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var interceptor = new CountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        await using var db = new SqliteAppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        await SeedSpecAsync(db, Gold);
        await SeedCalibrationAsync(db, Gold);
        await SeedStrategyAsync(db, Id(900), "Name1", AccountY);
        for (var i = 1; i <= strategyCount; i++)
        {
            await SeedStrategyAsync(db, Id(i), $"Name{i}", AccountX, Gold);
            await SeedRunAsync(db, Id(i), BacktestRunKind.Deploy, Gold, 3);
            await SeedRunAsync(db, Id(i), BacktestRunKind.Evaluation, Gold, 3);
        }

        interceptor.Reset();
        var dto = await Read(db, AccountX);
        return (interceptor.Count, dto);
    }

    [Fact]
    public async Task TheNumberOfDatabaseCommands_IsTheSameForTwoStrategiesAndForSix_AndNeverAboveFive()
    {
        var (two, dtoTwo) = await CountCommandsAsync(2);
        var (six, dtoSix) = await CountCommandsAsync(6);

        dtoTwo.Candidates.Should().HaveCount(2).And.OnlyContain(c => c.Deploy != null && c.Evaluation != null && c.Deploy.TradeCount == 3);
        dtoSix.Candidates.Should().HaveCount(6);
        dtoSix.Candidates.Single(c => c.Name == "Name1").NameExistsOnOtherAccount.Should().BeTrue();
        six.Should().Be(two, "the queries are batched with Contains over id and symbol lists and the name flag is a subquery, none per strategy");
        two.Should().BeLessThanOrEqualTo(5);
    }
}
