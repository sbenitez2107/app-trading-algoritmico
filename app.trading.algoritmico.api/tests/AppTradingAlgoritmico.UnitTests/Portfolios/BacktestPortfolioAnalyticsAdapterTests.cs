using System.Reflection;
using System.Text.Json;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.Backtests;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Phase 2B — the typed backtest adapters over the shipped analytics math (design.md D4/D4a/D4b/
/// D4c/D5/D6). Every figure here is either published with the density that supports it, or
/// WITHHELD as null. Never a numeric zero.
/// </summary>
public class BacktestPortfolioAnalyticsAdapterTests
{
    private const decimal Capital = 100_000m;

    // -------------------------------------------------------------------------
    // 2.11 / E1 — the daily gate, evaluated PER confidence level
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeVaR_IstFixture_WithholdsDailyVar95WhileReportingVar99OnTheSameRun()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]);

        risk.Density.DenseDayCount.Should().Be(3860);
        risk.Density.NegativeDayCount.Should().Be(164);
        risk.Density.NonZeroDayCount.Should().Be(318);
        risk.Density.TradeCount.Should().Be(329);
        risk.Density.ExcludedUnscalableCount.Should().Be(0);

        // 164 < floor(0.05 * 3859) + 1 = 193 — the 5th-percentile index cannot reach a loss.
        risk.DailyVar95.Should().BeNull();
        risk.DailyVar95Percent.Should().BeNull();
        risk.DailyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);

        // 164 >= floor(0.01 * 3859) + 1 = 39 — the MORE extreme percentile reads DEEPER into the
        // negative block, so one run legitimately yields two different verdicts.
        risk.DailyVar99Withheld.Should().Be(VarWithholdReason.None);

        // MEASURED, and it is NOT the artifacts' 199.46. Both the design and
        // portfolio-monthly-var/spec.md state the published VaR99 as "sorted[38] = -199.46,
        // negated". sorted[38] IS -199.46 (pinned separately below), but the shipped `Percentile`
        // does NOT read that index verbatim: rank = 0.01 * 3859 = 38.59 is fractional, so it
        // INTERPOLATES between sorted[38] = -199.46 and sorted[39] = -199.43. The published figure
        // is therefore 199.4423, and the artifacts' literal is wrong by 0.0177. The VERDICT the
        // scenario exists for — VaR99 reports while VaR95 is withheld on the same run — is
        // unaffected.
        risk.DailyVar99.Should().Be(199.44229999999999988m, "positive LOSS magnitude, interpolated between sorted[38] and sorted[39]");
        risk.DailyVar99Percent.Should().Be(risk.DailyVar99 / Capital);
    }

    [Fact]
    public void ComputeVaR_OostPopulation_WithholdsDailyVar95()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [OostSeries()]);

        risk.Density.DenseDayCount.Should().Be(3804);
        risk.Density.NegativeDayCount.Should().Be(172);
        risk.Density.NonZeroDayCount.Should().Be(320);

        // 172 < floor(0.05 * 3803) + 1 = 191.
        risk.DailyVar95.Should().BeNull();
        risk.DailyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);
    }

    // -------------------------------------------------------------------------
    // 2.12 — the monthly gate clears on both fixtures, and the FIGURES are asserted
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeVaR_IstFixture_ReportsTheMonthlyVar95Figure()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]);

        risk.Density.NegativeWindowCount.Should().Be(1148);
        risk.MonthlyVarOverlappingWindows.Should().Be(3831, "3,860 dense days - 30 + 1");
        risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.None);
        risk.MonthlyVar95.Should().Be(400.19m, "published as a positive loss magnitude — the percentile is -400.19");
    }

    [Fact]
    public void ComputeVaR_OostPopulation_ReportsTheMonthlyVar95Figure()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [OostSeries()]);

        risk.Density.NegativeWindowCount.Should().Be(1203);
        risk.MonthlyVarOverlappingWindows.Should().Be(3775);
        risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.None);
        risk.MonthlyVar95.Should().Be(378.62m);
    }

    // -------------------------------------------------------------------------
    // 2.13 — the WRONG predicate, and what it would have published
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(RawTradeListFixture.IstFileName, 3860, 318, 192)]
    [InlineData(RawTradeListFixture.OostFileName, 3804, 320, 190)]
    public void ComputeVaR_ANonZeroDayShareGateWouldPublishAFigureMeasuredToBeExactlyZero(
        string fixtureName, int denseDays, int nonZeroDays, int fifthPercentileIndex)
    {
        var sorted = DenseDailyNets(fixtureName).OrderBy(x => x).ToList();
        sorted.Should().HaveCount(denseDays);

        // A "non-zero share >= 5%" gate clears comfortably on BOTH fixtures...
        var nonZeroShare = (decimal)nonZeroDays / denseDays;
        nonZeroShare.Should().BeGreaterThan(0.05m);

        // ...and the figure it would then publish is exactly 0.00, because the 5th-percentile index
        // lands inside the ZERO block: positives sort ABOVE the zeros and can never supply the mass
        // a low percentile needs.
        sorted[fifthPercentileIndex].Should().Be(0.00m);

        var series = fixtureName == RawTradeListFixture.IstFileName ? IstSeries() : OostSeries();
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [series]);
        risk.DailyVar95.Should().BeNull("the gate is a relation against the NEGATIVE count, never against the non-zero share");
    }

    [Fact]
    public void IstFixture_TheVar99ReadIndexHoldsTheMeasuredNegativeValue()
    {
        // Corroborates the published 199.46 independently of the adapter: sorted[38] is the index
        // Percentile(., 0.01) reads at N = 3,860.
        var sorted = DenseDailyNets(RawTradeListFixture.IstFileName).OrderBy(x => x).ToList();
        sorted[38].Should().Be(-199.46m);
        sorted[38].Should().BeNegative("a supported percentile is one whose read index lands on an actual loss");
    }

    // -------------------------------------------------------------------------
    // 2.14 — SYNTHETIC boundary. No fixture can pin this: the daily gate withholds on
    // both and the monthly gate reports on both, so each has one branch no real data reaches.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(5, false)]   // exactly floor(0.05 * 100) = 5 negatives — one short
    [InlineData(6, true)]    // one more — reports
    public void ComputeVaR_DailyGateBoundary_Synthetic(int negativeDays, bool shouldReport)
    {
        // 101 dense days: negatives on days 1..k, one positive on day 101 to fix the span.
        var rows = new List<(int Day, decimal Profit)>();
        for (var d = 1; d <= negativeDays; d++) rows.Add((d, -100m));
        rows.Add((101, 10m));

        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [SyntheticSeries(rows)]);

        risk.Density.DenseDayCount.Should().Be(101);
        risk.Density.NegativeDayCount.Should().Be(negativeDays);

        if (shouldReport)
        {
            risk.DailyVar95Withheld.Should().Be(VarWithholdReason.None);
            risk.DailyVar95.Should().NotBeNull();
        }
        else
        {
            risk.DailyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);
            risk.DailyVar95.Should().BeNull();
        }
    }

    /// <summary>
    /// The MONTHLY-labelled boundary row PR1's verification asked for. The monthly gate shares
    /// <c>SupportedPercentile</c> with the daily one, but "shares a code path" is an inferential
    /// step, and this removes it: the relation is exercised over WINDOW sums, at the window count.
    /// </summary>
    [Theory]
    [InlineData(4, false)]   // exactly floor(0.05 * 90) = 4 negative windows of M = 91 — one short
    [InlineData(5, true)]
    public void ComputeVaR_MonthlyGateBoundary_Synthetic(int negativeWindows, bool shouldReport)
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [TailNegativeSeries(denseDays: 120, negativeWindows)]);

        risk.Density.DenseDayCount.Should().Be(120);
        risk.MonthlyVarOverlappingWindows.Should().Be(91);
        risk.Density.NegativeWindowCount.Should().Be(negativeWindows);

        if (shouldReport)
        {
            risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.None);
            risk.MonthlyVar95.Should().NotBeNull();
        }
        else
        {
            risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);
            risk.MonthlyVar95.Should().BeNull();
        }
    }

    /// <summary>
    /// 2.15 — the injected-defect check, at the only boundary the adapter exposes. The task's
    /// literal form ("zero out all but 191 of IST's negative window SUMS") is not reachable from
    /// outside the calculator: window sums are private and the adapter takes trades, not sums. The
    /// equivalent it CAN state is IST's own window count with its negative mass cut to 191 — one
    /// short of the 192 the relation needs at M = 3,831 — which flips the reported monthly figure
    /// to withheld while the count is still disclosed.
    /// </summary>
    [Theory]
    [InlineData(191, false)]
    [InlineData(192, true)]
    public void ComputeVaR_IstWindowCountWithItsNegativeMassCutToTheThreshold_FlipsTheMonthlyVerdict(
        int negativeWindows, bool shouldReport)
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(
            Capital, [TailNegativeSeries(denseDays: 3860, negativeWindows)]);

        risk.MonthlyVarOverlappingWindows.Should().Be(3831, "the same window count the real IST fixture has");
        risk.Density.NegativeWindowCount.Should().Be(negativeWindows, "the count is reported even when the figure is withheld");
        (risk.MonthlyVar95 is not null).Should().Be(shouldReport);
    }

    // -------------------------------------------------------------------------
    // 2.16 — ONE derivation: the payload's gating counts explain the payload's own verdicts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scoped deliberately to the FOUR gating counts. <c>TradeCount</c> and
    /// <c>ExcludedUnscalableCount</c> are bridge-sourced and <c>Measure</c> never sees them, so
    /// extending this assertion to them would fail for the wrong reason (design.md 0.1).
    /// </summary>
    [Fact]
    public void ComputeVaR_ReportedGatingCounts_AreTheCountsThatProducedTheVerdicts()
    {
        AssertSelfConsistent(PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]));
        AssertSelfConsistent(PortfolioAnalyticsCalculator.ComputeVaR(Capital, [OostSeries()]));
        AssertSelfConsistent(PortfolioAnalyticsCalculator.ComputeVaR(
            Capital, [TailNegativeSeries(denseDays: 120, negativeWindows: 4)]));
        AssertSelfConsistent(PortfolioAnalyticsCalculator.ComputeVaR(
            Capital, [TailNegativeSeries(denseDays: 120, negativeWindows: 5)]));
    }

    [Fact]
    public void ComputeVaR_TradeLevelCounts_ReconcileAgainstTheSeriesTheyDescribe()
    {
        var series = IstSeries();
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [series]);

        (risk.Density.TradeCount - risk.Density.ExcludedUnscalableCount).Should().Be(series.Nets.Count);
    }

    // -------------------------------------------------------------------------
    // 2.17 — no window trim (D4a)
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeVaR_BacktestAdapter_PassesNoWindowTrimAtAll()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]);

        risk.WindowDays.Should().Be(0, "a trailing-250-day window answers 'what is my risk NOW', the wrong question for a fixed sample");
        risk.ObservationDays.Should().Be(3860, "not 250 — the shipped default would discard ~93% of the sample");
        risk.Density.DenseDayCount.Should().Be(risk.ObservationDays);

        // Trimmed to 250 the gate would need only 13 negatives and would see 5 — a materially
        // different, and wrong, evaluation. Stated here so the number cannot drift back silently.
        var trimmed = DenseDailyNets(RawTradeListFixture.IstFileName).TakeLast(250).ToList();
        trimmed.Count(n => n < 0m).Should().Be(5);
    }

    // -------------------------------------------------------------------------
    // 2.18 — correlation aligns on the pairwise INTERSECTION and withholds per cell (D6)
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeCorrelation_DisjointTradingDays_WithholdsTheCellRatherThanReportingZero()
    {
        var a = SyntheticSeries([(1, 10m), (2, -20m), (3, 30m)], "A");
        var b = SyntheticSeries([(10, 5m), (11, -7m), (12, 9m)], "B");

        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([a, b]);

        correlation.Alignment.Should().Be("Intersection");
        correlation.Matrix[0][1].Should().BeNull("zero co-active days is not a coefficient of 0 — it is no coefficient at all");
        correlation.CoActiveDays[0][1].Should().Be(0);
        correlation.WithheldCellCount.Should().Be(1);
        correlation.AverageCorrelation.Should().BeNull("every reportable cell is withheld, and an average of nothing is not 0");
    }

    [Fact]
    public void ComputeCorrelation_FewerThanTwoCoActiveDays_WithholdsTheCell()
    {
        var a = SyntheticSeries([(1, 10m), (2, -20m)], "A");
        var b = SyntheticSeries([(2, 5m), (3, -7m)], "B");

        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([a, b]);

        correlation.CoActiveDays[0][1].Should().Be(1, "Pearson's own domain needs at least two observations");
        correlation.Matrix[0][1].Should().BeNull();
        correlation.WithheldCellCount.Should().Be(1);
    }

    [Fact]
    public void ComputeCorrelation_ConstantSeriesOverTheIntersection_WithholdsTheCell()
    {
        // Shipped Pearson returns 0 for a constant series. Publishing that 0 would read as
        // "uncorrelated", which is a different claim from "undefined".
        var a = SyntheticSeries([(1, 10m), (2, 10m), (3, 10m)], "A");
        var b = SyntheticSeries([(1, 5m), (2, -7m), (3, 9m)], "B");

        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([a, b]);

        correlation.CoActiveDays[0][1].Should().Be(3);
        correlation.Matrix[0][1].Should().BeNull();
        correlation.WithheldCellCount.Should().Be(1);
        correlation.AverageCorrelation.Should().BeNull();
    }

    [Fact]
    public void ComputeCorrelation_ValidIntersection_ReportsTheCellWithItsCoActivity()
    {
        var a = SyntheticSeries([(1, 10m), (2, -20m), (3, 30m), (9, 1m)], "A");
        var b = SyntheticSeries([(1, 20m), (2, -40m), (3, 60m), (8, 1m)], "B");

        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation([a, b]);

        correlation.Matrix[0][1].Should().Be(1.0000m, "the two members move together on every co-active day");
        correlation.CoActiveDays[0][1].Should().Be(3);
        correlation.CoActiveShare[0][1].Should().Be(0.6000m, "3 co-active days of the 5 on which EITHER member closed");
        correlation.WithheldCellCount.Should().Be(0);
        correlation.AverageCorrelation.Should().Be(1.0000m);
        correlation.Matrix[0][0].Should().Be(1m);
        correlation.Labels.Should().Equal("A", "B");
    }

    [Fact]
    public void ComputeCorrelation_IntersectionRemovesCoAbsence_WhereUnionWouldMeasureIt()
    {
        // Two members whose co-active days are perfectly correlated but which are absent on most
        // days. Union alignment would drown the coefficient in shared zeros; intersection does not.
        var a = SyntheticSeries([(1, 10m), (2, -20m), (3, 30m), (400, 5m)], "A");
        var b = SyntheticSeries([(1, 10m), (2, -20m), (3, 30m), (400, 5m)], "B");

        PortfolioAnalyticsCalculator.ComputeCorrelation([a, b]).Matrix[0][1].Should().Be(1.0000m);
    }

    [Fact]
    public void ComputeCorrelation_LivePath_StillAlignsOnTheUnionWhereTheBacktestPathWithholds()
    {
        // ONE pair, both doors. Union days {1,2,3}: x = [10,-10,0], y = [0,-10,10], both means 0,
        // so r = 100 / sqrt(200 * 200) = 0.5 EXACTLY — an arithmetic identity, not a recorded
        // measurement. The pair's intersection is the single day 2, which Pearson's own domain
        // cannot take at all.
        var live = new[]
        {
            LiveMember("A", [(1, 10m), (2, -10m)]),
            LiveMember("B", [(2, -10m), (3, 10m)]),
        };

        PortfolioAnalyticsCalculator.ComputeCorrelation(live).Matrix[0][1]
            .Should().Be(0.5000m, "the live path aligns on the UNION and a non-trading day contributes 0");

        var backtest = PortfolioAnalyticsCalculator.ComputeCorrelation(
            [SyntheticSeries([(1, 10m), (2, -10m)], "A"), SyntheticSeries([(2, -10m), (3, 10m)], "B")]);

        backtest.CoActiveDays[0][1].Should().Be(1);
        backtest.Matrix[0][1].Should().BeNull("the same pair, aligned on the intersection, supports no coefficient");
    }

    // -------------------------------------------------------------------------
    // 2.20 — no public raw-tuple door (D2)
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculator_ExposesNoPublicOverloadOverAnUntypedDatedNetTuple()
    {
        var publicMethods = typeof(PortfolioAnalyticsCalculator)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToList();

        foreach (var parameter in publicMethods.SelectMany(m => m.GetParameters()))
        {
            var type = parameter.ParameterType;
            var isTupleDoor = type.IsGenericType
                && type.GetGenericArguments().Any(a => a.FullName?.Contains("ValueTuple", StringComparison.Ordinal) == true
                    || a.FullName?.Contains("Tuple", StringComparison.Ordinal) == true);

            isTupleDoor.Should().BeFalse(
                $"{parameter.Name} on a public entry point would let a HAND-SCALED projection bind to the analytics primitives");
        }

        var entryPointParameterTypes = publicMethods
            .Where(m => m.Name is "ComputeVaR" or "ComputeCorrelation")
            .SelectMany(m => m.GetParameters())
            .Select(p => p.ParameterType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .Select(t => t.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        entryPointParameterTypes.Should().BeEquivalentTo(
            [typeof(PortfolioMemberInput), typeof(BacktestNetSeries)],
            "exactly two typed doors, and no raw one");
    }

    // -------------------------------------------------------------------------
    // 2.23 / 2.24 — withheld serialises as null, and a currency figure is not a band position
    // -------------------------------------------------------------------------

    [Fact]
    public void BacktestPortfolioRiskDto_EveryWithheldFigure_SerialisesAsJsonNullNeverZero()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]);
        var json = JsonSerializer.Serialize(risk, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        json.Should().Contain("\"dailyVar95\":null");
        json.Should().Contain("\"dailyVar95Percent\":null");
        json.Should().NotContain("\"dailyVar95\":0");
        json.Should().NotContain("\"dailyVar95Percent\":0");
        json.Should().Contain("\"dailyVar99\":199.44229999999999988");
    }

    [Fact]
    public void ComputeVaR_MonthlyVar95Percent_IsOnlyTheShippedCapitalBasisAndNeverABandPosition()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()]);

        risk.MonthlyVar95Percent.Should().Be(risk.MonthlyVar95!.Value / risk.InitialCapital);
        risk.InitialCapital.Should().Be(Capital, "the denominator is reported so the percentage can be read");

        // The KB's target-VaR determination walks up to 6 months of historical VaR until the
        // max/min ratio reaches 2:1, over the last 45 days of OPEN positions. Neither is
        // implemented here, so no member of this payload may claim a band position.
        var members = typeof(BacktestPortfolioRiskDto).GetProperties().Select(p => p.Name).ToList();
        members.Should().NotContain(n => n.Contains("Band", StringComparison.OrdinalIgnoreCase));
        members.Should().NotContain(n => n.Contains("TargetVar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ComputeVaR_NoMembers_WithholdsEveryFigureWithNoSeriesRatherThanPublishingZeros()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, Array.Empty<BacktestNetSeries>());

        risk.DailyVar95.Should().BeNull();
        risk.DailyVar99.Should().BeNull();
        risk.MonthlyVar95.Should().BeNull();
        risk.DailyVar95Withheld.Should().Be(VarWithholdReason.NoSeries);
        risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.NoSeries);
        risk.Density.DenseDayCount.Should().Be(0);
    }

    [Fact]
    public void ComputeVaR_ShortSeries_WithholdsTheMonthlyFigureAsInsufficientHistoryNotAsDensity()
    {
        // The 90-day floor is the SHIPPED gate and is not the density gate. They must stay legible
        // as different reasons.
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(
            Capital, [SyntheticSeries([(1, -10m), (2, 20m), (3, -5m)])]);

        risk.MonthlyVar95.Should().BeNull();
        risk.MonthlyVar95Withheld.Should().Be(VarWithholdReason.InsufficientHistory);
    }

    [Fact]
    public void ComputeVaR_PerFundingService_BreaksDownWithItsOwnDensityAndWithheldStates()
    {
        var risk = PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries(service: "Darwinex")]);

        risk.ByService.Should().HaveCount(1);
        var service = risk.ByService[0];
        service.Service.Should().Be("Darwinex");
        service.StrategyCount.Should().Be(1);
        service.Density.DenseDayCount.Should().Be(3860);
        service.DailyVar95.Should().BeNull();
        service.DailyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);
        service.MonthlyVar95.Should().Be(400.19m);
    }

    [Fact]
    public void ComputeVaR_CarriesTheRunSegmentAsMetadata()
    {
        PortfolioAnalyticsCalculator.ComputeVaR(Capital, [IstSeries()])
            .Segment.Should().Be(BacktestSegment.InSampleTest);
        PortfolioAnalyticsCalculator.ComputeCorrelation([IstSeries()])
            .Segment.Should().Be(BacktestSegment.InSampleTest);
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// The payload's OWN four gating counts, run back through the relation, must predict the
    /// payload's OWN three verdicts. A reported count that differs from the count the gate consumed
    /// would make the payload self-contradictory here.
    /// </summary>
    private static void AssertSelfConsistent(BacktestPortfolioRiskDto risk)
    {
        var n = risk.Density.DenseDayCount;
        var neg = risk.Density.NegativeDayCount;

        Supported(neg, n, 0.05).Should().Be(risk.DailyVar95 is not null);
        Supported(neg, n, 0.01).Should().Be(risk.DailyVar99 is not null);

        if (risk.MonthlyVar95Withheld != VarWithholdReason.InsufficientHistory)
        {
            var m = risk.MonthlyVarOverlappingWindows;
            Supported(risk.Density.NegativeWindowCount, m, 0.05).Should().Be(risk.MonthlyVar95 is not null);
        }

        return;

        static bool Supported(int negativeCount, int count, double p)
            => count > 0 && negativeCount >= (int)Math.Floor(p * (count - 1)) + 1;
    }

    private static BacktestNetSeries IstSeries(string? service = null)
        => FixtureSeries(RawTradeListFixture.IstFileName, LotGrid.ImoxRetester, BacktestSegment.InSampleTest, "IST", service);

    /// <summary>
    /// The OOST file is a mixed-sample NEGATIVE fixture for import (151 IS + 186 OOS1 rows), used
    /// here purely as a dense-day POPULATION for the gate relations. Its segment label is arbitrary
    /// and nothing in these tests asserts it — run selection is PR3's, and it would never hand this
    /// population to the adapter as one run.
    /// </summary>
    private static BacktestNetSeries OostSeries()
    {
        var grid = new LotGrid(sizeDecimals: 1, step: 0.10m, minLot: 0.10m, maxLots: 10m);
        return FixtureSeries(RawTradeListFixture.OostFileName, grid, BacktestSegment.OutOfSample, "OOST", null);
    }

    private static BacktestNetSeries FixtureSeries(
        string fileName, LotGrid grid, BacktestSegment segment, string label, string? service)
    {
        var source = RawTradeListFixture.Load(fileName);
        TradeRiskNormalizer.TryNormalize(source, grid, out var profile).Should().BeTrue();
        var resized = TradeResizer.Resize(profile!, profile!.Estimate.RiskPerTrade!.Value, grid);

        BacktestNetSeries.Bridge.TryBuild(
                source, resized, Guid.NewGuid(), label, service, segment, memberWeight: 1m, out var series)
            .Should().BeTrue();
        return series!;
    }

    /// <summary>The dense first-to-last calendar-day net series of a fixture, computed test-side.</summary>
    private static List<decimal> DenseDailyNets(string fileName)
    {
        var byDay = RawTradeListFixture.Load(fileName)
            .GroupBy(t => t.CloseTime.Date)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Profit));

        var first = byDay.Keys.Min();
        var last = byDay.Keys.Max();
        var days = (int)(last - first).TotalDays + 1;

        return Enumerable.Range(0, days)
            .Select(i => byDay.TryGetValue(first.AddDays(i), out var net) ? net : 0m)
            .ToList();
    }

    /// <summary>
    /// A series whose dense daily nets are exactly the given <c>(day, profit)</c> rows, built THROUGH
    /// the bridge — there is no other way to obtain one, which is the point of the private
    /// constructor.
    /// </summary>
    private static BacktestNetSeries SyntheticSeries(
        IReadOnlyList<(int Day, decimal Profit)> rows, string label = "S", string? service = null)
    {
        var source = rows
            .Select((r, i) => BacktestNetSeriesBridgeTests.Trade(rowIndex: i, profit: r.Profit, size: 1m, day: r.Day))
            .ToList();

        var resized = new ResizedTradeSeries(
            TargetRiskPerTrade: 200m,
            Grid: LotGrid.ImoxRetester,
            Trades: source.Select(t => new ResizedTrade(
                t.RowIndex, t.Ticket, 1m, 1m, TradeRiskInterval.Unknown, ResizeOutcome.OnTarget, RiskBasis.Measured)).ToList(),
            OnTargetCount: source.Count,
            RaisedToMinimumCount: 0,
            CappedAtMaximumCount: 0,
            MaxAchievedRisk: null,
            UnknownAchievedRiskCount: source.Count,
            UnscalableCount: 0);

        BacktestNetSeries.Bridge.TryBuild(
                source, resized, Guid.NewGuid(), label, service, BacktestSegment.InSampleTest, 1m, out var series)
            .Should().BeTrue();
        return series!;
    }

    /// <summary>
    /// <paramref name="denseDays"/> consecutive days, all +1 except the LAST
    /// <paramref name="negativeWindows"/>, which are large losses. A negative day at position
    /// <c>N-j</c> is spanned by exactly <c>j</c> rolling 30-day windows, so k tail losses produce
    /// EXACTLY k negative window sums — the only construction that can place a monthly gate on a
    /// chosen side of its threshold.
    /// </summary>
    private static BacktestNetSeries TailNegativeSeries(int denseDays, int negativeWindows)
    {
        var rows = Enumerable.Range(1, denseDays)
            .Select(d => (Day: d, Profit: d > denseDays - negativeWindows ? -10_000m : 1m))
            .ToList();
        return SyntheticSeries(rows);
    }

    private static PortfolioMemberInput LiveMember(string name, IReadOnlyList<(int Day, decimal Profit)> rows)
        => new(
            Guid.NewGuid(),
            name,
            Weight: 1m,
            Trades: rows.Select(r => new StrategyTrade
            {
                Id = Guid.NewGuid(),
                OpenTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(r.Day - 1),
                CloseTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(r.Day - 1),
                Profit = r.Profit,
                Item = "XAUUSD",
            }).ToList());
}
