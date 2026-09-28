using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1 apply follow-up (option A; hard rule 6) — proves the benchmark-gate rework is
/// safe: <see cref="FtmoChallengeRace.RunChain"/>'s precomputed phase-1 evaluation and precomputed
/// open-day attribution parameters produce results BYTE-IDENTICAL to the unshared path (the exact same
/// <see cref="FtmoBreachEvaluator.Evaluate"/> call the shared path skips), across both benchmark fixture
/// profiles and several starts. Falsified per hard rule 6: passing an evaluation of a DIFFERENT series
/// (not the caller's contract) is shown to actually change the result — proving the parameter is
/// consumed, not silently ignored, and that a wrong caller would be caught by this suite's own shape.
/// <see cref="FtmoBreachEvaluator"/> itself is never touched by this change.
/// </summary>
public class FtmoChallengeRaceSharedEvaluationTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const decimal Capital = 10_000m;
    private const decimal DailyPct = 0.05m;
    private const decimal MaxPct = 0.10m;

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void SharedPhase1EvaluationAndAttribution_ProduceTheIdenticalChain_AcrossSeveralStarts(
        FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);
        var starts = FtmoMultiStartBenchmarkFixture.MonthlyStartOpens(trades).Take(5);

        foreach (var startOpen in starts)
        {
            var suffix = trades
                .Where(t => t.OpenSource >= startOpen)
                .OrderBy(t => t.OpenSource)
                .ThenBy(t => t.RowIndex)
                .ToArray();

            var unshared = FtmoChallengeRace.RunChain(suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

            var phase1Evaluation = FtmoBreachEvaluator.Evaluate(suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct);
            var attribution = FtmoChallengeRace.AttributeOpenDays(suffix, Jerusalem, Berlin);
            var shared = FtmoChallengeRace.RunChain(
                suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct, phase1Evaluation, attribution);

            shared.Should().BeEquivalentTo(unshared,
                because: $"start {startOpen:o} must race identically whether phase 1's evaluation/attribution " +
                    "is shared in or recomputed");
        }
    }

    /// <summary>
    /// A source-zone wall-clock instant, <c>DateTimeKind.Unspecified</c> — the shape
    /// <see cref="FtmoDayClock.Attribute"/> requires (it throws on anything else).
    /// </summary>
    private static DateTime SourceUtc(int y, int m, int d, int h = 0, int mi = 0) =>
        new(y, m, d, h, mi, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade MakeTrade(int rowIndex, DateTime openSource, DateTime closeSource, decimal net) =>
        new(rowIndex, openSource, closeSource, net, Domain.Enums.ResizeOutcome.OnTarget, net);

    [Fact]
    public void APrecomputedEvaluationOfADifferentSeries_ChangesThePhase1Result()
    {
        // No breach, ever: 5 trades a day apart, net +400 each, phase 1 target (+10% of 10,000 = 11,000)
        // reached on day 4's close (4 trading days, meeting the day minimum) with plenty of headroom.
        var correctSeries = new[]
        {
            MakeTrade(1, SourceUtc(2024, 1, 1, 9), SourceUtc(2024, 1, 1, 10), 400m),
            MakeTrade(2, SourceUtc(2024, 1, 2, 9), SourceUtc(2024, 1, 2, 10), 400m),
            MakeTrade(3, SourceUtc(2024, 1, 3, 9), SourceUtc(2024, 1, 3, 10), 400m),
            MakeTrade(4, SourceUtc(2024, 1, 4, 9), SourceUtc(2024, 1, 4, 10), 400m),
            MakeTrade(5, SourceUtc(2024, 1, 8, 9), SourceUtc(2024, 1, 8, 10), 400m),
        };

        var correct = FtmoChallengeRace.RunChain(correctSeries, Jerusalem, Berlin, Capital, DailyPct, MaxPct);
        correct.Phase1.Outcome.Should().Be(
            Domain.Enums.FtmoPhaseOutcome.TargetReachedFirst, because: "the fixture is built to reach phase 1's target cleanly, with no breach");

        // A DIFFERENT series, on its very first close, breaches the max loss limit (-15% of 10,000, past
        // the 10% MaxPct ceiling). Its evaluation is the WRONG one for correctSeries — the contract
        // RunChain's own doc-comment states (design.md/RunChain) is violated on purpose.
        var wrongSeries = new[]
        {
            MakeTrade(1, SourceUtc(2024, 1, 1, 9), SourceUtc(2024, 1, 1, 10), -1_500m),
        };
        var wrongEvaluation = FtmoBreachEvaluator.Evaluate(wrongSeries, Jerusalem, Berlin, Capital, DailyPct, MaxPct);
        wrongEvaluation.Max.FirstBreach.Should().NotBeNull(because: "wrongSeries must actually breach for this falsification to mean anything");

        var attribution = FtmoChallengeRace.AttributeOpenDays(correctSeries, Jerusalem, Berlin);
        var poisoned = FtmoChallengeRace.RunChain(
            correctSeries, Jerusalem, Berlin, Capital, DailyPct, MaxPct, wrongEvaluation, attribution);

        poisoned.Should().NotBeEquivalentTo(correct,
            because: "an evaluation of a different (breaching) series must NOT silently agree with the correct, " +
                "breach-free phase-1 result — this proves precomputedEvaluation is actually consumed, falsifying " +
                "the shared-evaluation contract test above");
        poisoned.Phase1.Outcome.Should().Be(
            Domain.Enums.FtmoPhaseOutcome.BreachedFirst, because: "the poisoned evaluation's breach must now win the phase 1 race");
    }
}
