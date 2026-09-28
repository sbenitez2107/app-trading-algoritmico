using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.1.4 (design.md Decision 1's optimisation clause; hard rule 6) — proves
/// the race-private cached-open-day helper equals the shipped <see cref="FtmoReplayCalendar.ElapsedDays"/>
/// at every close-group cutoff of both benchmark fixture profiles, and that <see cref="FtmoChallengeRace.RunChain"/>
/// is unchanged on every monthly start against an unoptimised reference reimplementation of the
/// pre-cache scanner. <see cref="FtmoBreachEvaluator"/> is never touched by this optimisation.
/// </summary>
public class FtmoChallengeRaceElapsedDaysCacheEquivalenceTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const decimal Capital = 10_000m;
    private const decimal DailyPct = 0.05m;
    private const decimal MaxPct = 0.10m;

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void ElapsedDaysCache_MatchesTheShippedHelper_AtEveryCloseGroupCutoff(
        FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var cache = new FtmoChallengeRace.CachedOpenDays(trades, Jerusalem, Berlin);

        var ordered = trades.OrderBy(t => t.CloseSource).ThenBy(t => t.RowIndex).ToArray();

        // Every distinct close instant in the series is a candidate close-group cutoff, in ascending
        // order — the monotonic path RunPhase itself exercises.
        foreach (var cutoffClose in ordered.Select(t => t.CloseSource).Distinct())
        {
            var cutoffDay = FtmoDayClock.Attribute(cutoffClose, Jerusalem, Berlin).BookkeepingDay;

            var reference = FtmoReplayCalendar.ElapsedDays(anchor, cutoffDay, cutoffClose, trades, Jerusalem, Berlin);
            var cached = cache.CountOpenDays(anchor.FtmoDay, cutoffDay, cutoffClose);

            cached.Should().Be(reference.FtmoTradingDaysElapsed,
                because: $"cutoff {cutoffClose:o} must count the same trading days as the shipped helper");
        }
    }

    /// <summary>
    /// A source-zone wall-clock instant, <c>DateTimeKind.Unspecified</c> — the shape
    /// <see cref="FtmoDayClock.Attribute"/> requires (it throws on anything else).
    /// </summary>
    private static DateTime SourceUtc(int y, int m, int d, int h = 0, int mi = 0) =>
        new(y, m, d, h, mi, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade MakeTrade(int rowIndex, DateTime openSource, DateTime closeSource, decimal? net) =>
        new(
            rowIndex, openSource, closeSource, net,
            net is null ? Domain.Enums.ResizeOutcome.Unscalable : Domain.Enums.ResizeOutcome.OnTarget,
            net ?? 0m);

    /// <summary>
    /// Not itself an xUnit test method: <see cref="ProjectedTrade"/> is <c>internal</c>, and a PUBLIC
    /// test method cannot declare a parameter (or use <c>[MemberData]</c> yielding one) of a
    /// less-accessible type (CS0051) even under <c>InternalsVisibleTo</c> — matches the same
    /// convention already used by DemoBacktestComparabilityCalculatorTests.
    /// </summary>
    private static IEnumerable<(string Label, ProjectedTrade[] Trades)> NonMonotonicEquivalenceCases()
    {
        // 1. Zero-duration trade: OpenSource == CloseSource. Only the second disjunct
        //    (CloseSource <= cutoff) can ever admit it, since OpenSource < cutoff requires cutoff strictly
        //    after OpenSource, which equals CloseSource here.
        yield return (
            "zero-duration trade",
            new[]
            {
                MakeTrade(1, SourceUtc(2024, 1, 10, 9), SourceUtc(2024, 1, 10, 9), net: 10m),
                MakeTrade(2, SourceUtc(2024, 1, 12, 9), SourceUtc(2024, 1, 12, 11), net: 20m),
            });

        // 2. Unscalable trade (Net is null) — must never contribute a trading day regardless of cutoff.
        yield return (
            "unscalable trade excluded",
            new[]
            {
                MakeTrade(1, SourceUtc(2024, 1, 10, 9), SourceUtc(2024, 1, 10, 11), net: null),
                MakeTrade(2, SourceUtc(2024, 1, 12, 9), SourceUtc(2024, 1, 12, 11), net: 20m),
            });

        // 3. Same-instant opens and closes across two trades.
        yield return (
            "same-instant opens and closes",
            new[]
            {
                MakeTrade(1, SourceUtc(2024, 1, 10, 9), SourceUtc(2024, 1, 11, 9), net: 15m),
                MakeTrade(2, SourceUtc(2024, 1, 10, 9), SourceUtc(2024, 1, 11, 9), net: -5m),
            });

        // 4. Cutoffs exactly on the open/close instants themselves.
        yield return (
            "cutoffs exactly on open/close instants",
            new[]
            {
                MakeTrade(1, SourceUtc(2024, 1, 10, 9), SourceUtc(2024, 1, 10, 15), net: 5m),
                MakeTrade(2, SourceUtc(2024, 1, 10, 15), SourceUtc(2024, 1, 11, 9), net: 5m),
            });

        // 5. A trade opening on one FTMO day and closing on another.
        yield return (
            "trade spans two FTMO days",
            new[]
            {
                MakeTrade(1, SourceUtc(2024, 1, 10, 23, 50), SourceUtc(2024, 1, 12, 1, 10), net: 30m),
                MakeTrade(2, SourceUtc(2024, 1, 13, 9), SourceUtc(2024, 1, 13, 11), net: -10m),
            });
    }

    [Fact]
    public void ElapsedDaysCache_MatchesTheOracle_AtEveryNonMonotonicEquivalenceCase()
    {
        foreach (var (label, trades) in NonMonotonicEquivalenceCases())
        {
            var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
            var cache = new FtmoChallengeRace.CachedOpenDays(trades, Jerusalem, Berlin);

            // Ascending pass first (the incremental path RunPhase itself uses)...
            var ascending = trades.Select(t => t.CloseSource).Distinct().OrderBy(c => c).ToArray();
            foreach (var cutoffClose in ascending)
            {
                var cutoffDay = FtmoDayClock.Attribute(cutoffClose, Jerusalem, Berlin).BookkeepingDay;
                var reference = FtmoReplayCalendar.ElapsedDays(anchor, cutoffDay, cutoffClose, trades, Jerusalem, Berlin);
                cache.CountOpenDays(anchor.FtmoDay, cutoffDay, cutoffClose).Should().Be(
                    reference.FtmoTradingDaysElapsed, because: $"case '{label}' at ascending cutoff {cutoffClose:o}");
            }

            // ...then a deliberately non-monotonic cutoff BEFORE the last one already answered, exactly
            // like RunPhase's post-loop breach query can be — must still match the oracle via the
            // fallback path.
            var earlierCutoff = ascending[0];
            var earlierDay = FtmoDayClock.Attribute(earlierCutoff, Jerusalem, Berlin).BookkeepingDay;
            var earlierReference = FtmoReplayCalendar.ElapsedDays(anchor, earlierDay, earlierCutoff, trades, Jerusalem, Berlin);
            cache.CountOpenDays(anchor.FtmoDay, earlierDay, earlierCutoff).Should().Be(
                earlierReference.FtmoTradingDaysElapsed, because: $"case '{label}' at the non-monotonic fallback cutoff");
        }
    }

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void RunChain_IsUnchanged_OnEveryMonthlyStart_AgainstTheUnoptimisedReference(
        FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);
        var starts = FtmoMultiStartBenchmarkFixture.MonthlyStartOpens(trades);

        foreach (var startOpen in starts)
        {
            var suffix = trades
                .Where(t => t.OpenSource >= startOpen)
                .OrderBy(t => t.OpenSource)
                .ThenBy(t => t.RowIndex)
                .ToArray();

            var actual = FtmoChallengeRace.RunChain(suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct);
            var reference = UnoptimisedRunChain(suffix);

            actual.Should().Be(reference, because: $"start {startOpen:o} must race identically before and after the cache");
        }
    }

    /// <summary>
    /// Unoptimised reference: the pre-task-1.1.4 scanner, calling the shipped
    /// <see cref="FtmoReplayCalendar.ElapsedDays"/> directly on every close-group cutoff instead of the
    /// cached helper. A verbatim behavioural copy of <see cref="FtmoChallengeRace.RunPhase"/>/
    /// <see cref="FtmoChallengeRace.RunChain"/> as they existed BEFORE the cache, kept here only to
    /// prove the optimisation changed no output.
    /// </summary>
    private static FtmoChallengeRace.ChainResult UnoptimisedRunChain(IReadOnlyList<ProjectedTrade> trades)
    {
        var phase1 = UnoptimisedRunPhase(trades, FtmoChallengeRules.Phase1TargetPct);

        if (phase1.Outcome != Domain.Enums.FtmoPhaseOutcome.TargetReachedFirst)
            return new FtmoChallengeRace.ChainResult(phase1, FtmoChallengeRace.PhaseResult.NotStarted);

        var phase2Trades = trades
            .Where(t => t.OpenSource >= phase1.OutcomeSourceClose!.Value && t.CloseSource > phase1.OutcomeSourceClose!.Value)
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        if (phase2Trades.Count == 0)
        {
            return new FtmoChallengeRace.ChainResult(
                phase1,
                FtmoChallengeRace.PhaseResult.NotStarted with { Outcome = Domain.Enums.FtmoPhaseOutcome.NeitherByEndOfData });
        }

        var phase2 = UnoptimisedRunPhase(phase2Trades, FtmoChallengeRules.Phase2TargetPct);
        return new FtmoChallengeRace.ChainResult(phase1, phase2);
    }

    private static FtmoChallengeRace.PhaseResult UnoptimisedRunPhase(
        IReadOnlyList<ProjectedTrade> phaseTrades, decimal targetPct)
    {
        if (phaseTrades.Count == 0)
            return FtmoChallengeRace.PhaseResult.NotStarted with { Outcome = Domain.Enums.FtmoPhaseOutcome.NeitherByEndOfData };

        var anchor = FtmoReplayCalendar.Build(phaseTrades, Jerusalem, Berlin);
        var startOpen = anchor.SourceOpen;

        var ordered = phaseTrades.OrderBy(t => t.CloseSource).ThenBy(t => t.RowIndex).ToList();
        var targetLevel = Capital * (1m + targetPct);

        DateTime? firstTouch = null;
        DateOnly? minDaysMetDay = null;
        DateTime? minDaysMetClose = null;
        DateTime? decidedClose = null;
        DateOnly? decidedDay = null;
        int? decidedCalendarDays = null;
        int? decidedTradingDays = null;

        var runningBalance = Capital;
        var index = 0;
        while (index < ordered.Count)
        {
            var groupClose = ordered[index].CloseSource;
            while (index < ordered.Count && ordered[index].CloseSource == groupClose)
            {
                if (ordered[index].Net is { } net)
                    runningBalance += net;
                index++;
            }

            if (firstTouch is null && runningBalance >= targetLevel)
                firstTouch = groupClose;

            var groupDay = FtmoDayClock.Attribute(groupClose, Jerusalem, Berlin).BookkeepingDay;
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, groupDay, groupClose, phaseTrades, Jerusalem, Berlin);

            if (minDaysMetDay is null && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase)
            {
                minDaysMetDay = groupDay;
                minDaysMetClose = groupClose;
            }

            if (decidedClose is null
                && runningBalance >= targetLevel
                && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase
                && !FtmoOpenPositionSweep.OpenAt(phaseTrades, groupClose))
            {
                decidedClose = groupClose;
                decidedDay = groupDay;
                decidedCalendarDays = calendarDays;
                decidedTradingDays = tradingDays;
                break;
            }
        }

        var evaluation = FtmoBreachEvaluator.Evaluate(phaseTrades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        FtmoBreachEvaluator.BreachPoint? breachPoint = null;
        Domain.Enums.FtmoFirstBreachingLimit? breachLimit = null;
        if (evaluation.Daily.FirstBreach is not null || evaluation.Max.FirstBreach is not null)
        {
            var daily = evaluation.Daily.FirstBreach;
            var max = evaluation.Max.FirstBreach;

            if (daily is null)
            {
                breachPoint = max;
                breachLimit = Domain.Enums.FtmoFirstBreachingLimit.Max;
            }
            else if (max is null)
            {
                breachPoint = daily;
                breachLimit = Domain.Enums.FtmoFirstBreachingLimit.Daily;
            }
            else if (daily.Value.RowIndex == max.Value.RowIndex)
            {
                breachPoint = daily;
                breachLimit = Domain.Enums.FtmoFirstBreachingLimit.BothSameClose;
            }
            else
            {
                var dailyIsEarlier = (daily.Value.SourceTime, daily.Value.RowIndex)
                    .CompareTo((max.Value.SourceTime, max.Value.RowIndex)) < 0;
                breachPoint = dailyIsEarlier ? daily : max;
                breachLimit = dailyIsEarlier ? Domain.Enums.FtmoFirstBreachingLimit.Daily : Domain.Enums.FtmoFirstBreachingLimit.Max;
            }
        }

        if (breachPoint is not null && (decidedClose is null || breachPoint.Value.SourceTime <= decidedClose.Value))
        {
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, breachPoint.Value.FtmoDay, breachPoint.Value.SourceTime, phaseTrades, Jerusalem, Berlin);

            var touchAtOrBeforeBreach =
                firstTouch is not null && firstTouch.Value <= breachPoint.Value.SourceTime ? firstTouch : null;
            var minDaysMetDayAtOrBeforeBreach =
                minDaysMetClose is not null && minDaysMetClose.Value <= breachPoint.Value.SourceTime
                    ? minDaysMetDay
                    : null;

            return new FtmoChallengeRace.PhaseResult(
                Domain.Enums.FtmoPhaseOutcome.BreachedFirst, startOpen, touchAtOrBeforeBreach, minDaysMetDayAtOrBeforeBreach,
                breachPoint.Value.SourceTime, breachLimit,
                breachPoint.Value.Causes.Count == 0 ? Domain.Enums.FtmoBreachPointClass.Clean : Domain.Enums.FtmoBreachPointClass.Contingent,
                calendarDays, tradingDays);
        }

        if (decidedClose is not null)
        {
            return new FtmoChallengeRace.PhaseResult(
                Domain.Enums.FtmoPhaseOutcome.TargetReachedFirst, startOpen, firstTouch, minDaysMetDay,
                decidedClose, null, null, decidedCalendarDays, decidedTradingDays);
        }

        var last = ordered[^1];
        var lastDay = FtmoDayClock.Attribute(last.CloseSource, Jerusalem, Berlin).BookkeepingDay;
        var (lastCalendarDays, lastTradingDays) = FtmoReplayCalendar.ElapsedDays(
            anchor, lastDay, last.CloseSource, phaseTrades, Jerusalem, Berlin);

        return new FtmoChallengeRace.PhaseResult(
            Domain.Enums.FtmoPhaseOutcome.NeitherByEndOfData, startOpen, firstTouch, minDaysMetDay,
            null, null, null, lastCalendarDays, lastTradingDays);
    }
}
