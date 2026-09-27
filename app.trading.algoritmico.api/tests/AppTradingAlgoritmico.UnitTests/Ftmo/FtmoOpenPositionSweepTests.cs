using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-challenge-race, Phase 2.2 — <see cref="FtmoOpenPositionSweep"/>. An O(n log n) sweep over
/// scalable trades only (design.md Decision 4): <c>openAt(T) = #{Open &lt; T} − #{Close ≤ T ∧ Open &lt; T}</c>.
/// </summary>
public class FtmoOpenPositionSweepTests
{
    private static DateTime At(int day, int hour, int minute) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net = 10m, ResizeOutcome outcome = ResizeOutcome.OnTarget) =>
        new(rowIndex, open, close, net, outcome, FtmoLots: 1m);

    [Fact]
    public void OpenAt_NoOverlappingScalablePosition_ReturnsFalse()
    {
        var cutoff = At(10, 9, 0);
        var trades = new[]
        {
            Trade(0, At(9, 8, 0), At(9, 10, 0)), // closed well before cutoff
        };

        FtmoOpenPositionSweep.OpenAt(trades, cutoff).Should().BeFalse();
    }

    [Fact]
    public void OpenAt_AScalableTradeOpenBeforeTAndClosingAfterT_ReturnsTrue()
    {
        var cutoff = At(10, 9, 0);
        var trades = new[]
        {
            Trade(0, At(10, 8, 0), At(10, 10, 0)), // Open < T < Close
        };

        FtmoOpenPositionSweep.OpenAt(trades, cutoff).Should().BeTrue();
    }

    [Fact]
    public void OpenAt_AnUnscalableTradeOpenAcrossT_IsExcluded()
    {
        var cutoff = At(10, 9, 0);
        var trades = new[]
        {
            Trade(0, At(10, 8, 0), At(10, 10, 0), net: null, outcome: ResizeOutcome.Unscalable),
        };

        FtmoOpenPositionSweep.OpenAt(trades, cutoff).Should().BeFalse();
    }

    [Fact]
    public void OpenAt_ATradeClosingExactlyAtT_IsNotOpenAtT()
    {
        var cutoff = At(10, 9, 0);
        var trades = new[]
        {
            Trade(0, At(10, 8, 0), cutoff), // Close == T
        };

        FtmoOpenPositionSweep.OpenAt(trades, cutoff).Should().BeFalse();
    }

    /// <summary>
    /// Falsification (hard rule 5): a brute-force oracle over scalable rows, agreeing with the sweep
    /// on randomized trade sets and cutoffs. Temporarily breaking the sweep must turn this RED.
    /// </summary>
    [Fact]
    public void OpenAt_AgreesWithABruteForceOracle_OverRandomizedTradeSetsAndCutoffs()
    {
        var random = new Random(20260927);

        for (var iteration = 0; iteration < 200; iteration++)
        {
            var tradeCount = random.Next(1, 12);
            var trades = new List<ProjectedTrade>();
            var baseDay = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

            for (var i = 0; i < tradeCount; i++)
            {
                var openOffset = random.Next(0, 200);
                var duration = random.Next(0, 50);
                var open = baseDay.AddHours(openOffset);
                var close = open.AddHours(duration);
                var unscalable = random.Next(0, 5) == 0;
                trades.Add(new ProjectedTrade(
                    i, open, close, unscalable ? null : 10m,
                    unscalable ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, FtmoLots: 1m));
            }

            var cutoff = baseDay.AddHours(random.Next(0, 250));

            var oracle = BruteForceOracle(trades, cutoff);

            // Fed in natural (insertion) order AND in a fully shuffled order (the method has no
            // ordering precondition — see its doc comment): both must agree with the oracle.
            var sweptNaturalOrder = FtmoOpenPositionSweep.OpenAt(trades, cutoff);
            sweptNaturalOrder.Should().Be(oracle, $"iteration {iteration}, natural order, cutoff {cutoff}");

            var shuffled = trades.OrderBy(_ => random.Next()).ToList();
            var sweptShuffledOrder = FtmoOpenPositionSweep.OpenAt(shuffled, cutoff);
            sweptShuffledOrder.Should().Be(oracle, $"iteration {iteration}, shuffled order, cutoff {cutoff}");
        }
    }

    /// <summary>Matches the shipped <c>HasConcurrentOpenPosition</c>'s semantics, minus its Unscalable inclusion.</summary>
    private static bool BruteForceOracle(IReadOnlyList<ProjectedTrade> trades, DateTime cutoff)
    {
        foreach (var trade in trades)
        {
            if (trade.Net is null)
                continue; // Unscalable — excluded from the sweep too.

            if (trade.OpenSource < cutoff && cutoff < trade.CloseSource)
                return true;
        }

        return false;
    }
}
