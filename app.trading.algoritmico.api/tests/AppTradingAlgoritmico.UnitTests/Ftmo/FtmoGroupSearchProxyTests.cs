using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b.3.3-1b.3.5 — the proxy computed on each candidate's OWN common window (shipped
/// Intersect + Merge, so the window-shrink case is covered), the counted proxy removals, and the shortlist.
/// Capital 10,000 and a 5% daily allowance (500) throughout.
/// </summary>
public class FtmoGroupSearchProxyTests
{
    private static readonly SearchOptions Pairs = new(2, 2);

    private static ProjectedTrade[] Rows(params (int Month, int Day, decimal Net)[] t) =>
        [.. t.Select((x, i) => Trade(i, At(x.Month, x.Day, 9), At(x.Month, x.Day, 10), x.Net))];

    // A and B cover January-December; C only October-December. A's only losing day (-300, March) lies OUTSIDE C's window.
    private static readonly Strat A = Distinct(1, "A", Rows((1, 5, 10m), (3, 10, -300m), (11, 8, 10m), (12, 20, 10m)));
    private static readonly Strat B = Distinct(2, "B", Rows((1, 6, 10m), (3, 12, 10m), (11, 10, 10m), (12, 21, 10m)));
    private static readonly Strat C = Distinct(3, "C", Rows((10, 5, -100m), (10, 6, 10m), (12, 22, 10m)));

    private static ProxyOutcome Proxy(FtmoProjectionCache cache, params int[] strategies) =>
        ComputeProxy(cache, [.. strategies.Select(Id)], Params());

    // ---- 1b.3.3: own window ----

    [Fact]
    public void ComputeProxy_AddingALateStartingMember_ShrinksTheWindowUsedByTheProxy()
    {
        var cache = Cache(A, B, C);

        var ab = Proxy(cache, 1, 2);
        var abc = Proxy(cache, 1, 2, 3);

        ab.DailyUsed.Should().Be(0.6m, "{A,B} spans January-December, so the -300 March day counts: 300 / 500");
        abc.DailyUsed.Should().Be(0.2m, "{A,B,C} is trimmed to October-December: only C's -100 day remains: 100 / 500");
    }

    [Fact]
    public void ComputeProxy_EqualsTheProfileComputedFromScratchOnThatWindowAlone()
    {
        var cache = Cache(A, B, C);
        // Hand-picked rows of the {A,B,C} window (2026-10-05 09:00 .. 2026-12-20 10:00), deploy kind, by close.
        var inWindow = new[]
        {
            Trade(0, At(10, 5, 9), At(10, 5, 10), -100m), Trade(1, At(10, 6, 9), At(10, 6, 10), 10m),
            Trade(2, At(11, 8, 9), At(11, 8, 10), 10m), Trade(3, At(11, 10, 9), At(11, 10, 10), 10m),
            Trade(4, At(12, 20, 9), At(12, 20, 10), 10m),
        };

        var scratch = FtmoDailyLossProfile.Compute(inWindow, cache.DayOf, 10_000m, 0.05m);

        Proxy(cache, 1, 2, 3).DailyUsed.Should().Be(scratch.WorstDayLoss / 500m);
    }

    [Fact]
    public void ComputeProxy_TheSameCandidateInTwoPools_GivesEqualValues()
    {
        var other = Distinct(4, "D", Rows((6, 1, -400m), (6, 2, 20m)));

        Proxy(Cache(A, B), 1, 2).Should().Be(Proxy(Cache(A, B, C, other), 1, 2));
    }

    [Fact]
    public void ComputeProxy_TheScoreIsTheWorseOfBothKinds()
    {
        // Deploy loses 100, Evaluation (every net one higher) loses 99: the worse kind is Deploy.
        var x = Distinct(1, "X", Rows((1, 5, -100m), (1, 20, 10m)));
        var y = Distinct(2, "Y", Rows((1, 4, 10m), (1, 10, 10m), (1, 21, 10m)));

        Proxy(Cache(x, y), 1, 2).DailyUsed.Should().Be(0.2m);
    }

    [Fact]
    public void ComputeProxy_TheScoreIsTheWorseOfBothFxEnds()
    {
        var x = Distinct(1, "X", Rows((1, 5, -100m), (1, 20, 10m))) with { HighNetScale = 3m };
        var y = Distinct(2, "Y", Rows((1, 4, 10m), (1, 10, 10m), (1, 21, 10m)));

        Proxy(Cache(x, y), 1, 2).DailyUsed.Should().Be(0.6m, "the high end loses 300: 300 / 500");
    }

    [Fact]
    public void ComputeProxy_ReportsThePeakConcurrencyOfTheMergedWindow()
    {
        // Anchors fix the window to January 3-29 without overlapping each other; the middle trades overlap.
        var x = Distinct(1, "X", Trade(0, At(1, 2, 9), At(1, 2, 10), 10m), Trade(1, At(1, 5, 9), At(1, 5, 12), 10m), Trade(2, At(1, 30, 9), At(1, 30, 10), 10m));
        var y = Distinct(2, "Y", Trade(0, At(1, 3, 9), At(1, 3, 10), 10m), Trade(1, At(1, 5, 10), At(1, 5, 11), 10m), Trade(2, At(1, 29, 9), At(1, 29, 10), 10m));

        Proxy(Cache(x, y), 1, 2).Peak.Should().Be(2);
    }

    [Fact]
    public void ComputeProxy_DisjointRanges_AreRemovedAsNoCommonWindow()
    {
        var far = Distinct(4, "D", Rows((1, 1, 10m), (1, 2, 10m)));

        ComputeProxy(Cache(A, far), [Id(1), Id(4)], Params()).Removal.Should().Be(FtmoGroupRefusal.NoCommonWindow);
    }

    [Fact]
    public void Plan_NoCommonWindowInOneKind_IsCountedAndNeverShortlisted()
    {
        // Deploy ranges are disjoint (Jan vs Mar) while the Evaluation ones overlap, so the pair prune keeps it.
        var a = new Strat(1, "A", Rows((1, 5, 10m)), Rows((3, 5, 10m)));
        var b = new Strat(2, "B", Rows((3, 5, 10m)), Rows((3, 5, 11m)));

        var plan = Plan(Cache(a, b), _ => Resolution(), Pairs, Params(), 50m);

        plan.Funnel.Remaining.Should().Be(1);
        plan.RemovedNoCommonWindow.Should().Be(1);
        plan.Shortlist.Should().BeEmpty();
    }

    [Fact]
    public void Plan_AMemberWithNoRowsInTheWindow_IsCountedAndNeverShortlisted()
    {
        // The window is February 1-2 (A's whole range); B trades in January and March only.
        var a = Distinct(1, "A", Rows((2, 1, 10m), (2, 2, 10m)));
        var b = Distinct(2, "B", Rows((1, 1, 10m), (3, 1, 10m)));

        var plan = Plan(Cache(a, b), _ => Resolution(), Pairs, Params(), 50m);

        plan.RemovedMemberHasNoTrades.Should().Be(1);
        plan.Shortlist.Should().BeEmpty();
    }

    // ---- 1b.3.4: shortlist ----

    private static ProxyOutcome P(decimal daily, int peak = 1, FtmoGroupRefusal? removal = null) => new(removal, daily, peak);

    private static readonly int[][] Survivors = [[0, 1], [0, 2], [1, 2], [0, 1, 2], [0, 1, 3], [0, 2, 3]];

    [Fact]
    public void Shortlist_FillsAPerSizeQuotaBestProxyFirst_AndReturnsGlobalProxyOrder()
    {
        ProxyOutcome[] proxies = [P(.1m), P(.5m), P(.6m), P(.2m), P(.3m), P(.4m)];

        Shortlist(Survivors, proxies, new(2, 3, ShortlistSize: 4)).Should().Equal(0, 3, 4, 1);
    }

    [Fact]
    public void Shortlist_TheRemainderGoesToTheGlobalProxyOrder()
    {
        ProxyOutcome[] proxies = [P(.1m), P(.5m), P(.6m), P(.2m), P(.3m), P(.4m)];

        Shortlist(Survivors, proxies, new(2, 3, ShortlistSize: 5)).Should().Equal(0, 3, 4, 5, 1);
    }

    [Fact]
    public void Shortlist_ASizeWithTooFewCandidates_LetsTheOthersFillItsQuota()
    {
        int[][] survivors = [[0, 1], [0, 2], [1, 2], [0, 1, 2]];
        ProxyOutcome[] proxies = [P(.1m), P(.2m), P(.3m), P(.9m)];

        Shortlist(survivors, proxies, new(2, 3, ShortlistSize: 4)).Should().Equal(0, 1, 2, 3);
        Shortlist(survivors, proxies, new(2, 3, ShortlistSize: 3)).Should().Equal(0, 1, 3);
    }

    [Fact]
    public void Shortlist_TiesBreakByPeakThenBySortedIdTuple()
    {
        int[][] survivors = [[0, 2], [0, 1], [1, 2]];
        ProxyOutcome[] proxies = [P(.5m), P(.5m), P(.5m, peak: 0)];

        Shortlist(survivors, proxies, new(2, 2, ShortlistSize: 3)).Should().Equal(2, 1, 0);
    }

    [Fact]
    public void Shortlist_NeverPicksARemovedCandidate()
    {
        ProxyOutcome[] proxies = [P(0m, removal: FtmoGroupRefusal.NoCommonWindow), P(.5m), P(.6m), P(.2m), P(.3m), P(.4m)];

        Shortlist(Survivors, proxies, new(2, 3, ShortlistSize: 6)).Should().NotContain(0).And.HaveCount(5);
    }

    private static Strat[] EightMembers() =>
    [
        .. Enumerable.Range(1, 8).Select(n => Distinct(
            n, $"S{n}", Rows((1, 2 + (n % 3), n % 2 == 0 ? -10m * n : 5m * n), (1, 12 + n, n % 3 == 0 ? -7m * n : 3m * n)))),
    ];

    [Fact]
    public void ProxyAll_RunningInParallel_GivesTheSameOutputAsSequential_ByCandidateIndex()
    {
        var cache = Cache(EightMembers());
        var ids = cache.Members.Select(m => m.StrategyId).ToList();
        var survivors = Prune(cache, ids, _ => Resolution(), new(2, 4, MaxPerInstrument: 1), 10_000m, 50m).Survivors;

        var sequential = ProxyAll(cache, ids, survivors, Params(), maxDegreeOfParallelism: 1);
        var parallel = ProxyAll(cache, ids, survivors, Params(), maxDegreeOfParallelism: 4);

        survivors.Should().HaveCount(154);
        parallel.Should().Equal(sequential);
        sequential.Select(p => p.DailyUsed).Distinct().Should().HaveCountGreaterThan(3, "the fixture must not be vacuous");
    }

    [Fact]
    public void Plan_TheSameInputTwice_GivesAnIdenticalShortlist()
    {
        var cache = Cache(EightMembers());
        var options = new SearchOptions(2, 4, ShortlistSize: 12);

        var first = Plan(cache, _ => Resolution(), options, Params(), 50m);
        var second = Plan(cache, _ => Resolution(), options, Params(), 50m);

        first.Shortlist.Should().HaveCount(12);
        second.Shortlist.Select(s => (string.Join(",", s.MemberIds), s.DailyUsed, s.Peak))
            .Should().Equal(first.Shortlist.Select(s => (string.Join(",", s.MemberIds), s.DailyUsed, s.Peak)));
        second.Funnel.Should().Be(first.Funnel);
    }
}
