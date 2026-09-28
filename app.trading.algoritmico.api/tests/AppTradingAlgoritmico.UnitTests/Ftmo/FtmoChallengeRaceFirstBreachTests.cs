using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.2 (design.md Decision 2) — a direct 4-branch pin of
/// <see cref="FtmoChallengeRace.FirstBreach"/>, extracted VERBATIM from the shipped
/// <c>RunPhase</c>'s breach-limit selection block (lines 118–147 of the pre-extraction file). The
/// funded phase (Phase 1.3) is the new caller this pins for.
/// </summary>
public class FtmoChallengeRaceFirstBreachTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Day2 = new(2026, 1, 2, 9, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateOnly FtmoDay1 = new(2026, 1, 1);
    private static readonly DateOnly FtmoDay2 = new(2026, 1, 2);

    private static FtmoBreachEvaluator.BreachPoint Point(DateTime sourceTime, int rowIndex, DateOnly ftmoDay) =>
        new(sourceTime, sourceTime, Balance: 9_000m, Level: 9_500m, MarginPastLevel: 500m, rowIndex, ftmoDay, []);

    [Fact]
    public void DailyOnly_ReportsTheDailyBreach()
    {
        var dailyPoint = Point(Day1, rowIndex: 0, FtmoDay1);
        var evaluation = new FtmoBreachEvaluator.FtmoBreachEvaluation(
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(dailyPoint, dailyPoint),
            FtmoBreachEvaluator.FtmoLimitFinding.Clean());

        var result = FtmoChallengeRace.FirstBreach(evaluation);

        result.Should().NotBeNull();
        result!.Value.BreachPoint.Should().Be(dailyPoint);
        result.Value.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Daily);
    }

    [Fact]
    public void MaxOnly_ReportsTheMaxBreach()
    {
        var maxPoint = Point(Day1, rowIndex: 0, FtmoDay1);
        var evaluation = new FtmoBreachEvaluator.FtmoBreachEvaluation(
            FtmoBreachEvaluator.FtmoLimitFinding.Clean(),
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(maxPoint, maxPoint));

        var result = FtmoChallengeRace.FirstBreach(evaluation);

        result.Should().NotBeNull();
        result!.Value.BreachPoint.Should().Be(maxPoint);
        result.Value.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Max);
    }

    [Fact]
    public void BothAtTheSameClose_ReportsBothSameClose()
    {
        var dailyPoint = Point(Day1, rowIndex: 0, FtmoDay1);
        var maxPoint = Point(Day1, rowIndex: 0, FtmoDay1);
        var evaluation = new FtmoBreachEvaluator.FtmoBreachEvaluation(
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(dailyPoint, dailyPoint),
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(maxPoint, maxPoint));

        var result = FtmoChallengeRace.FirstBreach(evaluation);

        result.Should().NotBeNull();
        result!.Value.BreachPoint.Should().Be(dailyPoint);
        result.Value.BreachLimit.Should().Be(FtmoFirstBreachingLimit.BothSameClose);
    }

    [Fact]
    public void BothAtDifferentCloses_TheEarlierByTimeThenRowIndexWins()
    {
        // Daily is earlier (Day1) than max (Day2) -> daily wins.
        var dailyPoint = Point(Day1, rowIndex: 5, FtmoDay1);
        var maxPoint = Point(Day2, rowIndex: 0, FtmoDay2);
        var evaluation = new FtmoBreachEvaluator.FtmoBreachEvaluation(
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(dailyPoint, dailyPoint),
            FtmoBreachEvaluator.FtmoLimitFinding.BreachedAt(maxPoint, maxPoint));

        var result = FtmoChallengeRace.FirstBreach(evaluation);

        result.Should().NotBeNull();
        result!.Value.BreachPoint.Should().Be(dailyPoint);
        result.Value.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Daily);
    }

    [Fact]
    public void NoBreachOnEitherLimit_ReportsNull()
    {
        var evaluation = new FtmoBreachEvaluator.FtmoBreachEvaluation(
            FtmoBreachEvaluator.FtmoLimitFinding.Clean(),
            FtmoBreachEvaluator.FtmoLimitFinding.Clean());

        var result = FtmoChallengeRace.FirstBreach(evaluation);

        result.Should().BeNull();
    }
}
