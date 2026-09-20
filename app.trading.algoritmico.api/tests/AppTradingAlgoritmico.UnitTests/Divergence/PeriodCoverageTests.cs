using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 1.3 — pins the four-member order the coverage calculator depends on.</summary>
public class PeriodCoverageTests
{
    [Fact]
    public void PeriodCoverage_HasExactlyFourMembersInThisOrder()
    {
        var names = Enum.GetNames<PeriodCoverage>();

        names.Should().HaveCount(4);
        names.Should().ContainInOrder(
            nameof(PeriodCoverage.NoTradesEitherSide),
            nameof(PeriodCoverage.BothSidesTraded),
            nameof(PeriodCoverage.DemoOnlyNoBacktestTrades),
            nameof(PeriodCoverage.BacktestOnlyNoDemoTrades));

        ((int)PeriodCoverage.NoTradesEitherSide).Should().Be(0);
    }
}
