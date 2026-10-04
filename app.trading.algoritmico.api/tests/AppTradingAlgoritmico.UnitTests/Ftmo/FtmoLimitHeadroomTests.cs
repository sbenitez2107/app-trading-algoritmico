using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1c.1 (design D4) — headroom. Capital 10,000; daily allowance 5% (500); max allowance 10% (1,000).
/// Fractions: 0.8 means 80% of the allowance used.
/// </summary>
public class FtmoLimitHeadroomTests
{
    private static Func<DateTime, DateOnly> Day => t => DateOnly.FromDateTime(t);

    private static MergedSeries Merged(IReadOnlyList<ProjectedTrade> low, IReadOnlyList<ProjectedTrade>? high = null) =>
        new(low, high ?? low, [], []);

    private static ProjectedTrade T(int row, DateTime open, DateTime close, decimal? net) => Trade(row, open, close, net);

    private static ProjectedTrade OnDay(int row, int day, decimal? net) => T(row, At(1, day, 9), At(1, day, 10), net);

    private static FtmoChallengePhaseDto Phase(FtmoPhaseOutcome outcome, DateTime? start, DateTime? close, FtmoFirstBreachingLimit? limit = null) =>
        new(outcome, start, null, null, close, limit, null, null, null, null);

    private static FtmoMultiStartRowDto Start(
        FtmoChallengePhaseDto p1, FtmoChallengePhaseDto? p2 = null, FtmoFundedPhaseDto? funded = null) =>
        new(0, p1.StartSourceOpen ?? default, default, default, default, false, null, null, p1,
            p2 ?? Phase(FtmoPhaseOutcome.NotStarted, null, null),
            funded ?? new FtmoFundedPhaseDto(FtmoFundedOutcome.NotStarted, null, null, null, null, null, null, null, null),
            default, false);

    private static FtmoMultiStartRowDto Open(DateTime start, DateTime? close = null) =>
        Start(Phase(close is null ? FtmoPhaseOutcome.NeitherByEndOfData : FtmoPhaseOutcome.TargetReachedFirst, start, close));

    // ---- (a) daily ----

    [Fact]
    public void DailyUsed_DividesTheWorstDayLossByTheDailyAllowance_FromThePreviousMidnightBalance()
    {
        // Day 1 ends at 10,500; day 2 loses 400 from that reference: 400 / 500, not 400 / 10,000.
        var series = new[] { OnDay(0, 5, 500m), OnDay(1, 6, -400m) };

        FtmoLimitHeadroom.DailyUsed(Merged(series), Day, Params()).Should().Be(0.8m);
    }

    [Fact]
    public void DailyUsed_TakesTheWorseFxEnd()
    {
        var low = new[] { OnDay(0, 5, -100m) };
        var high = new[] { OnDay(0, 5, -450m) };

        FtmoLimitHeadroom.DailyUsed(Merged(low, high), Day, Params()).Should().Be(0.9m);
    }

    [Fact]
    public void DailyUsed_IsZero_WhenNothingEverLoses()
    {
        FtmoLimitHeadroom.DailyUsed(Merged([OnDay(0, 5, 100m)]), Day, Params()).Should().Be(0m);
    }

    [Fact]
    public void DailyUsed_OnTheMergedWindow_IsAtLeastTheOnePerPhaseValue_WhenAPhaseBoundarySplitsTheDay()
    {
        // Pin of the DISCLOSED difference: one FTMO day holds a -300 close then a -300 close. A phase that starts
        // between them only sees the second loss, so its own daily figure is smaller than the start-independent one.
        var first = T(0, At(1, 5, 8), At(1, 5, 9), -300m);
        var second = T(1, At(1, 5, 11), At(1, 5, 12), -300m);

        var merged = FtmoLimitHeadroom.DailyUsed(Merged([first, second]), Day, Params());
        var secondPhaseOnly = FtmoDailyLossProfile.Compute([second], Day, 10_000m, 0.05m).WorstDayLoss / 500m;

        merged.Should().Be(1.2m);
        secondPhaseOnly.Should().Be(0.6m);
        merged.Should().BeGreaterThan(secondPhaseOnly);
    }

    // ---- (b) per-start max drawdown ----

    [Fact]
    public void StartMaxUsed_RestartsEachPhaseAtCapital_AppliesTheSubsetRule_AndSkipsNullNets()
    {
        var t0 = T(0, At(1, 5, 9), At(1, 5, 10), -600m);
        var tNull = T(1, At(1, 5, 11), At(1, 5, 12), null);
        var t1 = T(2, At(1, 6, 9), At(1, 6, 10), 700m);
        var straddler = T(3, At(1, 5, 12), At(1, 7, 12), -900m); // opens before the P2 floor, closes after P1: in no phase
        var t2 = OnDay(4, 7, -300m);
        var t3 = OnDay(5, 8, -200m);
        var t4 = OnDay(6, 9, -100m);
        var series = new[] { t0, tNull, t1, straddler, t2, t3, t4 };

        // P1 closes at 6 Jan 10:00 (min 9,400 -> 0.6); P2 closes 8 Jan 10:00 and restarts at capital (min 9,500 ->
        // 0.5, would be 0.4 if it carried the P1 balance); funded restarts again (min 9,900 -> 0.1).
        var start = Start(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, At(1, 1), At(1, 6, 10)),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, At(1, 6, 10), At(1, 8, 10)),
            new FtmoFundedPhaseDto(FtmoFundedOutcome.NotStarted, At(1, 8, 10), null, null, null, null, null, null, null));

        FtmoLimitHeadroom.StartMaxUsed(Merged(series), start, Params()).Should().Be(0.6m);
        FtmoLimitHeadroom.StartMaxUsed(
            Merged(series), start with { Phase1 = Phase(FtmoPhaseOutcome.TargetReachedFirst, At(1, 7), At(1, 6, 10)) }, Params())
            .Should().Be(0.5m, "P1 now holds no rows, so the deepest phase is P2");
    }

    [Fact]
    public void StartMaxUsed_TakesTheWorseFxEnd()
    {
        var low = new[] { OnDay(0, 5, -200m) };
        var high = new[] { OnDay(0, 5, -700m) };

        FtmoLimitHeadroom.StartMaxUsed(Merged(low, high), Open(At(1, 1)), Params()).Should().Be(0.7m);
    }

    [Fact]
    public void MaxUsed_WorstAndMedianAcrossStarts_AreReportedSeparately_AndAnEvenCountAveragesTheMiddlePair()
    {
        var series = new[] { OnDay(0, 5, -200m), OnDay(1, 6, -400m), OnDay(2, 7, -800m) };
        var starts = new[]
        {
            Open(At(1, 1), At(1, 5, 10)),    // only the -200 row -> 0.2
            Open(At(1, 6, 9), At(1, 6, 10)), // only the -400 row -> 0.4
            Open(At(1, 7, 9)),               // only the -800 row -> 0.8
        };

        var odd = FtmoLimitHeadroom.MaxUsed(Merged(series), starts, Params());
        var even = FtmoLimitHeadroom.MaxUsed(Merged(series), [.. starts, Open(At(1, 8, 9))], Params());

        odd.Worst.Should().Be(0.8m);
        odd.Median.Should().Be(0.4m);
        even.Worst.Should().Be(0.8m);
        even.Median.Should().Be(0.3m, "sorted 0, 0.2, 0.4, 0.8: mean of 0.2 and 0.4");
    }

    [Fact]
    public void MaxUsed_NoStarts_IsZero()
    {
        FtmoLimitHeadroom.MaxUsed(Merged([OnDay(0, 5, -200m)]), [], Params()).Should().Be((0m, 0m));
    }

    [Theory]
    [InlineData(FtmoFirstBreachingLimit.Max)]
    [InlineData(FtmoFirstBreachingLimit.BothSameClose)]
    public void StartMaxUsed_APhaseThatBreachedTheMaxLimit_ExceedsOne(FtmoFirstBreachingLimit limit)
    {
        var start = Start(Phase(FtmoPhaseOutcome.BreachedFirst, At(1, 1), At(1, 5, 10), limit));

        FtmoLimitHeadroom.StartMaxUsed(Merged([OnDay(0, 5, -1_100m)]), start, Params()).Should().Be(1.1m);
    }

    [Fact]
    public void StartMaxUsed_APhaseThatDidNotBreachTheMaxLimit_IsAtMostOne()
    {
        var daily = Start(Phase(FtmoPhaseOutcome.BreachedFirst, At(1, 1), At(1, 5, 10), FtmoFirstBreachingLimit.Daily));
        var survived = Start(Phase(FtmoPhaseOutcome.NeitherByEndOfData, At(1, 1), null));

        FtmoLimitHeadroom.StartMaxUsed(Merged([OnDay(0, 5, -600m)]), daily, Params()).Should().BeLessThanOrEqualTo(1m);
        FtmoLimitHeadroom.StartMaxUsed(Merged([OnDay(0, 5, -1_000m)]), survived, Params()).Should().Be(1m, "exactly at the floor is not a breach");
    }

    // ---- combined ----

    [Fact]
    public void Headroom_IsOneMinusTheLargerOfDailyAndWorstMaxUsed_NeverTheMedian()
    {
        var h = new FtmoLimitHeadroom.KindHeadroom(DailyUsed: 0.3m, WorstMaxUsed: 0.6m, MedianMaxUsed: 0.1m);

        h.Headroom.Should().Be(0.4m);
        new FtmoLimitHeadroom.KindHeadroom(0.7m, 0.2m, 0.9m).Headroom.Should().Be(0.3m, "daily is the larger; the median never enters");
    }

    [Fact]
    public void Headroom_ZeroIsPreserved()
    {
        new FtmoLimitHeadroom.KindHeadroom(1m, 0.2m, 0.2m).Headroom.Should().Be(0m);
        FtmoLimitHeadroom.Rank([new(1m, 0m, 0m), new(0.1m, 0.1m, 0.1m)]).Should().Be(0m);
    }

    [Fact]
    public void Rank_IsTheHeadroomOfTheWorseKind()
    {
        FtmoLimitHeadroom.Rank([new(0.2m, 0.3m, 0.3m), new(0.5m, 0.75m, 0.1m)]).Should().Be(0.25m);
    }
}
