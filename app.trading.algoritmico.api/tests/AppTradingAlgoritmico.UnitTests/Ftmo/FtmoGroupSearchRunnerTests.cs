using System.Data.Common;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2a.3 (design D1, D7) — the REAL runner over a SQLite database: a fixed query count, the pool
/// refusal, a per-job DI scope that is gone before the CPU stages, the cache released at job end, parity with the
/// engine-direct pipeline and with the group simulation endpoint, and the progress stages.
/// </summary>
public class FtmoGroupSearchRunnerTests
{
    private static readonly Guid Account = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly DateTime Day0 = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The seed number behind a closed-form <see cref="Id"/>.</summary>
    private static int N(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0);

    private static FtmoGroupSearchRequest Request(
        int min = 2, int max = 3, int? maxSims = null, int? maxSeconds = null, bool? identical = null) => new(
        TradingAccountId: Account, MinMembers: min, MaxMembers: max, Broker: "FTMO", InitialCapital: 10_000m,
        TargetRiskPerTrade: 100m, SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m,
        MaxFullSimulations: maxSims, MaxWallClockSeconds: maxSeconds, IncludeIdenticalDeployEval: identical);

    // ---- harness: SQLite + command log + a DI scope whose DbContext records its disposal ----

    private class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
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

    private sealed class TrackingDb(DbContextOptions<AppDbContext> options) : SqliteAppDbContext(options)
    {
        public bool Disposed { get; private set; }

        public override void Dispose()
        {
            Disposed = true;
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class CommandLog : DbCommandInterceptor
    {
        private readonly List<string> _commands = [];

        public IReadOnlyList<string> Commands => _commands;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            _commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=False");
        private ServiceProvider? _provider;
        private DbContextOptions<AppDbContext>? _options;

        public CommandLog Log { get; } = new();

        public List<TrackingDb> Contexts { get; } = [];

        public ManualClock Clock { get; } = new();

        public static async Task<Harness> CreateAsync(int members, bool limits = true, int ineligibleDeployOnly = 0)
        {
            var h = new Harness();
            await h._connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(h._connection).AddInterceptors(h.Log).Options;
            h._options = options;
            h._provider = new ServiceCollection()
                .AddScoped<AppDbContext>(_ =>
                {
                    var db = new TrackingDb(options);
                    h.Contexts.Add(db);
                    return db;
                })
                .BuildServiceProvider();

            await using var seed = new SqliteAppDbContext(options);
            await seed.Database.EnsureCreatedAsync();
            await SeedAsync(seed, members, limits, ineligibleDeployOnly);
            h.Contexts.Clear();
            return h;
        }

        public AppDbContext NewDirectContext() => new SqliteAppDbContext(_options!);

        public FtmoGroupSearchRunner Runner(Action<FtmoProjectionCache>? onCache = null, int? maxPool = null)
            => new(_provider!.GetRequiredService<IServiceScopeFactory>(), Clock)
            {
                OnCacheBuilt = onCache,
                MaxPoolSize = maxPool ?? FtmoGroupSearchLimits.MaxPoolSize,
            };

        public async ValueTask DisposeAsync()
        {
            if (_provider is not null)
                await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static async Task SeedAsync(AppDbContext db, int members, bool limits, int deployOnly)
    {
        if (limits)
        {
            db.BrokerRiskLimits.Add(new BrokerRiskLimits
            {
                Broker = "FTMO",
                FundingService = FundingService.Ftmo,
                Kind = GuardrailKind.LossLimits,
                FtmoProduct = FtmoProduct.TwoStep,
                DrawdownModel = DrawdownModel.Static,
                DailyLossLimitPct = 0.05m,
                MaxLossLimitPct = 0.10m,
                Verified = true,
            });
        }

        // Members 1..members are complete; the next `deployOnly` hold a Deploy run only; one more belongs to ANOTHER account.
        var total = members + deployOnly + 1;
        for (var n = 1; n <= total; n++)
        {
            var symbol = $"PAR{n}";
            db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
            {
                SqxSymbol = symbol,
                FtmoSymbol = symbol,
                ContractSize = 100m,
                ProfitCurrency = "USD",
                SizeDecimals = 2,
                Step = 0.01m,
                MinLot = 0.01m,
                MaxLots = 1000m,
                SourceTimeZoneId = "Asia/Jerusalem",
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
            db.Strategies.Add(new Strategy
            {
                Id = Id(n),
                Name = $"S{n}",
                CreatedAt = DateTime.UtcNow,
                TradingAccountId = n == total ? Guid.NewGuid() : Account,
            });

            var kinds = n > members && n < total
                ? new[] { (BacktestRunKind.Deploy, 0) }
                : [(BacktestRunKind.Deploy, 0), (BacktestRunKind.Evaluation, 2)];
            foreach (var (kind, shift) in kinds)
            {
                var run = new BacktestRun
                {
                    Id = Guid.NewGuid(),
                    SourceFileName = "p.csv",
                    ContentHash = Guid.NewGuid().ToString("N"),
                    StrategyId = Id(n),
                    Kind = kind,
                    Symbol = symbol,
                    CreatedAt = DateTime.UtcNow,
                };
                db.BacktestRuns.Add(run);
                for (var i = 0; i < 90; i++)
                {
                    var k = i + shift + n;
                    var profit = i < 3 ? -1m : (k % 7 == 3 ? 400m : (k % 5 == 2 ? -150m : 20m));
                    var close = Day0.AddDays(i);
                    db.BacktestTrades.Add(new BacktestTrade
                    {
                        BacktestRunId = run.Id,
                        RowIndex = i,
                        Ticket = i + 1,
                        Symbol = symbol,
                        Type = "Long",
                        OpenTime = close.AddHours(-1),
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
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static Task<FtmoGroupSearchRunResult> RunAsync(
        FtmoGroupSearchRunner runner, FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto>? report = null, CancellationToken ct = default)
        => runner.RunAsync(request, report ?? (_ => { }), ct);

    // ---- 2a.3.1: the pool is loaded in a fixed number of queries, through the shared stages ----

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public async Task Loading_IssuesTheSameSixQueriesInOrder_WhateverThePoolSize(int members)
    {
        await using var h = await Harness.CreateAsync(members);
        var before = h.Log.Commands.Count;

        await RunAsync(h.Runner(), Request());

        var commands = h.Log.Commands.Skip(before).ToList();
        commands.Should().HaveCount(6, "no per-strategy query (no N+1)");
        string[] tables = ["Strategies", "BrokerRiskLimits", "BacktestRuns", "FtmoInstrumentSpecs", "SymbolCalibrations", "BacktestTrades"];
        for (var i = 0; i < tables.Length; i++)
            commands[i].Should().Contain(tables[i]);
    }

    [Fact]
    public async Task AnAccountWithNoStrategies_ReturnsAnEmptyResult_AfterOneQuery()
    {
        await using var h = await Harness.CreateAsync(3);
        var before = h.Log.Commands.Count;

        var result = await RunAsync(h.Runner(), Request() with { TradingAccountId = Guid.NewGuid() });

        h.Log.Commands.Skip(before).Should().HaveCount(1);
        result.Rows.Should().BeEmpty();
        result.Funnel.Examined.Should().Be(0);
        result.Cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task OnlyTheRequestedAccountsStrategies_AreInThePool()
    {
        await using var h = await Harness.CreateAsync(3);

        var result = await RunAsync(h.Runner(), Request());

        result.Rows.SelectMany(r => r.MemberIds).Distinct().Should().BeEquivalentTo([Id(1), Id(2), Id(3)]);
    }

    // ---- 2a.3.1: the pool-size refusal ----

    [Fact]
    public async Task AnEligiblePoolLargerThanTheMaximum_IsRefusedWithTheCounts_AndRunsNothing()
    {
        await using var h = await Harness.CreateAsync(3);
        var stages = new List<FtmoGroupSearchStage>();

        var act = () => RunAsync(h.Runner(maxPool: 2), Request(), p => stages.Add(p.Stage));

        var ex = (await act.Should().ThrowAsync<FtmoGroupSearchRefusedException>()).Which;
        ex.Message.Should().Contain("3").And.Contain("2");
        stages.Should().NotContain([FtmoGroupSearchStage.Proxy, FtmoGroupSearchStage.Simulating]);
    }

    [Fact]
    public async Task AnEligiblePoolExactlyAtTheMaximum_IsAccepted()
    {
        await using var h = await Harness.CreateAsync(3);

        var result = await RunAsync(h.Runner(maxPool: 3), Request());

        result.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AMissingLimitsRow_IsRefusedWithAMessage()
    {
        await using var h = await Harness.CreateAsync(3, limits: false);

        var act = () => RunAsync(h.Runner(), Request());

        await act.Should().ThrowAsync<FtmoGroupSearchRefusedException>();
    }

    [Fact]
    public async Task AnUnusableRequest_IsRefusedBeforeAnyQuery()
    {
        await using var h = await Harness.CreateAsync(3);
        var before = h.Log.Commands.Count;

        var act = () => RunAsync(h.Runner(), Request() with { TargetRiskPerTrade = 0m });

        await act.Should().ThrowAsync<FtmoGroupSearchRefusedException>();
        h.Log.Commands.Count.Should().Be(before);
    }

    // ---- 2a.3.1: per-job scope disposed BEFORE the CPU stages; cache released at the end ----

    [Fact]
    public async Task TheJobScopeAndItsDbContext_AreDisposedBeforeTheCpuStagesStart()
    {
        await using var h = await Harness.CreateAsync(3);
        var disposedAt = new List<(FtmoGroupSearchStage Stage, bool AllDisposed)>();

        await RunAsync(h.Runner(), Request(), p => disposedAt.Add((p.Stage, h.Contexts.Count > 0 && h.Contexts.All(c => c.Disposed))));

        h.Contexts.Should().HaveCount(1, "one scope, one DbContext, per job");
        disposedAt.Where(x => x.Stage == FtmoGroupSearchStage.Loading).Should().OnlyContain(x => !x.AllDisposed);
        disposedAt.Where(x => x.Stage is FtmoGroupSearchStage.Proxy or FtmoGroupSearchStage.Simulating or FtmoGroupSearchStage.Ranking)
            .Should().NotBeEmpty().And.OnlyContain(x => x.AllDisposed, "the CPU-heavy stages never hold the connection");
    }

    [Fact]
    public async Task TheJobScope_IsDisposedEvenWhenTheJobIsRefused()
    {
        await using var h = await Harness.CreateAsync(3);

        var act = () => RunAsync(h.Runner(maxPool: 1), Request());

        await act.Should().ThrowAsync<FtmoGroupSearchRefusedException>();
        h.Contexts.Should().ContainSingle().Which.Disposed.Should().BeTrue();
    }

    /// <summary>Returns the runner too: the caller keeps it alive, so a cache leaked into a runner field is caught.</summary>
    private static async Task<(WeakReference Cache, FtmoGroupSearchRunResult Result, FtmoGroupSearchRunner Runner)> RunCapturingCacheAsync(
        Harness h, FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto>? report = null, CancellationToken ct = default)
    {
        WeakReference? weak = null;
        var runner = h.Runner(c => weak = new WeakReference(c));
        var result = await RunAsync(runner, request, report, ct);
        return (weak!, result, runner);
    }

    private static void CollectUntilDead(WeakReference weak)
    {
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public async Task TheProjectionCache_IsReleasedWhenTheJobCompletes()
    {
        await using var h = await Harness.CreateAsync(3);

        var (cache, result, runner) = await RunCapturingCacheAsync(h, Request());

        result.Rows.Should().NotBeEmpty();
        CollectUntilDead(cache);
        cache.IsAlive.Should().BeFalse("the cache dies with the job");
        GC.KeepAlive(runner);
    }

    [Fact]
    public async Task TheProjectionCache_IsReleasedWhenTheJobIsCancelled()
    {
        await using var h = await Harness.CreateAsync(3);
        using var cts = new CancellationTokenSource();

        var (cache, result, runner) = await RunCapturingCacheAsync(
            h, Request(), p => { if (p.Stage == FtmoGroupSearchStage.Simulating && p.FullSimulationsDone == 1) cts.Cancel(); }, cts.Token);

        result.Cancelled.Should().BeTrue();
        CollectUntilDead(cache);
        cache.IsAlive.Should().BeFalse();
        GC.KeepAlive(runner);
    }

    // ---- 2a.3.1: mapping, progress, budgets ----

    [Fact]
    public async Task Progress_ReportsTheStagesInOrder_AndSimulatingIsMonotonic()
    {
        await using var h = await Harness.CreateAsync(3);
        var reports = new List<FtmoGroupSearchProgressDto>();

        await RunAsync(h.Runner(), Request(), reports.Add);

        var stages = reports.Select(r => r.Stage).Distinct().ToList();
        stages.Should().Equal(
            FtmoGroupSearchStage.Loading, FtmoGroupSearchStage.Eligibility, FtmoGroupSearchStage.Proxy,
            FtmoGroupSearchStage.Simulating, FtmoGroupSearchStage.Ranking);
        reports.Select(r => r.Stage).Should().BeInAscendingOrder();
        var done = reports.Where(r => r.Stage == FtmoGroupSearchStage.Simulating).Select(r => r.FullSimulationsDone).ToList();
        done.Should().BeInAscendingOrder().And.EndWith(4);
        reports.Where(r => r.Stage == FtmoGroupSearchStage.Simulating).Should().OnlyContain(r => r.Total == 4);
    }

    [Fact]
    public async Task TheResult_MapsTheFunnelRowsHeadroomAndSymbols()
    {
        await using var h = await Harness.CreateAsync(3, ineligibleDeployOnly: 1);

        var result = await RunAsync(h.Runner(), Request());

        result.Funnel.Examined.Should().Be(4, "three pairs and the triple, counted before any filtering");
        result.Funnel.Shortlisted.Should().Be(4);
        result.FullySimulated.Should().Be(4);
        result.StopReason.Should().Be(FtmoGroupSearchStopReason.None);
        result.Ineligible.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { StrategyId = Id(4), Name = "S4", Reason = FtmoGroupSearchIneligibleReason.MissingKind });
        result.Rows.Select(r => r.Rank).Should().Equal(1, 2, 3, 4);
        foreach (var row in result.Rows)
        {
            row.MemberNames.Should().Equal(row.MemberIds.Select(id => $"S{N(id)}"));
            row.Symbols.Should().Equal(row.MemberIds.Select(id => $"PAR{N(id)}"));
            row.Kinds.Should().HaveCount(2);
            row.KindHeadrooms.Should().HaveCount(2).And.OnlyContain(k => k.Headroom == 1m - Math.Max(k.WorstDailyUsed, k.WorstMaxUsed));
            row.Headroom.Should().Be(row.KindHeadrooms.Min(k => k.Headroom), "the rank scalar is the worse kind");
            row.IdenticalDeployEval.Should().BeFalse();
        }
    }

    [Fact]
    public async Task TheSimulationBudget_StopsTheJob_NamingTheLimit_AndCountingWhatWasNotComputed()
    {
        await using var h = await Harness.CreateAsync(3);

        var result = await RunAsync(h.Runner(), Request(maxSims: 2));

        result.StopReason.Should().Be(FtmoGroupSearchStopReason.MaxFullSimulations);
        result.FullySimulated.Should().Be(2);
        result.NotComputed.Should().Be(2);
        result.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task TheWallClockBudget_ComesFromTheRequest_AndStopsTheJob()
    {
        await using var h = await Harness.CreateAsync(3);

        var result = await RunAsync(
            h.Runner(), Request(maxSeconds: 10),
            p => { if (p.Stage == FtmoGroupSearchStage.Simulating && p.FullSimulationsDone == 1) h.Clock.Advance(TimeSpan.FromSeconds(11)); });

        result.StopReason.Should().Be(FtmoGroupSearchStopReason.WallClock);
        result.FullySimulated.Should().Be(1);
        result.NotComputed.Should().Be(3);
    }

    [Fact]
    public async Task ACancelDuringTheSimulation_ReturnsThePartialRows_FlaggedCancelled()
    {
        await using var h = await Harness.CreateAsync(3);
        using var cts = new CancellationTokenSource();

        var result = await RunAsync(
            h.Runner(), Request(),
            p => { if (p.Stage == FtmoGroupSearchStage.Simulating && p.FullSimulationsDone == 2) cts.Cancel(); }, cts.Token);

        result.Cancelled.Should().BeTrue();
        result.FullySimulated.Should().Be(2);
        result.Rows.Should().HaveCount(2);
    }

    // ---- 2a.3.1 parity (hard rule 4c): the runner equals the engine-direct pipeline and the group endpoint ----

    [Fact]
    public async Task TheRunnersRows_EqualTheEngineDirectPipeline_AndTheGroupSimulationEndpoint()
    {
        await using var h = await Harness.CreateAsync(3);
        var result = await RunAsync(h.Runner(), Request());

        await using var db = h.NewDirectContext();
        var ids = new[] { Id(1), Id(2), Id(3) };
        var ct = CancellationToken.None;
        var names = await FtmoGroupMemberResolution.LoadNamesAsync(db, ids, ct);
        var limits = await FtmoSimulationInputs.ResolveLimitsAsync(db, "FTMO", ct);
        var resolved = await FtmoGroupMemberResolution.LoadMembersAsync(db, ids, ids, names, null, null, ct);
        var trades = await FtmoGroupMemberResolution.LoadTradesAsync(db, resolved.Members, ct);
        FtmoSimulationInputs.TryResolveBerlin(out var berlin).Should().BeTrue();
        var gp = FtmoGroupMemberResolution.BuildGroupParams(resolved.Members, resolved.Resolve, limits, berlin!, 10_000m, null, null);
        var parameters = new FtmoGroupSimulationParameters(ids, "FTMO", 10_000m, 100m, null, null, 2, 0.01m, 0.01m, 10m);
        var cache = FtmoProjectionCache.Build(resolved.Members, trades, parameters.TryBuildSourceGrid()!, 100m, resolved.Resolve, gp);
        var plan = FtmoGroupSearchEngine.Plan(cache, resolved.Resolve, new FtmoGroupSearchEngine.SearchOptions(2, 3), gp, 100m);
        var outcome = FtmoGroupSearchEngine.Simulate(cache, plan.Shortlist, gp, new FtmoGroupSearchEngine.SimulationBudget(int.MaxValue), null, ct);
        var ranked = FtmoGroupSearchRanking.Rank(outcome.Results.Select(r => FtmoGroupSearchRanking.BuildEntry(cache, r, gp)));

        result.Rows.Select(r => r.MemberIds).Should().BeEquivalentTo(ranked.Select(r => r.Entry.MemberIds), o => o.WithStrictOrdering());
        result.Rows.Select(r => r.Headroom).Should().Equal(ranked.Select(r => r.Entry.Headroom));
        result.Rows.Select(r => r.WithinCeiling).Should().Equal(ranked.Select(r => r.WithinCeiling));

        foreach (var row in result.Rows)
        {
            var direct = await new FtmoGroupSimulationReadService(db).SimulateAsync(parameters with { MemberStrategyIds = row.MemberIds }, ct);
            direct.Status.Should().Be(FtmoSimulationStatus.Evaluated);
            row.Kinds.Should().BeEquivalentTo(direct.Kinds, o => o.ComparingRecordsByMembers());
            row.Kinds.Should().OnlyContain(k => k.Run != null && k.Run.Starts.Count > 1, "an empty comparison would prove nothing");
        }
    }

    [Fact]
    public async Task TwoIdenticalJobs_ProduceIdenticalResults()
    {
        await using var h = await Harness.CreateAsync(3);

        var a = await RunAsync(h.Runner(), Request());
        var b = await RunAsync(h.Runner(), Request());

        b.Should().BeEquivalentTo(a, o => o.ComparingRecordsByMembers().WithStrictOrdering());
    }

    // ---- 2a review correction: RESILIENCE-002 / RESILIENCE-003 ----

    [Fact]
    public async Task APoolAboveTheMaximum_IsRefusedBeforeTheTradesAreLoaded_AndNoCacheIsBuilt()
    {
        await using var h = await Harness.CreateAsync(3, ineligibleDeployOnly: 2);
        var before = h.Log.Commands.Count;
        var cacheBuilt = false;

        var act = () => RunAsync(h.Runner(_ => cacheBuilt = true, maxPool: 2), Request());

        var ex = (await act.Should().ThrowAsync<FtmoGroupSearchRefusedException>()).Which;
        ex.Message.Should().Contain("3").And.Contain("2");
        var commands = h.Log.Commands.Skip(before).ToList();
        commands.Should().HaveCount(5, "the refusal is decided on the five loading queries, before the trades");
        commands.Should().NotContain(c => c.Contains("BacktestTrades", StringComparison.Ordinal));
        cacheBuilt.Should().BeFalse();
    }

    [Fact]
    public async Task ThePoolCheck_IgnoresMembersThatCannotBeEligible()
    {
        await using var h = await Harness.CreateAsync(3, ineligibleDeployOnly: 2);

        var result = await RunAsync(h.Runner(maxPool: 3), Request());

        result.Rows.Should().NotBeEmpty("the two deploy-only strategies lack a kind, so the pool counts 3, not 5");
    }

    [Fact]
    public async Task ACancelDuringTheProxyStage_ReturnsCancelledWithNoRows_AndNeverSimulates()
    {
        await using var h = await Harness.CreateAsync(3);
        using var cts = new CancellationTokenSource();
        var stages = new List<FtmoGroupSearchStage>();

        var result = await RunAsync(
            h.Runner(), Request(),
            p => { stages.Add(p.Stage); if (p.Stage == FtmoGroupSearchStage.Proxy) cts.Cancel(); }, cts.Token);

        result.Cancelled.Should().BeTrue();
        result.Rows.Should().BeEmpty();
        result.FullySimulated.Should().Be(0);
        stages.Should().NotContain(FtmoGroupSearchStage.Simulating);
    }

    [Fact]
    public async Task TheCpuStages_RunOnADedicatedThread_NotOnAPoolThread()
    {
        await using var h = await Harness.CreateAsync(3);
        bool? onPool = null;

        await RunAsync(h.Runner(_ => onPool = Thread.CurrentThread.IsThreadPoolThread), Request());

        onPool.Should().BeFalse("a 15-minute CPU job must not hold a request-handling pool thread");
    }
}
