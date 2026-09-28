using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.1.1 — RED-first shape tests for the deterministic synthetic
/// benchmark fixture (design.md Decision 1). These pin the fixture's shape BEFORE the benchmark
/// (task 1.1.2) ever runs against it.
/// </summary>
public class FtmoMultiStartBenchmarkFixtureTests
{
    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void Build_ProducesExactlyOneThousandTrades(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);

        trades.Should().HaveCount(1_000);
    }

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void Build_SpansFromTwoThousandSixteenToTwoThousandTwentyFive(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);

        var ordered = trades.OrderBy(t => t.OpenSource).ToArray();
        ordered.First().OpenSource.Year.Should().Be(2016);
        ordered.Last().OpenSource.Year.Should().Be(2025);
    }

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void Build_HasExactlyFiveZeroDurationTrades(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);

        trades.Count(t => t.OpenSource == t.CloseSource).Should().Be(5);
    }

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void Build_HoldingTimesAreWithinOneToSeventyTwoHours(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);

        foreach (var trade in trades)
        {
            var holdHours = (trade.CloseSource - trade.OpenSource).TotalHours;
            holdHours.Should().BeInRange(0, 72);
        }
    }

    [Theory]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Fast)]
    [InlineData(FtmoMultiStartBenchmarkFixture.Profile.Never)]
    public void Build_NoTradeOverlapsTheNextOne(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);

        var ordered = trades.OrderBy(t => t.OpenSource).ThenBy(t => t.RowIndex).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            ordered[i].OpenSource.Should().BeOnOrAfter(ordered[i - 1].CloseSource);
        }
    }

    /// <summary>
    /// The fixture-shape RED for the "fast" profile's defining property (design.md Decision 1):
    /// from ANY start, ten consecutive trades already cross the +10% phase-1 target.
    /// </summary>
    [Fact]
    public void Build_FastProfile_AnyTenConsecutiveTradesReachThePhaseOneTarget()
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Fast);
        var ordered = trades.OrderBy(t => t.OpenSource).ThenBy(t => t.RowIndex).ToArray();

        for (var start = 0; start <= ordered.Length - 10; start += 97)
        {
            var sum = ordered.Skip(start).Take(10).Sum(t => t.Net!.Value);
            sum.Should().BeGreaterThanOrEqualTo(1_000m, "10% of the benchmark's 10,000 capital");
        }
    }

    /// <summary>
    /// The fixture-shape RED for the "never" profile's defining property (design.md Decision 1): no
    /// start ever reaches the +10% phase-1 target — the whole series' net sum stays at zero.
    /// </summary>
    [Fact]
    public void Build_NeverProfile_TheWholeSeriesNeverReachesThePhaseOneTarget()
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Never);

        trades.Sum(t => t.Net!.Value).Should().Be(0m);
    }

    [Fact]
    public void MonthlyStartOpens_ProducesOneStartPerCalendarMonth()
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Never);

        var starts = FtmoMultiStartBenchmarkFixture.MonthlyStartOpens(trades);

        // 2016-01 through 2025-12 inclusive = 120 months.
        starts.Should().HaveCount(120);
        starts.Should().BeInAscendingOrder();
    }
}
