using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Live-path output regression for the `backtest-portfolio-risk-analysis` private-core extraction
/// (design D4b, tasks 1.1 and 1.7).
///
/// This suite BACKFILLS shipped behaviour: it pins what <see cref="PortfolioAnalyticsCalculator"/>
/// already produces so that extracting `CorrelationMatrixCore` behind an `AlignmentMode` and
/// threading a `PercentilePolicy` through `VarFromDaily` / `ComputeMonthlyVar` cannot move a single
/// published figure. It therefore PASSES before the refactor, which is why it cannot be RED-first;
/// its trustworthiness comes from the recorded injected-defect runs (tasks note A), not from having
/// failed once.
///
/// Every expected value below is derived analytically in its own comment — none is a value copied
/// out of a test run.
///
/// The load-bearing assertion is <see cref="ComputeVaR_SparseNegativeSupport_StillReportsEveryShippedFigure"/>:
/// a series that WOULD fail the new negative-observation support test still returns its number,
/// which is what proves the shared percentile helpers were parameterised rather than re-behaved.
/// </summary>
public class PortfolioAnalyticsCalculatorLiveOutputRegressionTests
{
    private static long _ticket = 5_000_000;

    /// <summary>Deterministic closed trade — no <c>Random</c>, so repeated runs are byte-identical.</summary>
    private static StrategyTrade Trade(DateTime day, decimal profit) =>
        new()
        {
            Id = Guid.Empty,
            StrategyId = Guid.Empty,
            Ticket = _ticket++,
            OpenTime = day,
            CloseTime = day.AddHours(1),
            Type = "buy",
            Size = 0.1m,
            Item = "xauusd",
            OpenPrice = 100m,
            ClosePrice = 101m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = 0m,
            Taxes = 0m,
            Swap = 0m,
            Profit = profit,
            IsOpen = false,
        };

    private static PortfolioMemberInput Member(string name, params StrategyTrade[] trades) =>
        new(Guid.Empty, name, 1m, trades);

    private static PortfolioMemberInput MemberOn(string broker, string name, params StrategyTrade[] trades) =>
        new(Guid.Empty, name, 1m, trades, broker);

    // -------------------------------------------------------------------------
    // 1.1 — ComputeCorrelation: union alignment, pinned to analytically derived values.
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeCorrelation_UnionAlignment_PinsCoefficientAndAverage()
    {
        // Day 0 = 2026-01-01. Two members with DIFFERENT trading-day sets, so the union and the
        // pairwise intersection cannot agree — that is what makes this pin sensitive to the
        // AlignmentMode the live adapter passes.
        //
        //   A trades days 0,1,2:  +2, -2, +2
        //   B trades days 1,2,3:  -3, +3, -3
        //
        // Union of trading days = {0,1,2,3} → 4 observations, zero-filled where a member is idle:
        //   A = [ 2, -2,  2,  0]      mean =  0.50
        //   B = [ 0, -3,  3, -3]      mean = -0.75
        //   dA = [1.5, -2.5, 1.5, -0.5]
        //   dB = [0.75, -2.25, 3.75, -2.25]
        //   Sxy = 1.125 + 5.625 + 5.625 + 1.125 = 13.50
        //   Sxx = 2.25 + 6.25 + 2.25 + 0.25     = 11.00
        //   Syy = 0.5625 + 5.0625 + 14.0625 + 5.0625 = 24.75
        //   r = 13.5 / sqrt(11 * 24.75) = 13.5 / 16.5 = 0.818181... → rounded(4) = 0.8182
        //
        // For contrast (NOT what the live path may produce): the pairwise INTERSECTION is days
        // {1,2} only, where A = [-2, 2] and B = [-3, 3] → r = +1.0000 exactly. So flipping the live
        // adapter to Intersection moves this cell from 0.8182 to 1.0000 and the average with it.
        var d = new DateTime(2026, 1, 1);
        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([
            Member("A", Trade(d, 2m), Trade(d.AddDays(1), -2m), Trade(d.AddDays(2), 2m)),
            Member("B", Trade(d.AddDays(1), -3m), Trade(d.AddDays(2), 3m), Trade(d.AddDays(3), -3m)),
        ]);

        correlation.Labels.Should().Equal("A", "B");
        correlation.ObservationDays.Should().Be(4, "the UNION of both members' trading days");
        correlation.Matrix.Should().HaveCount(2);
        correlation.Matrix[0].Should().Equal(1m, 0.8182m);
        correlation.Matrix[1].Should().Equal(0.8182m, 1m);
        correlation.AverageCorrelation.Should().Be(
            0.8182m, "the single off-diagonal pair, union-aligned — NOT the intersection's 1.0000");
    }

    [Fact]
    public void ComputeCorrelation_NoMembers_PinsEmptyMatrix()
    {
        // Explicitly typed rather than `[]`: PR2 added a second typed overload
        // (IReadOnlyList<BacktestNetSeries>), and an EMPTY collection expression is ambiguous
        // between them. The pinned figures below are untouched.
        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation(Array.Empty<PortfolioMemberInput>());

        correlation.Labels.Should().BeEmpty();
        correlation.Matrix.Should().BeEmpty();
        correlation.ObservationDays.Should().Be(0);
        correlation.AverageCorrelation.Should().Be(0m);
    }

    [Fact]
    public void ComputeCorrelation_SingleMember_PinsUnitDiagonalAndZeroAverage()
    {
        // One member ⇒ no off-diagonal pair at all, so the average is 0 by absence of pairs
        // (offDiagCount == 0), not by a computed coefficient.
        var d = new DateTime(2026, 1, 1);
        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([
            Member("Solo", Trade(d, 10m), Trade(d.AddDays(1), -10m)),
        ]);

        correlation.ObservationDays.Should().Be(2);
        correlation.Matrix.Should().ContainSingle();
        correlation.Matrix[0].Should().Equal(1m);
        correlation.AverageCorrelation.Should().Be(0m, "no off-diagonal pairs exist");
    }

    [Fact]
    public void ComputeCorrelation_OpenTradesExcluded_PinsShippedFiltering()
    {
        // An OPEN trade contributes no day and no net, so the union shrinks to the closed days.
        var d = new DateTime(2026, 1, 1);
        var open = Trade(d.AddDays(9), 999m);
        open.IsOpen = true;
        open.ClosePrice = null;

        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([
            Member("A", Trade(d, 2m), Trade(d.AddDays(1), -2m), open),
            Member("B", Trade(d, -2m), Trade(d.AddDays(1), 2m)),
        ]);

        correlation.ObservationDays.Should().Be(2, "the open trade's day never enters the union");
        // The two closed series are exact mirrors, so the coefficient is -1 exactly.
        correlation.Matrix[0].Should().Equal(1m, -1m);
    }

    // -------------------------------------------------------------------------
    // 1.7 — PercentilePolicy regression: the live path is Unconditional, so a series that would
    //       FAIL the negative-observation support test still returns its number.
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeVaR_SparseNegativeSupport_StillReportsEveryShippedFigure()
    {
        // A dense 100-calendar-day series with exactly ONE negative day:
        //   day 0        = -1,000
        //   days 1..99   =    +10
        //
        // Sorted ascending: [-1000, 10 x 99].
        //
        // The support relation the backtest path will apply (spec: withhold when
        // negativeCount < floor(p * (N-1)) + 1) FAILS here for both the daily and the monthly
        // percentile:
        //
        //   daily   N = 100, p = 0.05 → floor(0.05 * 99) + 1 = 4 + 1 = 5   vs 1 negative day    → unsupported
        //   monthly M =  71, p = 0.05 → floor(0.05 * 70) + 1 = 3 + 1 = 4   vs 1 negative window → unsupported
        //
        // The live path passes Unconditional, so EVERY figure below must still be produced,
        // bit-identical to shipped behaviour:
        //
        //   Var95  = -Percentile(sorted, 0.05): rank = 0.05*99 = 4.95 → sorted[4]=sorted[5]=10
        //            ⇒ percentile = 10 ⇒ Var95 = -10  (a NEGATIVE VaR: the 5th percentile day is a gain)
        //   Var99  = -Percentile(sorted, 0.01): rank = 0.01*99 = 0.99 → -1000 + 1010*0.99 = -0.10
        //            ⇒ Var99 = 0.10
        //   Worst  = 1,000 (positive loss magnitude), Best = 10
        //
        //   Monthly: 30-day rolling sums over 100 days ⇒ 71 windows. Only the window starting at
        //   day 0 spans the loss: -1000 + 29*10 = -710. The other 70 windows are 30 * 10 = 300.
        //   Sorted: [-710, 300 x 70]; rank = 0.05*70 = 3.5 → sums[3] = sums[4] = 300
        //            ⇒ monthlyVar95 = -300, percent = -300 / 100,000 = -0.003
        var d = new DateTime(2026, 1, 1);
        var trades = new List<StrategyTrade> { Trade(d, -1_000m) };
        for (var i = 1; i < 100; i++) trades.Add(Trade(d.AddDays(i), 10m));

        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            MemberOn("Darwinex", "Sparse", [.. trades]),
        ]);

        risk.ObservationDays.Should().Be(100, "no trim applies below the shipped 250-day window");
        risk.Var95.Should().Be(
            -10m, "Unconditional never gates: 1 negative day < the 5 the support relation needs");
        risk.Var99.Should().Be(0.10m, "-(-1000 + 1010 * 0.99)");
        risk.WorstDay.Should().Be(1_000m);
        risk.BestDay.Should().Be(10m);

        var service = risk.ByService.Single();
        service.Service.Should().Be("Darwinex");
        service.MonthlyVarInsufficientHistory.Should().BeFalse("100 dense days clears the 90-day floor");
        service.MonthlyVarOverlappingWindows.Should().Be(71, "n - H + 1 = 100 - 30 + 1");
        service.MonthlyVarIndependentWindows.Should().Be(3, "n / H = 100 / 30");
        service.MonthlyVar95.Should().Be(
            -300m, "Unconditional never gates: 1 negative window < the 4 the relation needs");
        service.MonthlyVar95Percent.Should().Be(-0.003m, "-300 / 100,000");
    }

    [Fact]
    public void ComputeVaR_KnownDailySeries_PinsShippedPercentileInterpolation()
    {
        // Five dense daily nets [100, -200, 50, -500, 300] → sorted [-500, -200, 50, 100, 300].
        //   Var95: rank = 0.05*4 = 0.2 → -500 + 300*0.2 = -440 ⇒ 440
        //   Var99: rank = 0.01*4 = 0.04 → -500 + 300*0.04 = -488 ⇒ 488
        // Support relation for p=0.05, N=5: floor(0.05*4) + 1 = 1, and there are 2 negative days,
        // so this series WOULD pass the gate — it is pinned as the interpolating counterpart to the
        // sparse case above.
        var d = new DateTime(2026, 1, 1);
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [
            Member("A",
                Trade(d, 100m),
                Trade(d.AddDays(1), -200m),
                Trade(d.AddDays(2), 50m),
                Trade(d.AddDays(3), -500m),
                Trade(d.AddDays(4), 300m)),
        ]);

        risk.Method.Should().Be("Historical");
        risk.WindowDays.Should().Be(250, "the shipped default trim is unchanged by this slice");
        risk.ObservationDays.Should().Be(5);
        risk.Var95.Should().BeApproximately(440m, 0.0001m);
        risk.Var99.Should().BeApproximately(488m, 0.0001m);
        risk.Var95Percent.Should().BeApproximately(0.0044m, 0.000001m);
        risk.WorstDay.Should().Be(500m);
        risk.BestDay.Should().Be(300m);
    }

    [Fact]
    public void ComputeVaR_EmptySeries_PinsShippedZeros()
    {
        // No trades ⇒ no daily observations. Shipped behaviour returns 0 for every figure, and the
        // live path must keep doing so: Unconditional never converts an absent series into a
        // withheld one.
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(100_000m, [Member("Silent")]);

        risk.ObservationDays.Should().Be(0);
        risk.Var95.Should().Be(0m);
        risk.Var99.Should().Be(0m);
        risk.WorstDay.Should().Be(0m);
        risk.BestDay.Should().Be(0m);
    }
}
