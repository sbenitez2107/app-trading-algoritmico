using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Phase 2 — the pure pairing/offset algorithm. <see cref="DemoBacktestComparabilityCalculator"/>
/// is <c>internal</c>; this project sees it via <c>InternalsVisibleTo</c> declared on
/// <c>AppTradingAlgoritmico.Infrastructure.csproj</c> (design.md D6).
/// </summary>
public class DemoBacktestComparabilityCalculatorTests
{
    private static readonly Guid StrategyId = Guid.NewGuid();
    private const BacktestRunKind Kind = BacktestRunKind.Deploy;

    /// <summary>
    /// Reproduces the measured `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` fixture: 24 exact-minute pairs
    /// across 2026-04 (n=5, mean +22.06, range 16.2-25.5), 2026-05 (n=14, mean +22.55, range
    /// 11.4-31.9) and 2026-06 (n=5, mean +40.90, range 38.5-44.3). Per-pair offsets are not
    /// individually published by the measurement — only these aggregates — so the fixture is
    /// constructed to reproduce exactly the reported sum/min/max per month, backtest price held at
    /// an arbitrary constant and demo price offset from it.
    /// </summary>
    private static (List<OpenObservation> Demo, List<OpenObservation> Backtest) BuildMeasuredFixture()
    {
        var demo = new List<OpenObservation>();
        var backtest = new List<OpenObservation>();

        void AddPair(DateTime openTime, decimal offset)
        {
            const decimal backtestPrice = 15000m;
            demo.Add(new OpenObservation(openTime, backtestPrice + offset, "buy"));
            backtest.Add(new OpenObservation(openTime, backtestPrice, "Buy"));
        }

        var aprilOffsets = new[] { 16.2m, 25.5m, 22.0m, 22.1m, 24.5m }; // sum 110.3, n=5, mean 22.06
        for (var i = 0; i < aprilOffsets.Length; i++)
            AddPair(new DateTime(2026, 4, 1, 0, 0, 0).AddMinutes(i), aprilOffsets[i]);

        var mayOffsets = new List<decimal> { 11.4m, 31.9m };
        mayOffsets.AddRange(Enumerable.Repeat(22.7m, 12)); // sum 315.7, n=14, mean 22.55
        for (var i = 0; i < mayOffsets.Count; i++)
            AddPair(new DateTime(2026, 5, 1, 0, 0, 0).AddMinutes(i), mayOffsets[i]);

        var juneOffsets = new[] { 38.5m, 44.3m, 40.5m, 40.6m, 40.6m }; // sum 204.5, n=5, mean 40.90
        for (var i = 0; i < juneOffsets.Length; i++)
            AddPair(new DateTime(2026, 6, 1, 0, 0, 0).AddMinutes(i), juneOffsets[i]);

        return (demo, backtest);
    }

    [Fact]
    public void Measure_MeasuredFixture_ReproducesTwentyFourPairsAndMonthlyMeans()
    {
        var (demo, backtest) = BuildMeasuredFixture();

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(24);
        result.Status.Should().Be(ComparabilityReadoutStatus.Measured);
        result.Months.Should().HaveCount(3);

        var april = result.Months.Single(m => m.Month == 4);
        april.PairedCount.Should().Be(5);
        april.MeanOffsetPriceUnits.Should().Be(22.06m);
        april.MinOffsetPriceUnits.Should().Be(16.2m);
        april.MaxOffsetPriceUnits.Should().Be(25.5m);

        var may = result.Months.Single(m => m.Month == 5);
        may.PairedCount.Should().Be(14);
        may.MeanOffsetPriceUnits.Should().Be(22.55m);
        may.MinOffsetPriceUnits.Should().Be(11.4m);
        may.MaxOffsetPriceUnits.Should().Be(31.9m);

        var june = result.Months.Single(m => m.Month == 6);
        june.PairedCount.Should().Be(5);
        june.MeanOffsetPriceUnits.Should().Be(40.90m);
        june.MinOffsetPriceUnits.Should().Be(38.5m);
        june.MaxOffsetPriceUnits.Should().Be(44.3m);
    }

    [Fact]
    public void Measure_OpenTimesDifferingInSeconds_PairsOnTheTruncatedMinute()
    {
        var minute = new DateTime(2026, 5, 1, 10, 15, 0);
        var demo = new List<OpenObservation> { new(minute.AddSeconds(5), 15022.06m, "buy") };
        var backtest = new List<OpenObservation> { new(minute.AddSeconds(47), 15000.00m, "Buy") };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(1);
        result.Months.Single().MeanOffsetPriceUnits.Should().Be(22.06m);
    }

    [Fact]
    public void Measure_OpenTimesOneHourApart_DoNotPair()
    {
        var demo = new List<OpenObservation> { new(new DateTime(2026, 5, 1, 10, 15, 0), 15022.06m, "buy") };
        var backtest = new List<OpenObservation> { new(new DateTime(2026, 5, 1, 11, 15, 0), 15000m, "Buy") };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(0);
        result.DemoOnlyCount.Should().Be(1);
        result.BacktestOnlyCount.Should().Be(1);
        result.Months.Should().BeEmpty();
    }

    /// <summary>
    /// Structural contract test (tasks.md 2.4): every trade in the measured database is a buy, so
    /// no production fixture exercises this path today. The rule MUST still hold — a future short
    /// strategy would silently reintroduce cross-direction mispairing if direction were dropped
    /// from the key.
    /// </summary>
    [Fact]
    public void Measure_SameMinuteOpposingDirections_DoesNotPair()
    {
        var minute = new DateTime(2026, 5, 1, 10, 15, 0);
        var demo = new List<OpenObservation> { new(minute, 15022.06m, "buy") };
        var backtest = new List<OpenObservation> { new(minute, 15000m, "Sell") };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(0);
        result.DemoOnlyCount.Should().Be(1, "the demo trade never found a same-direction backtest partner at this minute");
        result.BacktestOnlyCount.Should().Be(1);
    }

    [Fact]
    public void Measure_MarchOctoberNovemberMonths_ReportTransitionRisk()
    {
        var demo = new List<OpenObservation>();
        var backtest = new List<OpenObservation>();

        void AddPair(DateTime openTime)
        {
            demo.Add(new OpenObservation(openTime, 15010m, "buy"));
            backtest.Add(new OpenObservation(openTime, 15000m, "Buy"));
        }

        AddPair(new DateTime(2026, 3, 5, 10, 0, 0));
        AddPair(new DateTime(2026, 10, 5, 10, 0, 0));
        AddPair(new DateTime(2026, 11, 5, 10, 0, 0));
        AddPair(new DateTime(2026, 1, 5, 10, 0, 0)); // non-transition control

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.Months.Single(m => m.Month == 3).DstRisk.Should().Be(DstTransitionRisk.TransitionMonth);
        result.Months.Single(m => m.Month == 10).DstRisk.Should().Be(DstTransitionRisk.TransitionMonth);
        result.Months.Single(m => m.Month == 11).DstRisk.Should().Be(DstTransitionRisk.TransitionMonth);
        result.Months.Single(m => m.Month == 1).DstRisk.Should().Be(DstTransitionRisk.None);
    }

    /// <summary>Defensive/synthetic — zero same-minute duplicates exist in the measured window (tasks.md 2.6).</summary>
    [Fact]
    public void Measure_TwoDemoOpensInOneMinute_ExcludesThatMinuteAndCountsItAmbiguous()
    {
        var minute = new DateTime(2026, 5, 1, 10, 15, 0);
        var demo = new List<OpenObservation>
        {
            new(minute, 15010m, "buy"),
            new(minute, 15011m, "buy"),
        };
        var backtest = new List<OpenObservation>();

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(0);
        result.AmbiguousMinuteDemoCount.Should().Be(2);
        result.DemoOnlyCount.Should().Be(0, "both demo trades are ambiguous, not unpaired");
        result.BacktestOnlyCount.Should().Be(0);
        result.Months.Should().BeEmpty();
    }

    /// <summary>Mirror of the demo-side case (tasks.md 2.7).</summary>
    [Fact]
    public void Measure_TwoBacktestOpensInOneMinute_ExcludesThatMinuteAndCountsItAmbiguous()
    {
        var minute = new DateTime(2026, 5, 1, 10, 15, 0);
        var demo = new List<OpenObservation>();
        var backtest = new List<OpenObservation>
        {
            new(minute, 15010m, "Buy"),
            new(minute, 15011m, "Buy"),
        };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(0);
        result.AmbiguousMinuteBacktestCount.Should().Be(2);
        result.BacktestOnlyCount.Should().Be(0, "both backtest trades are ambiguous, not unpaired");
        result.DemoOnlyCount.Should().Be(0);
    }

    /// <summary>
    /// Shared assertion for task 2.8's property-style invariant. Not itself an xUnit test method:
    /// <see cref="OpenObservation"/> is <c>internal</c>, and a PUBLIC test method cannot declare a
    /// parameter of a less-accessible type (CS0051) even under <c>InternalsVisibleTo</c> — so each
    /// scenario below is its own <c>[Fact]</c> instead of a <c>[Theory]</c>/<c>MemberData</c> pair.
    /// </summary>
    private static void AssertPartitionInvariant(List<OpenObservation> demo, List<OpenObservation> backtest)
    {
        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        (result.PairedCount + result.DemoOnlyCount + result.AmbiguousMinuteDemoCount)
            .Should().Be(demo.Count, "the three demo buckets must exhaust every demo trade exactly once");
        (result.PairedCount + result.BacktestOnlyCount + result.AmbiguousMinuteBacktestCount)
            .Should().Be(backtest.Count, "the three backtest buckets must exhaust every backtest trade exactly once");
    }

    [Fact]
    public void Measure_AnyInput_BucketsArePartitionsOfBothSides_OnePairPlusOneUnpairedEachSide()
    {
        var demo = new List<OpenObservation>
        {
            new(new DateTime(2026, 1, 1, 0, 0, 0), 15010m, "buy"),
            new(new DateTime(2026, 1, 1, 0, 5, 0), 15020m, "buy"),
        };
        var backtest = new List<OpenObservation>
        {
            new(new DateTime(2026, 1, 1, 0, 0, 0), 15000m, "Buy"),
            new(new DateTime(2026, 1, 1, 0, 10, 0), 15000m, "Buy"),
        };

        AssertPartitionInvariant(demo, backtest);
    }

    [Fact]
    public void Measure_AnyInput_BucketsArePartitionsOfBothSides_AmbiguousBothSidesPlusOnePair()
    {
        var demo = new List<OpenObservation>
        {
            new(new DateTime(2026, 2, 1, 0, 0, 0), 15010m, "buy"),
            new(new DateTime(2026, 2, 1, 0, 0, 0), 15011m, "buy"),
            new(new DateTime(2026, 2, 1, 1, 0, 0), 15020m, "buy"),
        };
        var backtest = new List<OpenObservation>
        {
            new(new DateTime(2026, 2, 1, 2, 0, 0), 15010m, "Buy"),
            new(new DateTime(2026, 2, 1, 2, 0, 0), 15011m, "Buy"),
            new(new DateTime(2026, 2, 1, 1, 0, 0), 15000m, "Buy"),
        };

        AssertPartitionInvariant(demo, backtest);
    }

    [Fact]
    public void Measure_AnyInput_BucketsArePartitionsOfBothSides_MeasuredFixture()
    {
        var (demo, backtest) = BuildMeasuredFixture();

        AssertPartitionInvariant(demo, backtest);
    }

    [Fact]
    public void Measure_AnyInput_BucketsArePartitionsOfBothSides_EmptyBothSides()
        => AssertPartitionInvariant([], []);

    [Fact]
    public void Measure_NoPairedOpens_ReturnsNullFiguresAndZeroCountNotAScore()
    {
        var demo = new List<OpenObservation> { new(new DateTime(2026, 5, 1, 10, 15, 0), 15010m, "buy") };
        var backtest = new List<OpenObservation> { new(new DateTime(2026, 5, 1, 11, 15, 0), 15000m, "Buy") };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(0);
        result.Status.Should().Be(ComparabilityReadoutStatus.NoPairedOpens);
        result.Months.Should().NotBeNull();
        result.Months.Should().BeEmpty("no month qualifies for a row without at least one paired trade — nothing is withheld because nothing was observed");
    }

    [Fact]
    public void Measure_SinglePairedTrade_StillPublishesTheFigureBesideTheCount()
    {
        var minute = new DateTime(2026, 5, 1, 10, 15, 0);
        var demo = new List<OpenObservation> { new(minute, 15022.06m, "buy") };
        var backtest = new List<OpenObservation> { new(minute, 15000m, "Buy") };

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        var month = result.Months.Should().ContainSingle().Subject;
        month.PairedCount.Should().Be(1);
        month.MeanOffsetPriceUnits.Should().Be(22.06m, "no invented minimum ever withholds a figure below n=1");
        month.MinOffsetPriceUnits.Should().Be(22.06m);
        month.MaxOffsetPriceUnits.Should().Be(22.06m);
        month.SignConsistency.Should().Be(1m);
    }

    [Fact]
    public void Measure_MixedSigns_ReportsSignConsistencyBelowOne()
    {
        var demo = new List<OpenObservation>();
        var backtest = new List<OpenObservation>();

        void AddPair(int minuteOffset, decimal offset)
        {
            var t = new DateTime(2026, 5, 1, 0, 0, 0).AddMinutes(minuteOffset);
            demo.Add(new OpenObservation(t, 15000m + offset, "buy"));
            backtest.Add(new OpenObservation(t, 15000m, "Buy"));
        }

        AddPair(0, 10m);
        AddPair(1, 5m);
        AddPair(2, 2m);
        AddPair(3, -3m);
        AddPair(4, -1m);

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        var month = result.Months.Should().ContainSingle().Subject;
        month.PairedCount.Should().Be(5);
        month.SignConsistency.Should().Be(0.6m, "3 positive of 5 paired offsets, never framed as demo outperforming backtest");
    }

    [Fact]
    public void Measure_CalledTwiceWithShuffledInput_ReturnsByteIdenticalOutput()
    {
        var (demo, backtest) = BuildMeasuredFixture();
        var shuffledDemo = demo.AsEnumerable().Reverse().ToList();
        var shuffledBacktest = backtest.AsEnumerable().Reverse().ToList();

        var first = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);
        var second = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, shuffledDemo, shuffledBacktest);

        second.Should().BeEquivalentTo(first, "pairing is keyed by (minute, direction), never by input order");
    }
}
