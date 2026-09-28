using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR2, task 2.3 (design.md Decision 7) — <see cref="FtmoOrderStatistics.Compute"/>:
/// nearest rank, <c>r = ceil(p*n)</c> (1-based) on sorted ints; <c>n = 0</c> reports every quantile as
/// null (spec.md "Aggregates Are Counts, Shares, And Order Statistics").
/// </summary>
public class FtmoOrderStatisticsTests
{
    [Fact]
    public void NObservations_Zero_EveryQuantileIsNull()
    {
        var result = FtmoOrderStatistics.Compute(Array.Empty<int>());

        result.N.Should().Be(0);
        result.Min.Should().BeNull();
        result.Q1.Should().BeNull();
        result.Median.Should().BeNull();
        result.Q3.Should().BeNull();
        result.Max.Should().BeNull();
    }

    [Fact]
    public void NObservations_One_EveryQuantileEqualsThatValue()
    {
        var result = FtmoOrderStatistics.Compute(new[] { 42 });

        result.N.Should().Be(1);
        result.Min.Should().Be(42);
        result.Q1.Should().Be(42);
        result.Median.Should().Be(42);
        result.Q3.Should().Be(42);
        result.Max.Should().Be(42);
    }

    [Fact]
    public void NObservations_Four_NearestRankMatchesHandComputedValues()
    {
        // Sorted: 10, 20, 30, 40. r = ceil(p*4): Min r=1->10, Q1 r=1->10, Median r=2->20, Q3 r=3->30, Max r=4->40.
        var result = FtmoOrderStatistics.Compute(new[] { 10, 20, 30, 40 });

        result.N.Should().Be(4);
        result.Min.Should().Be(10);
        result.Q1.Should().Be(10);
        result.Median.Should().Be(20);
        result.Q3.Should().Be(30);
        result.Max.Should().Be(40);
    }

    [Fact]
    public void NObservations_Five_NearestRankMatchesHandComputedValues()
    {
        // Sorted: 10, 20, 30, 40, 50. r = ceil(p*5): Min r=1->10, Q1 r=ceil(1.25)=2->20,
        // Median r=ceil(2.5)=3->30, Q3 r=ceil(3.75)=4->40, Max r=5->50.
        var result = FtmoOrderStatistics.Compute(new[] { 10, 20, 30, 40, 50 });

        result.N.Should().Be(5);
        result.Min.Should().Be(10);
        result.Q1.Should().Be(20);
        result.Median.Should().Be(30);
        result.Q3.Should().Be(40);
        result.Max.Should().Be(50);
    }
}
