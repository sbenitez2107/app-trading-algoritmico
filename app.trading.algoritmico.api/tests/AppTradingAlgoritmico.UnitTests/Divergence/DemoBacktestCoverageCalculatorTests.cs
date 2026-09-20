using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Phase 3 — pins the coverage calculator's classification, window derivation, and determinism.</summary>
public class DemoBacktestCoverageCalculatorTests
{
    private static CostObservation Obs(DateTime openTime)
        => new(openTime, 100m, 105m, 0.1m, "buy", 5m, null);

    [Fact]
    public void BothSidesTraded_WhenMonthHasDemoAndBacktestTrades()
    {
        var month = new DateTime(2026, 4, 10);
        var demo = new List<CostObservation> { Obs(month) };
        var backtest = new List<CostObservation> { Obs(month.AddDays(1)) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().ContainSingle(m => m.Year == 2026 && m.Month == 4 && m.PeriodCoverage == PeriodCoverage.BothSidesTraded);
    }

    [Fact]
    public void DemoOnlyNoBacktestTrades_WhenMonthHasDemoTradesAndNoBacktestTrades()
    {
        var demo = new List<CostObservation> { Obs(new DateTime(2026, 4, 10)) };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().ContainSingle(m => m.PeriodCoverage == PeriodCoverage.DemoOnlyNoBacktestTrades);
        result.CoverageBasis.Should().Be(CoverageBasis.PresumedFromBacktestTradeAbsence);
    }

    [Fact]
    public void BacktestOnlyNoDemoTrades_WhenMonthHasBacktestTradesAndNoDemoTrades()
    {
        var demo = new List<CostObservation>();
        var backtest = new List<CostObservation> { Obs(new DateTime(2026, 4, 10)) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().ContainSingle(m => m.PeriodCoverage == PeriodCoverage.BacktestOnlyNoDemoTrades);
    }

    [Fact]
    public void NoTradesEitherSide_WhenMonthHasNeitherSideTrading()
    {
        var demo = new List<CostObservation>
        {
            Obs(new DateTime(2026, 1, 10)),
            Obs(new DateTime(2026, 3, 10)),
        };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().HaveCount(3);
        result.Months.Should().ContainSingle(m => m.Month == 2 && m.PeriodCoverage == PeriodCoverage.NoTradesEitherSide);
    }

    [Fact]
    public void Window_SpansFromEarliestToLatestOpenTimestampAcrossTheUnionOfBothSides()
    {
        var demo = new List<CostObservation> { Obs(new DateTime(2026, 1, 5)) };
        var backtest = new List<CostObservation> { Obs(new DateTime(2026, 4, 5)) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().HaveCount(4);
        result.Months.Select(m => m.Month).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Window_WithExactlyOneTradeOverall_ProducesASingleMonthRow()
    {
        var demo = new List<CostObservation> { Obs(new DateTime(2026, 6, 15)) };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().ContainSingle();
        result.Months[0].Year.Should().Be(2026);
        result.Months[0].Month.Should().Be(6);
    }

    [Fact]
    public void Window_WithOneSideEntirelyEmpty_StillDerivesFromTheNonEmptySide()
    {
        var demo = new List<CostObservation>
        {
            Obs(new DateTime(2026, 1, 1)),
            Obs(new DateTime(2026, 3, 1)),
        };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().HaveCount(3);
        result.Months.Should().OnlyContain(m => m.PeriodCoverage == PeriodCoverage.DemoOnlyNoBacktestTrades || m.PeriodCoverage == PeriodCoverage.NoTradesEitherSide);
        result.Months.Select(m => m.Month).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void EmptyMonthRow_ReportsEmptyTimestampListsNotNull()
    {
        var demo = new List<CostObservation>
        {
            Obs(new DateTime(2026, 1, 1)),
            Obs(new DateTime(2026, 3, 1)),
        };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);
        var gapMonth = result.Months.Single(m => m.Month == 2);

        gapMonth.DemoOpenTimes.Should().NotBeNull().And.BeEmpty();
        gapMonth.BacktestOpenTimes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MonthRows_ExposeOpenTimestampsVerbatimWithDateTimeKindUntouched()
    {
        var demoTime = new DateTime(2026, 4, 10, 9, 30, 0, DateTimeKind.Unspecified);
        var backtestTime = new DateTime(2026, 4, 11, 14, 0, 0, DateTimeKind.Utc);
        var demo = new List<CostObservation> { Obs(demoTime) };
        var backtest = new List<CostObservation> { Obs(backtestTime) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);
        var month = result.Months.Single();

        month.DemoOpenTimes.Should().ContainSingle(t => t == demoTime && t.Kind == DateTimeKind.Unspecified);
        month.BacktestOpenTimes.Should().ContainSingle(t => t == backtestTime && t.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public void MonthRows_AreOrderedAscendingByYearThenMonthRegardlessOfInputOrder()
    {
        var demo = new List<CostObservation>
        {
            Obs(new DateTime(2026, 3, 1)),
            Obs(new DateTime(2026, 1, 1)),
            Obs(new DateTime(2026, 2, 1)),
        };
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Select(m => m.Month).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void CoverageBasis_IsNonNullOnEveryReadoutIncludingNoGapMonths()
    {
        var month = new DateTime(2026, 4, 10);
        var demo = new List<CostObservation> { Obs(month) };
        var backtest = new List<CostObservation> { Obs(month) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.CoverageBasis.Should().Be(CoverageBasis.PresumedFromBacktestTradeAbsence);
    }

    [Fact]
    public void BothSidesEmpty_ReturnsAnEmptyComponentRatherThanAnEmptyWindowRow()
    {
        var demo = new List<CostObservation>();
        var backtest = new List<CostObservation>();

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Should().NotBeNull();
        result.Months.Should().BeEmpty();
    }

    [Fact]
    public void Window_CrossingAYearBoundary_ProducesTheFullMonthSequenceInOrderIncludingTheRollover()
    {
        var demo = new List<CostObservation> { Obs(new DateTime(2025, 11, 5)) };
        var backtest = new List<CostObservation> { Obs(new DateTime(2026, 2, 5)) };

        var result = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        result.Months.Select(m => (m.Year, m.Month)).Should().Equal(
            (2025, 11), (2025, 12), (2026, 1), (2026, 2));
        result.Months.Should().SatisfyRespectively(
            m => m.PeriodCoverage.Should().Be(PeriodCoverage.DemoOnlyNoBacktestTrades),
            m => m.PeriodCoverage.Should().Be(PeriodCoverage.NoTradesEitherSide),
            m => m.PeriodCoverage.Should().Be(PeriodCoverage.NoTradesEitherSide),
            m => m.PeriodCoverage.Should().Be(PeriodCoverage.BacktestOnlyNoDemoTrades));
    }

    [Fact]
    public void RepeatedCalls_AreByteIdentical()
    {
        var demo = new List<CostObservation>
        {
            Obs(new DateTime(2026, 1, 1)),
            Obs(new DateTime(2026, 3, 1)),
        };
        var backtest = new List<CostObservation> { Obs(new DateTime(2026, 2, 1)) };

        var first = DemoBacktestCoverageCalculator.Compute(demo, backtest);
        var second = DemoBacktestCoverageCalculator.Compute(demo, backtest);

        first.Should().BeEquivalentTo(second, options => options.WithStrictOrdering());
    }
}
