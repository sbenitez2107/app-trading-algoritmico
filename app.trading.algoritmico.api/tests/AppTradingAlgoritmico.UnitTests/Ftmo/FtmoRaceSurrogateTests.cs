using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search D3 — the multi-start race surrogate on hand-built series. Capital 10,000, daily allowance 5%
/// (500), max allowance 10% (1,000), targets +1,000 then +500, four trading days per phase. Times are Jerusalem
/// (source) clock; the Berlin day starts at 01:00 Jerusalem.
/// </summary>
public class FtmoRaceSurrogateTests
{
    private static DateOnly DayOf(DateTime t) => FtmoDayClock.Attribute(t, Jerusalem, Berlin).BookkeepingDay;

    /// <summary>One trade that closes at <paramref name="closeHour"/> and opens an hour earlier.</summary>
    private static ProjectedTrade T(int row, int month, int day, int closeHour, decimal? net) =>
        Trade(row, At(month, day, closeHour).AddHours(-1), At(month, day, closeHour), net);

    private static MergedSeries Series(params ProjectedTrade[] rows)
    {
        var numbered = rows.Select((t, i) => t with { RowIndex = i }).ToList();
        return new MergedSeries(numbered, numbered, [], []);
    }

    private static FtmoRaceSurrogate.KindResult Kind(params ProjectedTrade[] rows) =>
        FtmoRaceSurrogate.ComputeKind(Series(rows), DayOf, Params());

    /// <summary>Four +250 days: phase 1 (+1,000, four trading days) is decided on the 8th.</summary>
    private static IEnumerable<ProjectedTrade> PhaseOne(int month = 1, int firstDay = 5) =>
        Enumerable.Range(0, 4).Select(i => T(0, month, firstDay + i, 10, 250m));

    /// <summary>Four +125 days: phase 2 (+500, four trading days) is decided on the 15th.</summary>
    private static IEnumerable<ProjectedTrade> PhaseTwo(int month = 1, int firstDay = 12) =>
        Enumerable.Range(0, 4).Select(i => T(0, month, firstDay + i, 10, 125m));

    // ---- (a) breach before the target ----

    [Fact]
    public void ABreachBeforeTheTarget_Counts_OnTheDailyFloor()
    {
        var kind = Kind([T(0, 1, 5, 10, -600m), .. PhaseOne(firstDay: 6)]);

        kind.StartCount.Should().Be(1);
        kind.Breaches.Should().Be(1);
        kind.BreachShare.Should().Be(1m);
    }

    [Fact]
    public void ABreachBeforeTheTarget_Counts_OnTheStaticMaxFloor()
    {
        // -300 on each of four days: no day passes 500, but the balance ends at 8,800, under the 9,000 max floor.
        var kind = Kind(T(0, 1, 5, 10, -300m), T(1, 1, 6, 10, -300m), T(2, 1, 7, 10, -300m), T(3, 1, 8, 10, -300m));

        kind.Breaches.Should().Be(1);
        kind.WorstUsed.Should().BeGreaterThan(1m, "1,200 of a 1,000 allowance");
    }

    [Fact]
    public void ExactlyAtTheFloor_IsNotABreach()
    {
        var kind = Kind(T(0, 1, 5, 10, -500m));

        kind.Breaches.Should().Be(0, "the floor is crossed only strictly below it");
        kind.WorstUsed.Should().Be(1m, "500 of a 500 allowance is fully used, not breached");
    }

    [Fact]
    public void TheTargetReachedBeforeABreach_IsNotABreach()
    {
        var kind = Kind(T(0, 1, 5, 10, -200m), T(1, 1, 6, 10, 450m), T(2, 1, 7, 10, 450m), T(3, 1, 8, 10, 450m), T(4, 1, 9, 10, 450m));

        kind.Breaches.Should().Be(0);
        kind.MedianDays.Should().BeNull("phase 2 never starts, so there are no days to both targets");
    }

    // ---- (a') what happens after the target ----

    [Fact]
    public void ADrawdownAfterTheTarget_ThatOnlyAFreshPhaseCouldSee_DoesNotBreach()
    {
        // Phase 1 is decided on the 8th at 11,000. The next account restarts at capital, so a 400 dip is fine.
        var kind = Kind([.. PhaseOne(), T(0, 1, 12, 10, -400m)]);

        kind.Breaches.Should().Be(0);
        kind.WorstUsed.Should().Be(0.8m, "400 of the 500 daily allowance on the fresh account");
    }

    [Fact]
    public void AnOpenPositionAtTheTarget_DefersTheDecision_SoThePhaseTwoRowsStayInPhaseOne()
    {
        // At the 8th close the balance is 11,000 but a position opened at 09:30 is still open; it closes -300 on the 9th,
        // under the target. The decision cannot be taken on the 8th, so the +500 of the 12th-15th stays in phase 1.
        var longTrade = Trade(0, At(1, 8, 9).AddMinutes(30), At(1, 9, 10), -300m);
        var kind = Kind([.. PhaseOne(), longTrade, .. PhaseTwo()]);

        kind.Breaches.Should().Be(0);
        kind.MedianDays.Should().BeNull("phase 1 is decided late, so phase 2 has no rows");
    }

    [Fact]
    public void ALossAfterBothTargets_InTheFundedPhase_IsABreach()
    {
        var kind = Kind([.. PhaseOne(), .. PhaseTwo(), T(0, 1, 20, 10, -600m)]);

        kind.Breaches.Should().Be(1, "the funded phase counts, as the engine's funded-breached outcome does");
        kind.MedianDays.Should().BeNull("a breach is not a completed chain");
    }

    [Fact]
    public void BothTargets_GiveTheCalendarDaysFromTheStartToTheSecondTarget()
    {
        var kind = Kind([.. PhaseOne(), .. PhaseTwo()]);

        kind.Breaches.Should().Be(0);
        kind.MedianDays.Should().Be(10, "from the 5th to the 15th");
    }

    [Fact]
    public void TheTargetIsNotDecided_BeforeTheMinimumTradingDays()
    {
        // +1,000 in a single day: no decision on day one, and the later rows stay inside phase 1.
        var kind = Kind(T(0, 1, 5, 10, 1_000m), T(1, 1, 6, 10, -100m));

        kind.MedianDays.Should().BeNull();
        kind.WorstUsed.Should().Be(0.2m, "the 100 loss counts on the same account, not on a fresh one");
    }

    [Fact]
    public void ExactlyAtTheFloor_OnTheWalkedHeadOfAPhase_IsNotABreachEither()
    {
        // A row closed on Feb 2 at 07:00 (opened in January) makes the February start begin mid-day, so its first rows are walked.
        var january = Trade(0, At(1, 5, 9), At(1, 5, 10), 10m);
        var spanning = Trade(1, At(1, 20, 9), At(2, 2, 7), 0m);
        var exactlyAtTheFloor = Trade(2, At(2, 2, 9), At(2, 2, 10), -500m);

        foreach (var kind in new[]
        {
            FtmoRaceSurrogate.ComputeKind(Series(january, spanning, exactlyAtTheFloor), DayOf, Params()),
            FtmoRaceSurrogate.ComputeKindReference(Series(january, spanning, exactlyAtTheFloor), DayOf, Params()),
        })
        {
            kind.StartCount.Should().Be(2);
            kind.Breaches.Should().Be(0);
            kind.WorstUsed.Should().Be(1m);
        }
    }

    [Fact]
    public void ExactlyAtTheMaxFloor_OnTheWalkedHeadOfAPhase_IsNotABreach_AndOneUnitBelowIs()
    {
        // The daily allowance is widened to 50% so only the 1,000 max allowance is in play.
        var p = Params(dailyPct: 0.5m);
        var january = Trade(0, At(1, 5, 9), At(1, 5, 10), 10m);
        var spanning = Trade(1, At(1, 20, 9), At(2, 2, 7), 0m);

        var at = FtmoRaceSurrogate.ComputeKind(Series(january, spanning, Trade(2, At(2, 2, 9), At(2, 2, 10), -1_000m)), DayOf, p);
        var below = FtmoRaceSurrogate.ComputeKind(Series(january, spanning, Trade(2, At(2, 2, 9), At(2, 2, 10), -1_000.01m)), DayOf, p);

        at.Breaches.Should().Be(0);
        below.Breaches.Should().Be(1);
    }

    // ---- the daily floor at the previous Berlin midnight ----

    [Theory]
    [InlineData(0, 30, true)]   // Jerusalem 00:30 on the 6th is 23:30 Berlin on the 5th: the same FTMO day, 600 lost.
    [InlineData(1, 30, false)]  // 01:30 Jerusalem is 00:30 Berlin on the 6th: a new day, the reference resets.
    public void TheDailyFloor_ResetsAtTheBerlinMidnight_NotTheSourceMidnight(int hour, int minute, bool breached)
    {
        var first = Trade(0, At(1, 5, 21), At(1, 5, 22), -300m);
        var second = Trade(1, At(1, 6, hour).AddMinutes(minute - 60), At(1, 6, hour).AddMinutes(minute), -300m);

        Kind(first, second).Breaches.Should().Be(breached ? 1 : 0);
    }

    // ---- starts ----

    [Fact]
    public void TheStarts_AreTheShippedEnumeratorsStarts_IncludingTheBerlinMonthBoundary()
    {
        // Jerusalem 00:30 on Feb 1 is 23:30 Berlin on Jan 31: still January. Mar 1 00:30 Jerusalem is Berlin Feb 28.
        var rows = new[]
        {
            Trade(0, At(1, 5, 9), At(1, 5, 10), 10m),
            Trade(1, At(1, 6, 9), At(1, 6, 10), 10m),
            Trade(2, At(2, 1, 0).AddMinutes(30), At(2, 1, 2), 10m),
            Trade(3, At(3, 1, 0).AddMinutes(30), At(3, 1, 2), 10m),
            Trade(4, At(3, 10, 9), At(3, 10, 10), null),
            Trade(5, At(5, 2, 9), At(5, 2, 10), 10m),
        };

        var expected = FtmoStartEnumerator.Enumerate(rows, Jerusalem, Berlin).Starts.Count;

        expected.Should().Be(3, "January, February (the Mar 1 00:30 open) and May; the Feb 1 00:30 open is January, March and April have none");
        Kind(rows).StartCount.Should().Be(expected);
    }

    [Fact]
    public void ARowOpenedBeforeAStart_NeverLeaksIntoThatStart()
    {
        // The -600 row opens on Jan 5 and closes on Feb 3. The February start (Feb 2 09:00) must not see its close.
        var spanning = Trade(0, At(1, 5, 9), At(2, 3, 10), -600m);
        var february = Trade(1, At(2, 2, 9), At(2, 2, 10), 10m);

        var kind = Kind(spanning, february);

        kind.StartCount.Should().Be(2);
        kind.Breaches.Should().Be(1, "only the January start owns the spanning row");
    }

    [Fact]
    public void AnUnscalableRow_NeverStartsAMonth_AndNeverMovesTheBalance()
    {
        var kind = Kind(T(0, 1, 5, 10, null), T(1, 2, 3, 10, 10m));

        kind.StartCount.Should().Be(1, "the January row is unscalable, so only February starts");
    }

    // ---- aggregation on the worse kind ----

    [Fact]
    public void Aggregate_TakesTheWorseKindOnEveryKey()
    {
        var deploy = new FtmoRaceSurrogate.KindResult(10, 1, 0.4m, 30);
        var eval = new FtmoRaceSurrogate.KindResult(10, 3, 0.7m, 20);

        var outcome = FtmoRaceSurrogate.Aggregate([deploy, eval]);

        outcome.BreachShare.Should().Be(0.3m);
        outcome.Headroom.Should().Be(0.3m, "1 - the larger of 0.4 and 0.7");
        outcome.MedianDays.Should().Be(30);
    }

    [Fact]
    public void Aggregate_AMissingMedianOnEitherKind_IsMissing_AndAZeroIsNot()
    {
        var some = new FtmoRaceSurrogate.KindResult(10, 0, 0m, 0);
        var none = new FtmoRaceSurrogate.KindResult(10, 0, 0m, null);

        FtmoRaceSurrogate.Aggregate([some, none]).MedianDays.Should().BeNull();
        FtmoRaceSurrogate.Aggregate([none, some]).MedianDays.Should().BeNull();
        FtmoRaceSurrogate.Aggregate([some, some]).MedianDays.Should().Be(0);
        FtmoRaceSurrogate.Aggregate([some, some]).Headroom.Should().Be(1m);
    }

    [Fact]
    public void Aggregate_AKindWithNoStarts_HasNoBreachShare()
    {
        FtmoRaceSurrogate.Aggregate([new FtmoRaceSurrogate.KindResult(0, 0, 0m, null)]).BreachShare.Should().Be(0m);
    }

    // ---- the shortlist keys ----

    private static readonly int[][] Combos = [[0, 1], [0, 2], [0, 3], [1, 2], [1, 3], [2, 3]];

    private static ProxyOutcome Race(decimal breach, decimal headroom, int? days, int peak = 1, decimal daily = 0.5m)
        => new(null, daily, peak, new FtmoRaceSurrogate.Outcome(breach, headroom, days));

    private static IReadOnlyList<int> Order(params ProxyOutcome[] proxies)
        => Shortlist(Combos[..proxies.Length], proxies, new SearchOptions(2, 2, ShortlistSize: int.MaxValue));

    [Fact]
    public void Shortlist_OrdersByTheBreachShareFirst_EvenWhenTheHeadroomIsWorse()
    {
        Order(Race(0.2m, 0.9m, 5), Race(0m, 0.1m, 99)).Should().Equal(1, 0);
    }

    [Fact]
    public void Shortlist_OrdersByTheHeadroomHighestFirst_ARealZeroAndANegativeAreValues()
    {
        Order(Race(0m, -0.2m, 5), Race(0m, 0m, 5), Race(0m, 0.1m, 5)).Should().Equal(2, 1, 0);
    }

    [Fact]
    public void Shortlist_OrdersByTheMedianDaysLowestFirst_NoneComesLast_AndZeroIsNotNone()
    {
        Order(Race(0m, 0.5m, null), Race(0m, 0.5m, 40), Race(0m, 0.5m, 0), Race(0m, 0.5m, 7)).Should().Equal(2, 3, 1, 0);
    }

    [Fact]
    public void Shortlist_ThenByPeak_ThenByTheCombo()
    {
        Order(Race(0m, 0.5m, 7, peak: 3), Race(0m, 0.5m, 7, peak: 1), Race(0m, 0.5m, 7, peak: 1)).Should().Equal(1, 2, 0);
    }

    [Fact]
    public void Shortlist_KeepsTheRemovalsOut_AndTheQuotaPerSize()
    {
        ProxyOutcome[] proxies =
        [
            new(FtmoGroupRefusal.NoCommonWindow, 0m, 0, null), Race(0m, 0.9m, 1), Race(0m, 0.8m, 1), Race(0m, 0.7m, 1),
        ];

        Shortlist(Combos[..4], proxies, new SearchOptions(2, 2, ShortlistSize: 2)).Should().Equal(1, 2);
    }

    [Fact]
    public void Shortlist_ACandidateWithoutASurrogate_IsOrderedByItsDailyUsed()
    {
        ProxyOutcome[] proxies = [new(null, 0.6m, 1), new(null, 0.2m, 1)];

        Shortlist(Combos[..2], proxies, new SearchOptions(2, 2, ShortlistSize: 2)).Should().Equal(1, 0);
    }

    [Fact]
    public void Shortlist_ACandidateWithNoSurrogateStarts_SortsAfterEveryCandidateWithStarts_WithinItsSize()
    {
        var noStarts = new ProxyOutcome(null, 0m, 1, FtmoRaceSurrogate.Aggregate([new FtmoRaceSurrogate.KindResult(0, 0, 0m, null)]));

        // Index 0 would otherwise win: breach 0, headroom 1, and the lowest peak.
        Order(noStarts, Race(0.3m, 0.2m, 40, peak: 5), Race(0m, 0.1m, null, peak: 9)).Should().Equal(2, 1, 0);
    }

    // ---- the fast pass equals the row-by-row oracle ----

    /// <summary>A deterministic linear congruential stream: the tests never use a random source.</summary>
    private static Func<int, int> Stream(int seed)
    {
        var state = (uint)(seed * 2_654_435_761u) + 12345u;
        return bound =>
        {
            state = (state * 1_664_525u) + 1_013_904_223u;
            return (int)((state >> 8) % (uint)bound);
        };
    }

    private static MergedSeries RandomSeries(int seed, int rows, bool withUnscalable)
    {
        var next = Stream(seed);
        var drift = ((seed % 5) - 2) * 25;
        var list = new List<ProjectedTrade>();
        for (var i = 0; i < rows; i++)
        {
            // Opens spread over ~500 days with a few same-instant opens; durations from zero to several days.
            var open = At(1, 1, 3).AddHours((i * 47) + next(30));
            var close = open.AddHours(next(9) == 0 ? 0 : 1 + next(next(5) == 0 ? 160 : 20));
            decimal? net = withUnscalable && next(25) == 0 ? null : (next(700) - 300 + drift) * 1m;
            list.Add(new ProjectedTrade(i, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, 1m));
        }

        var ordered = list.OrderBy(t => t.OpenSource).ThenBy(t => t.RowIndex).Select((t, i) => t with { RowIndex = i }).ToList();
        var high = ordered.Select(t => t with { Net = t.Net is null ? null : t.Net + (t.RowIndex % 3 == 0 ? 7m : -3m) }).ToList();
        return new MergedSeries(ordered, high, [], []);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheFastPass_EqualsTheRowByRowOracle_OnManyDeterministicSeries(bool withUnscalable)
    {
        var breachedSomewhere = false;
        var completedSomewhere = false;
        for (var seed = 1; seed <= 120; seed++)
        {
            var series = RandomSeries(seed, rows: 60 + (seed * 7 % 400), withUnscalable);
            var fast = FtmoRaceSurrogate.ComputeKind(series, DayOf, Params());
            var oracle = FtmoRaceSurrogate.ComputeKindReference(series, DayOf, Params());

            fast.Should().Be(oracle, $"seed {seed}");
            breachedSomewhere |= oracle.Breaches > 0;
            completedSomewhere |= oracle.MedianDays is not null;
        }

        breachedSomewhere.Should().BeTrue("the corpus must exercise breaches");
        completedSomewhere.Should().BeTrue("the corpus must exercise completed chains");
    }

    // ---- the proxy carries the surrogate ----

    [Fact]
    public void ComputeProxy_CarriesTheSurrogateOfTheWorseKind()
    {
        // Deploy loses 300 + 201 = 501 on one day (a daily breach); Evaluation is one higher per member: 499, no breach.
        var a = Distinct(1, "A", Trade(0, At(1, 5, 9), At(1, 5, 10), -300m));
        var b = Distinct(2, "B", Trade(0, At(1, 5, 9), At(1, 5, 10), -201m));
        var cache = Cache(a, b);

        var proxy = FtmoGroupSearchEngine.ComputeProxy(cache, [Id(1), Id(2)], Params());

        proxy.Removal.Should().BeNull();
        proxy.Race.Should().NotBeNull();
        proxy.Race!.Value.BreachShare.Should().Be(1m, "the worse kind breaches its only start");
        proxy.Race.Value.Headroom.Should().BeLessThan(0m, "501 of a 500 allowance");
        proxy.DailyUsed.Should().Be(501m / 500m, "the daily proxy stays available for the DTO and the progress UI");
    }

    // ---- parity with the shipped group computation ----

    private static Strat Member(int n, int hourShift, decimal first, decimal second, decimal dip, decimal small)
    {
        var rows = new List<ProjectedTrade>();
        for (var d = 5; d <= 8; d++)
            rows.Add(Trade(rows.Count, At(1, d, 9 + hourShift), At(1, d, 10 + hourShift), first));

        for (var d = 12; d <= 15; d++)
            rows.Add(Trade(rows.Count, At(1, d, 9 + hourShift), At(1, d, 10 + hourShift), second));

        rows.Add(Trade(rows.Count, At(2, 2, 9 + hourShift), At(2, 2, 10 + hourShift), dip));
        for (var d = 3; d <= 6; d++)
            rows.Add(Trade(rows.Count, At(3, d, 9 + hourShift), At(3, d, 10 + hourShift), small));

        return Distinct(n, $"SYM{n}", [.. rows]);
    }

    [Fact]
    public void Parity_TheBreachCount_TheStartCountAndTheMedian_MatchTheShippedGroupComputation()
    {
        // January chain: both targets, then a -600 funded day on Feb 2 (a breach). The Feb 2 start breaches in phase 1.
        // The March start only drifts up. 3 starts, 2 breaches, one chain complete (a breach is not).
        var cache = Cache(Member(1, 0, 125m, 62.5m, -300m, 50m), Member(2, 2, 125m, 62.5m, -300m, 50m));
        var p = Params();

        foreach (var kind in FtmoGroupMemberResolution.Kinds)
        {
            var members = cache.Rebind([Id(1), Id(2)], kind);
            var group = FtmoGroupComputation.ComputeGroup(kind, members, p, CancellationToken.None);
            group.Run!.Summary.Should().NotBeNull();

            var series = members.Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!)).ToList();
            var merged = Merge(series, Intersect(series)!.Value);
            var surrogate = FtmoRaceSurrogate.ComputeKind(merged, cache.DayOf, p);

            var summary = group.Run.Summary!;
            var engineBreaches = summary.Outcomes
                .Where(o => o.Outcome is FtmoChainOutcome.Phase1Breached or FtmoChainOutcome.Phase2Breached or FtmoChainOutcome.FundedBreached)
                .Sum(o => o.Count);

            surrogate.StartCount.Should().Be(summary.StartCount, $"{kind}: same monthly starts");
            surrogate.Breaches.Should().Be(engineBreaches, $"{kind}: same breach flag per start");
            engineBreaches.Should().Be(2, "the scenario is built to breach on two starts");
            surrogate.MedianDays.Should().Be(summary.DaysToBothTargets.Median, $"{kind}: same days to both targets");
        }
    }
}
