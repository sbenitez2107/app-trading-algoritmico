using System.Runtime.CompilerServices;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b.1 (design D2) — the per-job projection cache: one input per (StrategyId, Kind), rebound per
/// candidate with the member's index in ascending-StrategyId order, plus the shared close-instant day dictionary,
/// each member's instrument set and coverage. The cache holds no static reference, so it dies with the job.
/// </summary>
public class FtmoProjectionCacheTests
{
    private static Strat Sample(int n, string symbol = "EURUSD") =>
        Distinct(n, symbol, Trade(0, At(1, 12, 9), At(1, 12, 10), 10m), Trade(1, At(1, 14, 9), At(1, 14, 11), -5m));

    [Fact]
    public void Input_IsKeyedByStrategyAndKind_AndKindsDiffer()
    {
        var cache = Cache(Sample(2), Sample(1));

        var deploy = cache.Input(Id(1), BacktestRunKind.Deploy);
        var eval = cache.Input(Id(1), BacktestRunKind.Evaluation);

        deploy.StrategyId.Should().Be(Id(1));
        deploy.Projection!.ProjectedLow![0].Net.Should().Be(10m);
        eval.Projection!.ProjectedLow![0].Net.Should().Be(11m);
    }

    [Fact]
    public void Members_AreHeldInAscendingStrategyIdOrder_WhateverOrderTheyWereGiven()
    {
        var cache = Cache(Sample(3), Sample(1), Sample(2));

        cache.Members.Select(m => m.StrategyId).Should().Equal(Id(1), Id(2), Id(3));
    }

    [Fact]
    public void Rebind_AssignsTheIndexInTheCandidate_AndLeavesTheCachedInputsUntouched()
    {
        var cache = Cache(Sample(1), Sample(2), Sample(3));

        var rebound = cache.Rebind([Id(2), Id(3)], BacktestRunKind.Deploy);

        rebound.Select(m => (m.StrategyId, m.MemberOrder)).Should().Equal((Id(2), 0), (Id(3), 1));
        cache.Input(Id(3), BacktestRunKind.Deploy).MemberOrder.Should().Be(0, "the cached placeholder is never mutated");
        rebound[1].Projection.Should().BeSameAs(cache.Input(Id(3), BacktestRunKind.Deploy).Projection, "the projection is shared, not copied");
    }

    [Fact]
    public void DayOf_SharesOneBookkeepingDayPerCloseInstant_ForEveryMember()
    {
        var cache = Cache(Sample(1), Sample(2));

        cache.DayOf(At(1, 12, 10)).Should().Be(FtmoDayClock.Attribute(At(1, 12, 10), Jerusalem, Berlin).BookkeepingDay);
        cache.DayOf(At(1, 14, 11)).Should().Be(FtmoDayClock.Attribute(At(1, 14, 11), Jerusalem, Berlin).BookkeepingDay);
    }

    [Fact]
    public void InstrumentsAndCoverage_AreHeldPerMember()
    {
        var cache = Cache(Sample(1, "EURUSD"), Sample(2, "XAUUSD"));

        cache.Instruments(Id(2)).Should().BeEquivalentTo(["XAUUSD"]);
        cache.Coverage(Id(1)).Should().Be((At(1, 12, 9), At(1, 14, 11)));
    }

    [Fact]
    public void Coverage_OfAMemberWithoutRows_IsNull()
    {
        var cache = Cache(new Strat(1, "EURUSD", [], []));

        cache.Coverage(Id(1)).Should().BeNull();
    }

    // ---- Req: Cache released ----

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference BuildAndDrop()
    {
        var cache = Cache(Sample(1), Sample(2));
        cache.Rebind([Id(1), Id(2)], BacktestRunKind.Deploy);
        return new WeakReference(cache);
    }

    [Fact]
    public void Cache_IsCollectedOnceTheJobDropsIt()
    {
        var weak = BuildAndDrop();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        weak.IsAlive.Should().BeFalse("no static state may keep the cache alive past the job");
    }
}
