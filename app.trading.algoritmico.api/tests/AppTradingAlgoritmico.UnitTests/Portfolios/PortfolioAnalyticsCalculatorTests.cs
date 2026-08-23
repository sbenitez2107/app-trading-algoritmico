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

    /// <summary>One closed trade per calendar day starting at <paramref name="start"/>, each netting
    /// <paramref name="netPerDay"/> — builds a DENSE daily series with no zero-fill gaps.</summary>
    private static StrategyTrade[] DailyTrades(DateTime start, int days, decimal netPerDay) =>
        Enumerable.Range(0, days)
            .Select(i => Trade(start.AddDays(i), start.AddDays(i).AddHours(1), netPerDay))
            .ToArray();

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

    // -------------------------------------------------------------------------
    // Monthly VaR (30-calendar-day rolling window) — guardrail-agnostic, per service.
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeVaR_MonthlyVar_BelowMinHistory_ReturnsInsufficientHistory()
    {
        // 60 calendar days of dense history — below the 90-day minimum (user decision #2).
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "A", 1m, DailyTrades(d, 60, -10m)),
        ]);

        var svc = risk.ByService.Single();
        svc.MonthlyVarInsufficientHistory.Should().BeTrue();
        svc.MonthlyVar95.Should().BeNull();
        svc.MonthlyVar95Percent.Should().BeNull();
        svc.MonthlyVarOverlappingWindows.Should().Be(0);
        svc.MonthlyVarIndependentWindows.Should().Be(0);
    }

    [Fact]
    public void ComputeVaR_MonthlyVar_ConstantDailyLoss_ComputesExpectedP05AndWindowCounts()
    {
        // 100 calendar days, every day nets -10 → every 30-day window sums to exactly -300,
        // so the 5th percentile of window sums is -300 regardless of interpolation rank.
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "A", 1m, DailyTrades(d, 100, -10m)),
        ]);

        var svc = risk.ByService.Single();
        svc.MonthlyVarInsufficientHistory.Should().BeFalse();
        svc.MonthlyVarObservationDays.Should().Be(100);
        svc.MonthlyVarOverlappingWindows.Should().Be(71, "n-H+1 = 100-30+1");
        svc.MonthlyVarIndependentWindows.Should().Be(3, "n/H = 100/30 (integer)");
        svc.MonthlyVar95.Should().Be(300m, "every window sums to -300, so -p05 = 300");
        svc.MonthlyVar95Percent.Should().Be(0.003m, "300 / 100,000 initial capital");
    }

    [Fact]
    public void ComputeVaR_MonthlyVar_DifferentConstantLoss_Triangulates()
    {
        // Different magnitude AND a different day count — proves the estimator isn't hardcoded.
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "A", 1m, DailyTrades(d, 120, -20m)),
        ]);

        var svc = risk.ByService.Single();
        svc.MonthlyVarOverlappingWindows.Should().Be(91, "n-H+1 = 120-30+1");
        svc.MonthlyVarIndependentWindows.Should().Be(4, "n/H = 120/30");
        svc.MonthlyVar95.Should().Be(600m, "every window sums to -600 (30 days * -20)");
        svc.MonthlyVar95Percent.Should().Be(0.006m);
    }

    [Fact]
    public void ComputeVaR_MonthlyVar_ZeroFilledDaysDoNotDistortSums()
    {
        // A single -3000 spike on day 0 and a +1 trade on day 119; every other one of the 120
        // dense calendar days is a zero-filled no-trade day. Only the ONE window starting at day 0
        // sees the spike — every other window (including the 89 that touch neither event) sums to
        // exactly 0, proving zero-fill days contribute nothing and don't leak into neighbouring
        // windows.
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "A", 1m,
                Trade(d, d.AddHours(1), -3000m),
                Trade(d.AddDays(119), d.AddDays(119).AddHours(1), 1m)),
        ]);

        var svc = risk.ByService.Single();
        svc.MonthlyVarInsufficientHistory.Should().BeFalse();
        svc.MonthlyVarObservationDays.Should().Be(120);
        svc.MonthlyVarOverlappingWindows.Should().Be(91);
        svc.MonthlyVarIndependentWindows.Should().Be(4);
        svc.MonthlyVar95.Should().Be(0m, "89 of 91 windows touch neither event and sum to exactly 0");
    }

    [Fact]
    public void ComputeVaR_MonthlyVar_WindowCountsAtN250_MatchesStatedWeakness()
    {
        // The design's stated statistical weakness: at n=250, H=30 there are 221 overlapping but
        // only 8 INDEPENDENT windows — both counts must ship so the UI can show the effective
        // sample size.
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "A", 1m, DailyTrades(d, 250, -5m)),
        ], windowDays: 250);

        var svc = risk.ByService.Single();
        svc.MonthlyVarObservationDays.Should().Be(250);
        svc.MonthlyVarOverlappingWindows.Should().Be(221, "n-H+1 = 250-30+1");
        svc.MonthlyVarIndependentWindows.Should().Be(8, "n/H = 250/30 (integer)");
    }

    [Fact]
    public void ComputeMonthlyReturns_IntraMonthDrawdownResets_WhileUnderwaterCarriesThePriorPeak()
    {
        // Baseline 100k. March loses 8,000; April and May only make money back.
        //   Mar: 100,000 → 92,000   (the only month that actually hurt)
        //   Apr:  92,000 → 92,460
        //   May:  92,460 → 93,400
        // MaxDrawdownPercent resets its peak each month, so April/May report 0.
        // UnderwaterPercent carries the 100,000 peak, so the SAME drawdown repeats.
        var months = PortfolioAnalyticsCalculator.ComputeMonthlyReturns(100_000m, [
            Member("A", 1m,
                Trade(new DateTime(2026, 3, 10), new DateTime(2026, 3, 10, 12, 0, 0), -8_000m),
                Trade(new DateTime(2026, 4, 10), new DateTime(2026, 4, 10, 12, 0, 0), 460m),
                Trade(new DateTime(2026, 5, 10), new DateTime(2026, 5, 10, 12, 0, 0), 940m)),
        ]);

        months.Should().HaveCount(3);

        var mar = months[0];
        mar.MaxDrawdownPercent.Should().BeApproximately(0.08m, 0.0001m, "8,000 lost off the 100,000 opening peak");
        mar.UnderwaterPercent.Should().BeApproximately(0.08m, 0.0001m, "same event, same all-time peak");

        var apr = months[1];
        apr.EquityEnd.Should().Be(92_460m);
        apr.MaxDrawdownPercent.Should().Be(0m, "April only went up from its own opening equity");
        apr.UnderwaterPercent.Should().BeApproximately(0.08m, 0.0001m,
            "seeded with the opening state — April STARTED 8% below the 100,000 all-time peak");

        var may = months[2];
        may.MaxDrawdownPercent.Should().Be(0m);
        may.UnderwaterPercent.Should().BeApproximately(0.0754m, 0.0001m, "(100,000 - 92,460) / 100,000");
    }

    [Fact]
    public void ComputeMonthlyReturns_SingleMonthDip_ReportsSameDepthOnBothDrawdownMetrics()
    {
        // One month, no prior history: the month's own peak IS the all-time peak, so A == B.
        //   +1,000 → 101,000 (new peak) | -3,000 → 98,000 (trough) | +500 → 98,500
        //   depth = 3,000 / 101,000
        var months = PortfolioAnalyticsCalculator.ComputeMonthlyReturns(100_000m, [
            Member("A", 1m,
                Trade(new DateTime(2026, 1, 5), new DateTime(2026, 1, 5, 12, 0, 0), 1_000m),
                Trade(new DateTime(2026, 1, 12), new DateTime(2026, 1, 12, 12, 0, 0), -3_000m),
                Trade(new DateTime(2026, 1, 20), new DateTime(2026, 1, 20, 12, 0, 0), 500m)),
        ]);

        var jan = months.Should().ContainSingle().Subject;
        jan.MaxDrawdownPercent.Should().BeApproximately(3_000m / 101_000m, 0.0001m);
        jan.UnderwaterPercent.Should().BeApproximately(3_000m / 101_000m, 0.0001m);
        jan.WinCount.Should().Be(2);
        jan.LossCount.Should().Be(1);
        jan.TradeCount.Should().Be(3);
    }

    [Fact]
    public void ComputeMonthlyReturns_WinLossCounts_AreWeightedNetsAndExcludeBreakeven()
    {
        // Two members at equal weight → each net is halved, but SIGNS are what W/L counts.
        // Jan nets: +100, -50, 0 (breakeven), +200 → 2 wins, 1 loss, 4 trades.
        var months = PortfolioAnalyticsCalculator.ComputeMonthlyReturns(100_000m, [
            Member("A", 1m,
                Trade(new DateTime(2026, 1, 5), new DateTime(2026, 1, 5, 12, 0, 0), 100m),
                Trade(new DateTime(2026, 1, 6), new DateTime(2026, 1, 6, 12, 0, 0), -50m)),
            Member("B", 1m,
                Trade(new DateTime(2026, 1, 7), new DateTime(2026, 1, 7, 12, 0, 0), 0m),
                Trade(new DateTime(2026, 1, 8), new DateTime(2026, 1, 8, 12, 0, 0), 200m)),
        ]);

        var jan = months.Should().ContainSingle().Subject;
        jan.TradeCount.Should().Be(4);
        jan.WinCount.Should().Be(2);
        jan.LossCount.Should().Be(1, "the flat trade counts as neither a win nor a loss");
    }

    [Fact]
    public void ComputeMemberEquityCurves_ContributionsAreWeightedAndCumulative()
    {
        var d = new DateTime(2026, 1, 1);
        // Weights are RAW size multipliers, not shares: 0.5 = half size, 2 = double size.
        var curves = PortfolioAnalyticsCalculator.ComputeMemberEquityCurves([
            Member("A", 0.5m,
                Trade(d, d.AddHours(1), 1_000m),
                Trade(d.AddDays(2), d.AddDays(2).AddHours(1), -400m)),
            Member("B", 2m,
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 600m)),
        ]);

        curves.Should().HaveCount(2);

        var a = curves[0];
        a.RawWeight.Should().Be(0.5m);
        a.Points.Should().HaveCount(2);
        a.Points[0].Contribution.Should().Be(500m, "1,000 at half size");
        a.Points[1].Contribution.Should().Be(300m, "500 - 200, cumulative");
        a.FinalContribution.Should().Be(300m);

        var b = curves[1];
        b.RawWeight.Should().Be(2m);
        b.Points.Should().ContainSingle();
        b.FinalContribution.Should().Be(1_200m, "600 at double size");
    }

    [Fact]
    public void ComputeMemberEquityCurves_SumOfContributions_ReconcilesWithCombinedEquityCurve()
    {
        // The decomposition must be HONEST: every unit of combined profit belongs to exactly one
        // member, so the contributions add up to the combined curve's gain over initial capital.
        var d = new DateTime(2026, 1, 1);
        var members = new[]
        {
            Member("A", 3m,
                Trade(d, d.AddHours(1), 1_000m),
                Trade(d.AddDays(3), d.AddDays(3).AddHours(1), -400m)),
            Member("B", 1m,
                Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 600m),
                Trade(d.AddDays(4), d.AddDays(4).AddHours(1), 250m)),
        };

        var curves = PortfolioAnalyticsCalculator.ComputeMemberEquityCurves(members);
        var combined = PortfolioAnalyticsCalculator.ComputeEquityCurve(100_000m, members);

        var totalContribution = curves.Sum(c => c.FinalContribution);
        var combinedGain = combined[^1].Equity - 100_000m;

        totalContribution.Should().BeApproximately(combinedGain, 0.0001m);
    }

    [Fact]
    public void ComputeMemberEquityCurves_AppliesRawSizeMultiplier_WithoutRenormalizing()
    {
        var d = new DateTime(2026, 1, 1);
        // Weight 3 means TRIPLE size, not 75% of the book — the portfolio never rescales to 1.
        var curves = PortfolioAnalyticsCalculator.ComputeMemberEquityCurves([
            Member("A", 3m, Trade(d, d.AddHours(1), 1_000m)),
            Member("B", 1m, Trade(d.AddDays(1), d.AddDays(1).AddHours(1), 1_000m)),
        ]);

        curves[0].RawWeight.Should().Be(3m);
        curves[0].FinalContribution.Should().Be(3_000m);
        curves[1].RawWeight.Should().Be(1m);
        curves[1].FinalContribution.Should().Be(1_000m);
    }

    [Fact]
    public void ComputeMemberEquityCurves_ExcludesOpenTrades_AndKeepsEmptyMembers()
    {
        var d = new DateTime(2026, 1, 1);
        var curves = PortfolioAnalyticsCalculator.ComputeMemberEquityCurves([
            Member("A", 1m,
                Trade(d, d.AddHours(1), 1_000m),
                Trade(d.AddDays(1), close: null, profit: 5_000m, isOpen: true)),
            Member("Silent", 1m),
        ]);

        curves[0].Points.Should().ContainSingle("the open trade contributes nothing");
        curves[0].FinalContribution.Should().Be(1_000m, "weight 1 = full size");

        // A member with no closed trades still gets a row, so the selector can show it as flat.
        curves[1].Points.Should().BeEmpty();
        curves[1].FinalContribution.Should().Be(0m);
        curves[1].StrategyName.Should().Be("Silent");
    }
}
