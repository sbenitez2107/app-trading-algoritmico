using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b.4 — the full computation over the shortlist: shortlist-only, parity with the shipped group
/// service, the two budgets (time through an injected delegate, never a real clock) and cancellation.
/// </summary>
public class FtmoGroupSearchSimulationTests
{
    private static readonly SearchOptions Pairs = new(2, 2);

    private static ProjectedTrade[] Rows(int seed) =>
        [.. Enumerable.Range(0, 30).Select(i =>
            Trade(i, At(1, 1, 9).AddDays(i * 3), At(1, 1, 10).AddDays(i * 3), ((i + seed) % 4 == 0) ? -40m : 15m))];

    private static FtmoProjectionCache Pool(int size) =>
        Cache([.. Enumerable.Range(1, size).Select(n => Distinct(n, $"SYM{n}", Rows(n)))]);

    private static List<ShortlistedCandidate> Shortlist(FtmoProjectionCache cache, int count = 3)
    {
        var plan = Plan(cache, _ => Resolution(), Pairs, Params(), 50m);
        return [.. plan.Shortlist.Take(count)];
    }

    private static SimulationBudget Unlimited => new(int.MaxValue);

    [Fact]
    public void Simulate_RunsTheFullComputationOncePerShortlistedCandidate_WithBothKinds()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);

        var outcome = Simulate(cache, shortlist, Params(), Unlimited, null, CancellationToken.None);

        shortlist.Should().HaveCount(6);
        outcome.Results.Select(r => r.Candidate).Should().Equal(shortlist);
        outcome.Results.Should().OnlyContain(r => r.Kinds.Count == 2);
        outcome.Results.SelectMany(r => r.Kinds).Count().Should().Be(shortlist.Count * 2);
        outcome.Stop.Should().Be(SearchStopReason.None);
        outcome.NotComputed.Should().Be(0);
        outcome.Cancelled.Should().BeFalse();
    }

    [Fact]
    public void Simulate_ARefusedKind_CarriesItsRefusalAndNoMetrics()
    {
        // Two members with disjoint windows: the proxy would have removed it, so the shortlist is hand-built.
        var early = Distinct(1, "A", Trade(0, At(1, 5, 9), At(1, 5, 10), 10m), Trade(1, At(1, 9, 9), At(1, 9, 10), 10m));
        var late = Distinct(2, "B", Trade(0, At(6, 5, 9), At(6, 5, 10), 10m), Trade(1, At(6, 9, 9), At(6, 9, 10), 10m));
        var cache = Cache(early, late);
        var candidate = new ShortlistedCandidate([Id(1), Id(2)], 0m, 0);

        var outcome = Simulate(cache, [candidate], Params(), Unlimited, null, CancellationToken.None);

        var kinds = outcome.Results.Single().Kinds;
        kinds.Should().HaveCount(2);
        kinds.Should().OnlyContain(k => k.Status == FtmoSimulationStatus.Refused && k.Refusal == FtmoGroupRefusal.NoCommonWindow && k.Run == null);
    }

    [Fact]
    public void Simulate_StopsAtTheSimulationBudget_NamingTheLimitAndCountingWhatWasNotComputed()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);

        var outcome = Simulate(cache, shortlist, Params(), new SimulationBudget(2), null, CancellationToken.None);

        outcome.Results.Should().HaveCount(2);
        outcome.Results.Select(r => r.Candidate).Should().Equal(shortlist.Take(2), "a budget stop cuts the tail in shortlist order");
        outcome.Stop.Should().Be(SearchStopReason.MaxFullSimulations);
        outcome.NotComputed.Should().Be(4);
        outcome.Cancelled.Should().BeFalse();
    }

    [Fact]
    public void Simulate_StopsAtTheWallClockBudget_ThroughTheInjectedCheck_BeforeEachCandidate()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);
        var checks = 0;
        var budget = new SimulationBudget(int.MaxValue, () => ++checks > 3);

        var outcome = Simulate(cache, shortlist, Params(), budget, null, CancellationToken.None);

        outcome.Results.Should().HaveCount(3);
        outcome.Stop.Should().Be(SearchStopReason.WallClock);
        outcome.NotComputed.Should().Be(3);
        checks.Should().Be(4, "the check runs once before each candidate, and the fourth call stops the loop");
    }

    [Fact]
    public void Simulate_AnAlreadyExpiredClock_StopsBeforeTheFirstCandidate()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);

        var outcome = Simulate(cache, shortlist, Params(), new SimulationBudget(1, () => true), null, CancellationToken.None);

        outcome.Results.Should().BeEmpty();
        outcome.Stop.Should().Be(SearchStopReason.WallClock);
        outcome.NotComputed.Should().Be(6);
    }

    [Fact]
    public void Simulate_ACancelledToken_ReturnsThePartialResultsComputedSoFar_FlaggedCancelled()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);
        using var cts = new CancellationTokenSource();

        // The loop itself never inspects the token; only ComputeGroup does, so the cancel must flow into it.
        var outcome = Simulate(cache, shortlist, Params(), Unlimited, p => { if (p.Done == 2) cts.Cancel(); }, cts.Token);

        outcome.Cancelled.Should().BeTrue();
        outcome.Results.Select(r => r.Candidate).Should().Equal(shortlist.Take(2));
        outcome.NotComputed.Should().Be(4);
        outcome.Stop.Should().Be(SearchStopReason.None, "cancelling is not a budget stop");
    }

    [Fact]
    public void Simulate_PartialResultsOfACancelledRun_AreDeterministic()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);

        SimulationOutcome Run()
        {
            using var cts = new CancellationTokenSource();
            return Simulate(cache, shortlist, Params(), Unlimited, p => { if (p.Done == 3) cts.Cancel(); }, cts.Token);
        }

        var a = Run();
        var b = Run();

        a.Results.Should().HaveCount(3);
        b.Should().BeEquivalentTo(a, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public void Simulate_ProgressIsMonotonic_AndEndsAtTheNumberComputed()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);
        var seen = new List<SimulationProgress>();

        var outcome = Simulate(cache, shortlist, Params(), Unlimited, seen.Add, CancellationToken.None);

        seen.Select(s => s.Done).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        seen.Should().OnlyContain(s => s.Total == shortlist.Count);
        seen[^1].Done.Should().Be(outcome.Results.Count);
    }

    [Fact]
    public void Simulate_TwoIdenticalRuns_ProduceIdenticalShortlistsAndMetrics()
    {
        var cache = Pool(4);
        var shortlist = Shortlist(cache, 6);

        var a = Simulate(cache, shortlist, Params(), Unlimited, null, CancellationToken.None);
        var b = Simulate(cache, Shortlist(cache, 6), Params(), Unlimited, null, CancellationToken.None);

        b.Should().BeEquivalentTo(a, o => o.ComparingRecordsByMembers());
    }

    // ---- 1b.4.2: parity with the shipped group service (the real SimulateAsync path) ----

    private static readonly DateTime Day0 = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static BacktestTrade MakeTrade(int row, string symbol, decimal profit, DateTime close) => new()
    {
        RowIndex = row,
        Ticket = row + 1,
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
    };

    private static async Task SeedAsync(AppDbContext db, int members)
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
        for (var n = 1; n <= members; n++)
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
            var id = Id(n);
            db.Strategies.Add(new Strategy { Id = id, Name = $"S{n}", CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
            foreach (var (kind, shift) in new[] { (BacktestRunKind.Deploy, 0), (BacktestRunKind.Evaluation, 2) })
            {
                var run = new BacktestRun
                {
                    Id = Guid.NewGuid(),
                    SourceFileName = "p.csv",
                    ContentHash = Guid.NewGuid().ToString("N"),
                    StrategyId = id,
                    Kind = kind,
                    Symbol = symbol,
                    CreatedAt = DateTime.UtcNow,
                };
                db.BacktestRuns.Add(run);
                for (var i = 0; i < 90; i++)
                {
                    var k = i + shift + n;
                    var profit = i < 3 ? -1m : (k % 7 == 3 ? 400m : (k % 5 == 2 ? -150m : 20m));
                    var trade = MakeTrade(i, symbol, profit, Day0.AddDays(i));
                    trade.BacktestRunId = run.Id;
                    db.BacktestTrades.Add(trade);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Simulate_EveryShortlistedCandidate_HasMetricsEqualToADirectRunOfTheGroupSimulation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedAsync(db, 3);
        var ids = Enumerable.Range(1, 3).Select(Id).ToList();
        var parameters = new FtmoGroupSimulationParameters(ids, "FTMO", 10_000m, 100m, null, null, 2, 0.01m, 0.01m, 10m);
        var ct = CancellationToken.None;

        // The same stages the group service runs, then the search engine on the cached projections.
        var names = await FtmoGroupMemberResolution.LoadNamesAsync(db, ids, ct);
        var limits = await FtmoSimulationInputs.ResolveLimitsAsync(db, "FTMO", ct);
        var resolved = await FtmoGroupMemberResolution.LoadMembersAsync(db, ids, ids, names, null, null, ct);
        var trades = await FtmoGroupMemberResolution.LoadTradesAsync(db, resolved.Members, ct);
        FtmoSimulationInputs.TryResolveBerlin(out var berlin).Should().BeTrue();
        var groupParams = FtmoGroupMemberResolution.BuildGroupParams(resolved.Members, resolved.Resolve, limits, berlin!, 10_000m, null, null);
        var cache = FtmoProjectionCache.Build(resolved.Members, trades, parameters.TryBuildSourceGrid()!, 100m, resolved.Resolve, groupParams);

        var plan = Plan(cache, resolved.Resolve, new SearchOptions(2, 3), groupParams, 100m);
        var outcome = Simulate(cache, plan.Shortlist, groupParams, Unlimited, null, ct);

        plan.Shortlist.Should().HaveCount(4, "three pairs and the triple");
        outcome.Results.Should().HaveCount(4);
        foreach (var result in outcome.Results)
        {
            var direct = await new FtmoGroupSimulationReadService(db).SimulateAsync(parameters with { MemberStrategyIds = result.Candidate.MemberIds }, ct);
            direct.Status.Should().Be(FtmoSimulationStatus.Evaluated);
            result.Kinds.Should().BeEquivalentTo(direct.Kinds, o => o.ComparingRecordsByMembers());
            result.Kinds.Should().OnlyContain(k => k.Run != null && k.Run.Starts.Count > 1, "an empty comparison would prove nothing");
        }
    }
}
