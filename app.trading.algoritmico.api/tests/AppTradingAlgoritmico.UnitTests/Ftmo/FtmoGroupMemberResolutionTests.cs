using System.Data.Common;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMemberResolution;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1a.1 (design D1) — the staged member resolution extracted from
/// <see cref="FtmoGroupSimulationReadService"/>. The stages must return what the inline code produced, and the
/// group service must keep issuing the same queries in the same order (names, limits, then runs/specs/
/// calibrations, then trades), so a refusal never pays for a later query.
/// </summary>
public class FtmoGroupMemberResolutionTests
{
    private const string Broker = "FTMO";
    private const string Gold = "XAUUSD_MRS";
    private const string Euro = "EURGBP_MRS";
    private const string NoSpec = "NOSPEC_MRS";
    private static readonly DateTime SafeDay = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    // ---- fixtures ----

    private static BacktestTrade MakeTrade(int row, string symbol, decimal profit, DateTime open, DateTime close) => new()
    {
        RowIndex = row,
        Ticket = row + 1,
        Symbol = symbol,
        Type = "Long",
        OpenTime = open,
        OpenPrice = 100m,
        Size = 1.00m,
        CloseTime = close,
        ClosePrice = 101m,
        Profit = profit,
        Balance = 10_000m,
        SampleTypeRaw = "InSample",
        Segment = BacktestSegment.InSample,
        CloseType = profit < 0m ? "SL" : "TP",
        RealizedRisk = profit < 0m ? 100m : null,
    };

    private static List<BacktestTrade> OrderedRun(string symbol, int count, int shift)
    {
        var trades = new List<BacktestTrade>(count);
        for (var i = 0; i < count; i++)
        {
            var close = SafeDay.AddDays(i);
            var profit = i < 3 ? -1m : ((i + shift) % 7 == 3 ? 400m : ((i + shift) % 5 == 2 ? -150m : 20m));
            trades.Add(MakeTrade(i, symbol, profit, close.AddHours(-1), close));
        }

        return trades;
    }

    private static async Task SeedLimitsAsync(AppDbContext db, decimal? dailyPct = 0.05m)
    {
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = dailyPct,
            MaxLossLimitPct = 0.10m,
            Verified = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSymbolAsync(AppDbContext db, string symbol, string currency, string zone, bool spec = true)
    {
        if (spec)
        {
            db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
            {
                SqxSymbol = symbol,
                FtmoSymbol = symbol,
                ContractSize = 100m,
                ProfitCurrency = currency,
                SizeDecimals = 2,
                Step = 0.01m,
                MinLot = 0.01m,
                MaxLots = 1000m,
                SourceTimeZoneId = zone,
                Provenance = "test",
                CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            db.SymbolCalibrations.Add(new SymbolCalibration
            {
                Symbol = symbol,
                Status = CalibrationStatus.Calibrated,
                PointValue = 100m,
                SampleCount = 10,
                CalibratedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedMemberAsync(AppDbContext db, Guid id, string name, string symbol, int shift, bool evaluation = true)
    {
        db.Strategies.Add(new Strategy { Id = id, Name = name, CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        foreach (var kind in evaluation ? new[] { BacktestRunKind.Deploy, BacktestRunKind.Evaluation } : [BacktestRunKind.Deploy])
        {
            var run = new BacktestRun
            {
                Id = Guid.NewGuid(),
                SourceFileName = "resolution.csv",
                ContentHash = Guid.NewGuid().ToString("N"),
                StrategyId = id,
                Kind = kind,
                Symbol = symbol,
                CreatedAt = DateTime.UtcNow,
            };
            db.BacktestRuns.Add(run);
            foreach (var trade in OrderedRun(symbol, 20, shift + (kind == BacktestRunKind.Evaluation ? 2 : 0)))
            {
                trade.BacktestRunId = run.Id;
                db.BacktestTrades.Add(trade);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<AppDbContext> SeedWorldAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, "USD", "Asia/Jerusalem");
        await SeedSymbolAsync(db, Euro, "EUR", "Europe/London");
        await SeedSymbolAsync(db, NoSpec, "USD", "Asia/Jerusalem", spec: false);
        await SeedMemberAsync(db, Id(1), "gold", Gold, shift: 1);
        await SeedMemberAsync(db, Id(2), "euro", Euro, shift: 2);
        return db;
    }

    private static FtmoGroupSimulationParameters Params(IEnumerable<Guid> ids, decimal? fxLow = null, decimal? fxHigh = null) =>
        new([.. ids], Broker, 10_000m, 100m, fxLow, fxHigh, 2, 0.01m, MinLot: 0.01m, MaxLots: 10m);

    private static async Task<ResolvedMembers> ResolveAsync(
        AppDbContext db, IReadOnlyList<Guid> ids, decimal? fxLow = null, decimal? fxHigh = null)
    {
        var names = await LoadNamesAsync(db, ids, CancellationToken.None);
        return await LoadMembersAsync(db, ids, [.. ids.Where(names.ContainsKey)], names, fxLow, fxHigh, CancellationToken.None);
    }

    private static FtmoSimulationInputs.LimitsResolution Limits() => new(null, 0.05m, 0.10m, null);

    private static TimeZoneInfo Berlin()
    {
        FtmoSimulationInputs.TryResolveBerlin(out var zone).Should().BeTrue();
        return zone!;
    }

    // ---- 1a.1.1: the stages return what the inline code produced ----

    [Fact]
    public async Task LoadNamesAsync_ReturnsOnlyTheStrategiesThatExist()
    {
        await using var db = await SeedWorldAsync();

        var names = await LoadNamesAsync(db, [Id(1), Id(2), Id(99)], CancellationToken.None);

        names.Should().BeEquivalentTo(new Dictionary<Guid, string> { [Id(1)] = "gold", [Id(2)] = "euro" });
    }

    [Fact]
    public async Task LoadMembersAsync_BuildsOrderedMembersWithTheirRunsAndDisplay()
    {
        await using var db = await SeedWorldAsync();

        var resolved = await ResolveAsync(db, [Id(1), Id(2)], fxLow: 1.05m, fxHigh: 1.10m);

        resolved.Symbols.Should().BeEquivalentTo([Gold, Euro]);
        resolved.Members.Select(m => (m.StrategyId, m.Name, m.Order)).Should().Equal((Id(1), "gold", 0), (Id(2), "euro", 1));
        resolved.Members.Should().OnlyContain(m => m.Runs.Count == 2 && m.SymbolRefusedBy == null && m.Display != null);
        resolved.Members[0].Display!.Spec!.SourceTimeZoneId.Should().Be("Asia/Jerusalem");
        resolved.Members[1].Display!.Spec!.SourceTimeZoneId.Should().Be("Europe/London");
        resolved.Resolve(Gold).Spec.Should().NotBeNull();
        resolved.Resolve(null).Refusal.Should().NotBeNull("a run with no symbol resolves like a symbol with no spec");
    }

    [Fact]
    public async Task LoadMembersAsync_AMemberWithoutAnEvaluationRun_HoldsOnlyTheDeployRun()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, "USD", "Asia/Jerusalem");
        await SeedMemberAsync(db, Id(1), "deploy-only", Gold, shift: 1, evaluation: false);

        var resolved = await ResolveAsync(db, [Id(1)]);

        resolved.Members.Single().Runs.Keys.Should().Equal(BacktestRunKind.Deploy);
    }

    [Fact]
    public async Task LoadTradesAsync_ReturnsTheTradesOfEveryReplayableRun_AndNothingForARefusedSymbol()
    {
        await using var db = await SeedWorldAsync();
        await SeedMemberAsync(db, Id(3), "nospec", NoSpec, shift: 3);

        var resolved = await ResolveAsync(db, [Id(1), Id(3)]);
        var trades = await LoadTradesAsync(db, resolved.Members, CancellationToken.None);

        var gold = resolved.Members.Single(m => m.StrategyId == Id(1));
        var refused = resolved.Members.Single(m => m.StrategyId == Id(3));
        refused.SymbolRefusedBy.Should().NotBeNull();
        trades.Keys.Should().BeEquivalentTo(gold.Runs.Values.Select(r => r.RunId));
        trades.Values.Should().OnlyContain(t => t.Count == 20);
    }

    [Fact]
    public async Task LoadTradesAsync_WithNoReplayableMember_ReturnsEmptyWithoutAQuery()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, NoSpec, "USD", "Asia/Jerusalem", spec: false);
        await SeedMemberAsync(db, Id(3), "nospec", NoSpec, shift: 3);

        var resolved = await ResolveAsync(db, [Id(3)]);
        var trades = await LoadTradesAsync(db, resolved.Members, CancellationToken.None);

        trades.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGroupParams_UsesTheFirstResolvedMemberZone_AndTheLimitsAndCapital()
    {
        await using var db = await SeedWorldAsync();
        var resolved = await ResolveAsync(db, [Id(1), Id(2)], 1.05m, 1.10m);

        var p = BuildGroupParams(resolved.Members, resolved.Resolve, Limits(), Berlin(), 12_345m, 1.05m, 1.10m);

        p.SourceZone!.Id.Should().Be("Asia/Jerusalem", "the first member's zone is the group's source zone");
        p.InitialCapital.Should().Be(12_345m);
        (p.DailyPct, p.MaxPct).Should().Be((0.05m, 0.10m));
        p.Rules.Phase1TargetPct.Should().Be(FtmoChallengeRules.Phase1TargetPct);
    }

    [Fact]
    public async Task BuildGroupParams_TheEchoBandIsTheRequestedBandOnlyWhenThisGroupHoldsANonUsdMember()
    {
        await using var db = await SeedWorldAsync();
        var resolved = await ResolveAsync(db, [Id(1), Id(2)], 1.05m, 1.10m);
        var usdOnly = resolved.Members.Where(m => m.StrategyId == Id(1)).ToList();
        var withEuro = resolved.Members.Where(m => m.StrategyId == Id(2)).ToList();

        var usdParams = BuildGroupParams(usdOnly, resolved.Resolve, Limits(), Berlin(), 10_000m, 1.05m, 1.10m);
        var euroParams = BuildGroupParams(withEuro, resolved.Resolve, Limits(), Berlin(), 10_000m, 1.05m, 1.10m);
        var euroNoBand = BuildGroupParams(withEuro, resolved.Resolve, Limits(), Berlin(), 10_000m, null, null);

        usdParams.EchoFxBand.Should().Be((1m, 1m), "the band is echoed per GROUP, not per pool");
        euroParams.EchoFxBand.Should().Be((1.05m, 1.10m));
        euroNoBand.EchoFxBand.Should().Be((1m, 1m));
        euroParams.SourceZone!.Id.Should().Be("Europe/London", "the zone is also per group");
    }

    [Fact]
    public async Task ToKindInput_ProjectsAHeldRun_RefusesAnUnresolvedSymbol_AndMarksAMissingKind()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, "USD", "Asia/Jerusalem");
        await SeedSymbolAsync(db, NoSpec, "USD", "Asia/Jerusalem", spec: false);
        await SeedMemberAsync(db, Id(1), "gold", Gold, shift: 1, evaluation: false);
        await SeedMemberAsync(db, Id(3), "nospec", NoSpec, shift: 3);
        var parameters = Params([Id(1), Id(3)]);
        var resolved = await ResolveAsync(db, [Id(1), Id(3)]);
        var trades = await LoadTradesAsync(db, resolved.Members, CancellationToken.None);
        var grid = parameters.TryBuildSourceGrid()!;
        var gold = resolved.Members.Single(m => m.StrategyId == Id(1));
        var refused = resolved.Members.Single(m => m.StrategyId == Id(3));

        var held = ToKindInput(gold, BacktestRunKind.Deploy, trades, grid, 100m, resolved.Resolve);
        var missing = ToKindInput(gold, BacktestRunKind.Evaluation, trades, grid, 100m, resolved.Resolve);
        var symbolRefused = ToKindInput(refused, BacktestRunKind.Deploy, trades, grid, 100m, resolved.Resolve);

        held.RunId.Should().Be(gold.Runs[BacktestRunKind.Deploy].RunId);
        held.Refusal.Should().BeNull();
        held.Projection!.ProjectedLow.Should().HaveCount(20);
        held.MemberOrder.Should().Be(gold.Order);
        missing.RunId.Should().BeNull();
        missing.Projection.Should().BeNull();
        symbolRefused.RunId.Should().NotBeNull();
        symbolRefused.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
        symbolRefused.Projection.Should().BeNull();
    }

    // ---- 1a.1.2: query-count pins through the service (stage order and early exits) ----

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

    private sealed class CommandLog : DbCommandInterceptor
    {
        private readonly List<string> _commands = [];

        public IReadOnlyList<string> Commands => _commands;

        public void Reset() => _commands.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            _commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }
    }

    private static async Task<(IReadOnlyList<string> Commands, FtmoGroupSimulationDto Result)> RunAsync(
        bool seedLimits, Func<Guid[]> ids)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var log = new CommandLog();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(log).Options;
        await using var db = new SqliteAppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        if (seedLimits)
            await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, "USD", "Asia/Jerusalem");
        await SeedMemberAsync(db, Id(1), "gold", Gold, shift: 1);
        await SeedMemberAsync(db, Id(2), "gold2", Gold, shift: 2);

        log.Reset();
        var result = await new FtmoGroupSimulationReadService(db).SimulateAsync(Params(ids()), CancellationToken.None);
        return (log.Commands, result);
    }

    [Fact]
    public async Task AnUnknownId_IssuesOnlyTheNamesQuery_NoLimitsRunsSpecsOrTrades()
    {
        var (commands, result) = await RunAsync(seedLimits: true, () => [Id(1), Id(99)]);

        result.Refusal.Should().Be(FtmoGroupRefusal.MemberNotFound);
        commands.Should().HaveCount(1);
        commands[0].Should().Contain("Strategies");
    }

    [Fact]
    public async Task ALimitsRefusal_IssuesOnlyNamesThenLimits_NoRunsSpecsCalibrationsOrTrades()
    {
        var (commands, result) = await RunAsync(seedLimits: false, () => [Id(1), Id(2)]);

        result.Refusal.Should().Be(FtmoGroupRefusal.SharedInputsRefused);
        commands.Should().HaveCount(2);
        commands[0].Should().Contain("Strategies");
        commands[1].Should().Contain("BrokerRiskLimits");
    }

    [Fact]
    public async Task AFullRun_IssuesSixQueriesInOrder_NamesLimitsRunsSpecsCalibrationsTrades()
    {
        var (commands, result) = await RunAsync(seedLimits: true, () => [Id(1), Id(2)]);

        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        commands.Should().HaveCount(6);
        string[] tables = ["Strategies", "BrokerRiskLimits", "BacktestRuns", "FtmoInstrumentSpecs", "SymbolCalibrations", "BacktestTrades"];
        for (var i = 0; i < tables.Length; i++)
            commands[i].Should().Contain(tables[i], $"query {i + 1} is the {tables[i]} query");
    }
}
