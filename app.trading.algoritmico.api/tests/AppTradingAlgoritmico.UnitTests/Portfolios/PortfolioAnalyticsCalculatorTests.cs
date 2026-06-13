using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Tests for the pure portfolio calculator. Members are weighted, merged into one chronological
/// stream, and measured with the shared analytics primitives. No DB / no DI.
/// </summary>
public class PortfolioAnalyticsCalculatorTests
{
    private static StrategyTrade Trade(
        DateTime open,
        DateTime? close,
        decimal profit,
        bool isOpen = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            StrategyId = Guid.Empty,
            Ticket = Random.Shared.NextInt64(1_000_000, long.MaxValue),
            OpenTime = open,
            CloseTime = close,
            Type = "buy",
            Size = 0.1m,
            Item = "ndx",
            OpenPrice = 100m,
            ClosePrice = isOpen ? null : 101m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = 0m,
            Taxes = 0m,
            Swap = 0m,
            Profit = profit,
            IsOpen = isOpen,
        };

    private static PortfolioMemberInput Member(string name, decimal weight, params StrategyTrade[] trades) =>
        new(Guid.NewGuid(), name, weight, trades);

    private static PortfolioMemberInput MemberOn(string broker, string name, decimal weight, params StrategyTrade[] trades) =>
        new(Guid.NewGuid(), name, weight, trades, broker);

    [Fact]
    public void Compute_NoMembers_ReturnsZerosAndBaselineEquity()
    {
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, []);

        dto.MemberCount.Should().Be(0);
        dto.TradeCount.Should().Be(0);
        dto.NetProfit.Should().Be(0m);
        dto.MaxDrawdownAmount.Should().Be(0m);
        dto.FinalEquity.Should().Be(100_000m, "no trades → equity stays at the baseline");
        dto.Members.Should().BeEmpty();
    }

    [Fact]
    public void Compute_FullWeight_SumsMemberNetsLikeSqx()
    {
        var d = new DateTime(2026, 1, 1);
        // A: +100, B: +200. Weight 1 each = full combination (SQX style) → Net = 300.
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, [
            Member("A", 1m, Trade(d, d.AddHours(1), 100m)),
            Member("B", 1m, Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 200m)),
        ]);

        dto.MemberCount.Should().Be(2);
        dto.TradeCount.Should().Be(2);
        dto.WinCount.Should().Be(2);
        dto.NetProfit.Should().Be(300m, "weight 1 each → full sum, like an SQX portfolio");

        var a = dto.Members.Single(m => m.StrategyName == "A");
        a.NormalizedWeight.Should().BeApproximately(0.5m, 0.0001m, "share of total allocation weight");
        a.NetProfit.Should().Be(100m, "standalone member net");
        a.WeightedNetProfit.Should().Be(100m, "weight 1 → full net contributes");
        a.ContributionPercent.Should().BeApproximately(100m / 300m, 0.0001m);
    }

    [Fact]
    public void Compute_ExplicitWeights_UsedAsRawMultipliers()
    {
        var d = new DateTime(2026, 1, 1);
        // Weights are raw size multipliers (NOT normalized): 3*100 + 1*100 = 400.
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, [
            Member("A", 3m, Trade(d, d.AddHours(1), 100m)),
            Member("B", 1m, Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 100m)),
        ]);

        dto.NetProfit.Should().Be(400m, "3*100 + 1*100 — raw multipliers");
        dto.Members.Single(m => m.StrategyName == "A").NormalizedWeight.Should().BeApproximately(0.75m, 0.0001m, "3/(3+1) share");
        dto.Members.Single(m => m.StrategyName == "B").NormalizedWeight.Should().BeApproximately(0.25m, 0.0001m);
    }

    [Fact]
    public void Compute_ZeroWeight_ExcludesStrategy()
    {
        var d = new DateTime(2026, 1, 1);
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, [
            Member("A", 1m, Trade(d, d.AddHours(1), 100m)),
            Member("B", 0m, Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 999m)),
        ]);

        dto.NetProfit.Should().Be(100m, "weight 0 excludes the strategy from the combination");
    }

    [Fact]
    public void Compute_MergedStream_OffsettingDrawdownsNetOut()
    {
        // The whole point of a portfolio: member drawdowns offset on the combined equity curve.
        // Full weight (1 each, SQX style), baseline 100,000.
        //   day1 A +1000 → equity 101,000 (peak)
        //   day2 B  -600 → equity 100,400 (dd 600)
        //   day3 A  -400 → equity 100,000 (dd 1000 ← max)
        //   day4 B  +800 → equity 100,800
        var d = new DateTime(2026, 1, 1);
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, [
            Member("A", 1m,
                Trade(d, d.AddHours(1), 1000m),
                Trade(d.AddDays(2), d.AddDays(2).AddHours(1), -400m)),
            Member("B", 1m,
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), -600m),
                Trade(d.AddDays(3), d.AddDays(3).AddHours(1), 800m)),
        ]);

        dto.NetProfit.Should().Be(800m, "1000 - 600 - 400 + 800");
        dto.FinalEquity.Should().Be(100_800m);
        dto.MaxDrawdownAmount.Should().Be(1000m, "peak 101,000 → valley 100,000");
        dto.MaxDrawdownPercent.Should().BeApproximately(1000m / 101_000m, 0.0001m);
        dto.TotalReturn.Should().BeApproximately(0.008m, 0.0001m, "800 / 100,000");
    }

    [Fact]
    public void Compute_OpenTrades_ExcludedFromAggregates()
    {
        var d = new DateTime(2026, 1, 1);
        var dto = PortfolioAnalyticsCalculator.Compute(100_000m, [
            Member("A", 1m,
                Trade(d, d.AddHours(1), 100m),
                Trade(d.AddDays(1), close: null, profit: 999m, isOpen: true)),
        ]);

        dto.TradeCount.Should().Be(1, "open trade excluded");
        dto.NetProfit.Should().Be(100m, "single member at full weight, open trade contributes nothing");
    }

    [Fact]
    public void ComputeMonthlyReturns_CompoundsOnMergedWeightedStream()
    {
        // Full weight (1 each), baseline 100k.
        // Jan: A +10,000 + B +10,000 = +20,000 → 20%, equity → 120,000
        // Feb: A +11,000 + B +11,000 = +22,000 → 22,000/120,000 = 18.33%, equity → 142,000
        var months = PortfolioAnalyticsCalculator.ComputeMonthlyReturns(100_000m, [
            Member("A", 1m,
                Trade(new DateTime(2026, 1, 10), new DateTime(2026, 1, 10, 12, 0, 0), 10_000m),
                Trade(new DateTime(2026, 2, 10), new DateTime(2026, 2, 10, 12, 0, 0), 11_000m)),
            Member("B", 1m,
                Trade(new DateTime(2026, 1, 15), new DateTime(2026, 1, 15, 12, 0, 0), 10_000m),
                Trade(new DateTime(2026, 2, 15), new DateTime(2026, 2, 15, 12, 0, 0), 11_000m)),
        ]);

        months.Should().HaveCount(2);
        months[0].EquityStart.Should().Be(100_000m);
        months[0].EquityEnd.Should().Be(120_000m);
        months[0].ReturnPercent.Should().BeApproximately(0.20m, 0.0001m);
        months[0].TradeCount.Should().Be(2);
        months[1].EquityStart.Should().Be(120_000m, "compounds from January");
        months[1].EquityEnd.Should().Be(142_000m);
        months[1].ReturnPercent.Should().BeApproximately(22_000m / 120_000m, 0.0001m);
    }

    [Fact]
    public void ComputeEquityCurve_ForwardWalksWeightedStream_WithDrawdown()
    {
        var d = new DateTime(2026, 1, 1);
        var curve = PortfolioAnalyticsCalculator.ComputeEquityCurve(100_000m, [
            Member("A", 1m,
                Trade(d, d.AddHours(1), 1000m),
                Trade(d.AddDays(2), d.AddDays(2).AddHours(1), -400m)),
            Member("B", 1m,
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), -600m),
                Trade(d.AddDays(3), d.AddDays(3).AddHours(1), 800m)),
        ]);

        curve.Should().HaveCount(4);
        curve[0].Equity.Should().Be(101_000m);
        curve[2].Equity.Should().Be(100_000m);
        curve[2].Drawdown.Should().Be(1000m, "below the 101,000 peak");
        curve[3].Equity.Should().Be(100_800m);
    }

    [Fact]
    public void ComputeVaR_HistoricalPercentiles_FromKnownDailySeries()
    {
        // Single member, full weight, baseline 100k. 5 consecutive daily nets:
        //   [100, -200, 50, -500, 300]  → sorted [-500, -200, 50, 100, 300]
        // VaR95 = -Percentile(5%): rank 0.2 → -500 + 300*0.2 = -440 → loss 440
        // VaR99 = -Percentile(1%): rank 0.04 → -500 + 300*0.04 = -488 → loss 488
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            Member("A", 1m,
                Trade(d.AddDays(0), d.AddDays(0).AddHours(1), 100m),
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), -200m),
                Trade(d.AddDays(2), d.AddDays(2).AddHours(1), 50m),
                Trade(d.AddDays(3), d.AddDays(3).AddHours(1), -500m),
                Trade(d.AddDays(4), d.AddDays(4).AddHours(1), 300m)),
        ]);

        risk.Method.Should().Be("Historical");
        risk.ObservationDays.Should().Be(5);
        risk.Var95.Should().BeApproximately(440m, 0.01m);
        risk.Var99.Should().BeApproximately(488m, 0.01m);
        risk.WorstDay.Should().Be(500m, "largest single-day loss as a positive magnitude");
        risk.BestDay.Should().Be(300m);
        risk.Var95Percent.Should().BeApproximately(0.0044m, 0.0001m);
    }

    [Fact]
    public void ComputeVaR_DecomposesRiskPerService()
    {
        // Two members on different brokers, equal weight (0.5 each).
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("FTMO", "A", 1m,
                Trade(d, d.AddHours(1), 100m),
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), -300m)),
            MemberOn("Darwinex", "B", 1m,
                Trade(d, d.AddHours(1), -100m),
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 200m)),
        ]);

        risk.ByService.Should().HaveCount(2);
        risk.ByService.Select(s => s.Service).Should().BeEquivalentTo(["FTMO", "Darwinex"]);
        risk.ByService.Should().OnlyContain(s => s.StrategyCount == 1);
    }
}
