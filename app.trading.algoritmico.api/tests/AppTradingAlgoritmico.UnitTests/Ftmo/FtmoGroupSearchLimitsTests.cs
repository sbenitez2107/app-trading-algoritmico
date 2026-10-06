using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d.2.4 (design D5) — the search constants are DERIVED from the benchmark, and this test keeps them
/// so: a constant edited without a new measurement fails here. The two figures below are the ones
/// <see cref="FtmoGroupSearchBenchmarkTests"/> measured (Release, 12 logical cores; see its doc comment for the table),
/// rounded UP. This always runs; it never touches the machine's speed.
/// </summary>
public class FtmoGroupSearchLimitsTests
{
    /// <summary>
    /// Proxy stage at P = 24: the worse of two median-of-3 runs, 56.1 s over 12,926 candidates = 4.34 ms each, rounded up.
    /// The margin is THIN: a single P = 24 sample reached 60.9 s, above the 60 s gate.
    /// </summary>
    private const double MeasuredProxySecondsPerCandidate = 0.0044;

    /// <summary>The slowest per-candidate full computation (both kinds) of k = 2, 3, 4 over two runs: median 3.48 s (k = 4), rounded up.</summary>
    private const double MeasuredSecondsPerGroup = 3.5;

    private const double ProxyGateSeconds = 60;

    private static long Choose(int n, int k)
    {
        long result = 1;
        for (var i = 1; i <= k; i++)
            result = result * (n - k + i) / i;
        return result;
    }

    private static long Candidates(int pool) => Choose(pool, 2) + Choose(pool, 3) + Choose(pool, 4);

    [Fact]
    public void TheSimulationBudgetTimesTheMeasuredGroupTime_FitsTheDefaultWallClock()
    {
        var worstCase = FtmoGroupSearchLimits.DefaultMaxFullSimulations * MeasuredSecondsPerGroup;

        worstCase.Should().BeLessThanOrEqualTo(FtmoGroupSearchLimits.DefaultMaxWallClock.TotalSeconds,
            "a search that simulates its whole budget must be able to finish before its own clock stops it");
    }

    [Fact]
    public void TheConstants_ArePinnedToTheValuesTheBenchmarkJustified()
    {
        FtmoGroupSearchLimits.ShortlistSize.Should().Be(75, "a per-size quota of 25 over sizes 2..4: the calibration's DEPTH d100 = 25");
        FtmoGroupSearchLimits.DefaultMaxFullSimulations.Should().Be(75);
        FtmoGroupSearchLimits.DefaultMaxWallClock.Should().Be(TimeSpan.FromMinutes(30));
        FtmoGroupSearchLimits.MaxFullSimulationsCeiling.Should().Be(500);
        FtmoGroupSearchLimits.MaxWallClockSecondsCeiling.Should().Be(3600);
        FtmoGroupSearchLimits.MaxPoolSize.Should().Be(24);
    }

    [Fact]
    public void MaxPoolSize_IsTheLargestPoolWhoseProxyPassFitsTheGate()
    {
        Candidates(24).Should().Be(12_926);

        (Candidates(FtmoGroupSearchLimits.MaxPoolSize) * MeasuredProxySecondsPerCandidate).Should().BeLessThanOrEqualTo(ProxyGateSeconds);
        (Candidates(FtmoGroupSearchLimits.MaxPoolSize + 1) * MeasuredProxySecondsPerCandidate).Should().BeGreaterThan(
            ProxyGateSeconds, "one more strategy would break the gate, so the constant is not just conservative");
    }
}
