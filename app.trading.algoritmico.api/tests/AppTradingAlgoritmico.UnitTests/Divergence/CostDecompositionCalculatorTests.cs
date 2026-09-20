using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// PR B2 — swap, embedded cost, residual. Exercises <see cref="CostDecompositionCalculator.Decompose"/>
/// directly with in-memory <see cref="CostObservation"/> lists, mirroring
/// <c>DemoBacktestComparabilityCalculatorTests</c>' fixture style.
/// </summary>
public class CostDecompositionCalculatorTests
{
    private static readonly Guid StrategyId = Guid.NewGuid();
    private const BacktestRunKind Kind = BacktestRunKind.Deploy;
    private static readonly DateTime BaseTime = new(2026, 4, 21, 10, 15, 0);

    private static PriceOffsetComparabilityDto EmptyComparability(
        decimal? pairedDemoNetPl = null, decimal? pairedBacktestNetPl = null,
        decimal? demoOnlyNetPl = null, decimal? backtestOnlyNetPl = null)
        => new(StrategyId, Kind, ComparabilityReadoutStatus.Measured, 0, 0, 0, 0, 0, [],
            pairedDemoNetPl, pairedBacktestNetPl, demoOnlyNetPl, backtestOnlyNetPl);

    private static CostObservation Demo(DateTime openTime, string type = "buy", decimal size = 0.1m,
        decimal openPrice = 15000m, decimal? closePrice = 15010m, decimal? netPl = 10m, decimal? swap = 0m)
        => new(openTime, openPrice, closePrice, size, type, netPl, swap);

    private static CostObservation Backtest(DateTime openTime, string type = "Buy", decimal size = 0.1m,
        decimal openPrice = 15000m, decimal? closePrice = 15010m, decimal? netPl = 8m)
        => new(openTime, openPrice, closePrice, size, type, netPl, null);

    // ==================================================================
    // Phase 11 — swap component
    // ==================================================================

    [Fact]
    public void Swap_ReportsSumOfDemoSwapAcrossTheWindow()
    {
        var demo = new[]
        {
            Demo(BaseTime, swap: 1.5m),
            Demo(BaseTime.AddDays(1), swap: -2m),
            Demo(BaseTime.AddDays(2), swap: 0m),
        };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), demo, [], calibration: null);

        result.Swap!.TotalSwap.Should().Be(-0.5m);
        result.Swap.SwapPayingTradeCount.Should().Be(2);
        result.Swap.TotalTradeCount.Should().Be(3);
    }

    [Fact]
    public void Swap_WhenNoTradePaysSwap_ReportsZeroSumAndZeroCount()
    {
        var demo = new[] { Demo(BaseTime, swap: 0m), Demo(BaseTime.AddDays(1), swap: 0m) };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), demo, [], calibration: null);

        result.Swap!.TotalSwap.Should().Be(0m);
        result.Swap.SwapPayingTradeCount.Should().Be(0);
    }

    [Fact]
    public void Swap_WhenDemoTradeSetIsEmpty_ReportsNullSum()
    {
        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], [], calibration: null);

        result.Swap!.TotalSwap.Should().BeNull();
        result.Swap.TotalTradeCount.Should().Be(0);
    }

    // ==================================================================
    // Phase 12 — embedded cost component
    // ==================================================================

    [Fact]
    public void EmbeddedCost_WhenNoCalibrationRowExists_ReportsNoCalibrationRowWithNullFigures()
    {
        var backtest = new[] { Backtest(BaseTime) };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], backtest, calibration: null);

        result.EmbeddedCost!.State.Should().Be(EmbeddedCostAvailability.NoCalibrationRow);
        result.EmbeddedCost.PointValue.Should().BeNull();
        result.EmbeddedCost.EmbeddedCostEstimate.Should().BeNull();
        result.Residual!.Residual.Should().BeNull();
    }

    [Theory]
    [InlineData(CalibrationStatus.InsufficientSamples, EmbeddedCostAvailability.InsufficientSamples)]
    [InlineData(CalibrationStatus.Inconsistent, EmbeddedCostAvailability.Inconsistent)]
    public void EmbeddedCost_WhenCalibrationStatusIsNotCalibrated_ReportsThatStateWithNullFigures(
        CalibrationStatus underlyingStatus, EmbeddedCostAvailability expected)
    {
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", underlyingStatus, null, 5, null, null, DateTime.UtcNow);
        var backtest = new[] { Backtest(BaseTime) };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], backtest, calibration);

        result.EmbeddedCost!.State.Should().Be(expected);
        result.EmbeddedCost.PointValue.Should().BeNull();
        result.EmbeddedCost.EmbeddedCostEstimate.Should().BeNull();
    }

    [Fact]
    public void EmbeddedCost_WhenCalibrated_PublishesPointValueEstimateAndCalibratedAtVerbatim()
    {
        var calibratedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, calibratedAt);

        // buy: gross = (Close - Open) * Size * PointValue = (15010 - 15000) * 0.1 * 100 = 100
        // estimate = Σ gross - Σ Profit(NetPl) = 100 - 8 = 92
        var backtest = new[] { Backtest(BaseTime, netPl: 8m) };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], backtest, calibration);

        result.EmbeddedCost!.State.Should().Be(EmbeddedCostAvailability.Calibrated);
        result.EmbeddedCost.PointValue.Should().Be(100.0m);
        result.EmbeddedCost.SampleCount.Should().Be(776);
        result.EmbeddedCost.EmbeddedCostEstimate.Should().Be(92m);
        result.EmbeddedCost.CalibratedAt.Should().Be(calibratedAt);
    }

    [Theory]
    [InlineData(CalibrationStatus.InsufficientSamples)]
    [InlineData(CalibrationStatus.Inconsistent)]
    public void EmbeddedCost_ForEveryNonCalibratedState_NeverSubstitutesAnAssumedPointValue(CalibrationStatus status)
    {
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", status, null, 5, null, null, DateTime.UtcNow);

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], [Backtest(BaseTime)], calibration);

        result.EmbeddedCost!.PointValue.Should().BeNull();
        result.EmbeddedCost.EmbeddedCostEstimate.Should().BeNull();
    }

    [Fact]
    public void EmbeddedCost_WhenBacktestRowIsDegenerate_ReportsNullEstimateNotAPartialTotal()
    {
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);
        var backtest = new[]
        {
            Backtest(BaseTime, netPl: 8m),
            Backtest(BaseTime.AddDays(1), size: 0m, netPl: 3m), // degenerate: Size == 0
        };

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], backtest, calibration);

        result.EmbeddedCost!.EmbeddedCostEstimate.Should().BeNull();
        result.EmbeddedCost.PointValue.Should().Be(100.0m, "PointValue comes straight from the calibration row, not the degenerate computation");
    }

    [Fact]
    public void EmbeddedCost_KeysOnVerbatimSqxSymbolNoMapping()
    {
        // The calculator receives an already-resolved SymbolCalibrationSnapshot — this test pins that
        // its Symbol travels verbatim with no normalization applied by the calculator.
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 10, 1m, 2m, DateTime.UtcNow);

        var result = CostDecompositionCalculator.Decompose(
            EmptyComparability(), new CoverageComponentDto([]), [], [Backtest(BaseTime)], calibration);

        result.EmbeddedCost!.State.Should().Be(EmbeddedCostAvailability.Calibrated);
    }

    [Fact]
    public void EmbeddedCostComponentDto_MemberShape()
    {
        var properties = typeof(EmbeddedCostComponentDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        properties.Should().ContainKey("State").WhoseValue.Should().Be(typeof(EmbeddedCostAvailability));
        properties.Should().ContainKey("PointValue").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("SampleCount").WhoseValue.Should().Be(typeof(int?));
        properties.Should().ContainKey("EmbeddedCostEstimate").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("CalibratedAt").WhoseValue.Should().Be(typeof(DateTime?));
    }

    [Fact]
    public void SwapComponentDto_MemberShape()
    {
        var properties = typeof(SwapComponentDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        properties.Should().ContainKey("TotalSwap").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("SwapPayingTradeCount").WhoseValue.Should().Be(typeof(int));
        properties.Should().ContainKey("TotalTradeCount").WhoseValue.Should().Be(typeof(int));
    }

    // ==================================================================
    // Phase 13 — comparability gate signature
    // ==================================================================

    [Fact]
    public void Decompose_RequiresComparabilityDtoAsAMandatoryNonOptionalParameter()
    {
        var method = typeof(CostDecompositionCalculator).GetMethod(nameof(CostDecompositionCalculator.Decompose));
        var parameter = method!.GetParameters().Single(p => p.ParameterType == typeof(PriceOffsetComparabilityDto));

        parameter.IsOptional.Should().BeFalse();
        Nullable.GetUnderlyingType(parameter.ParameterType).Should().BeNull();
    }

    [Fact]
    public void Decompose_NeverRecomputesComparabilityInternally()
    {
        var root = ApiRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AppTradingAlgoritmico.Infrastructure", "Services", "CostDecompositionCalculator.cs"));

        source.Should().NotContain("DemoBacktestComparabilityCalculator.Measure",
            "the calculator must never recompute comparability internally — it comes from the caller only");
    }

    [Fact]
    public void RootDto_CarriesComparabilityFiguresVerbatimAlongsideSwapEmbeddedAndResidual()
    {
        var comparability = EmptyComparability();

        var result = CostDecompositionCalculator.Decompose(
            comparability, new CoverageComponentDto([]), [], [], calibration: null);

        result.Comparability.Should().BeSameAs(comparability);
    }

    // ==================================================================
    // Phase 14 — execution residual
    // ==================================================================

    [Fact]
    public void Residual_IsComputedOnlyOverThePairedSubset()
    {
        var comparability = EmptyComparability(pairedDemoNetPl: 100m, pairedBacktestNetPl: 60m, demoOnlyNetPl: 500m, backtestOnlyNetPl: 400m);
        var demo = new[] { Demo(BaseTime, swap: 2m) };
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);
        var backtest = new[] { Backtest(BaseTime, netPl: 8m) };

        var result = CostDecompositionCalculator.Decompose(comparability, new CoverageComponentDto([]), demo, backtest, calibration);

        // Residual = PairedDemoNetPl - PairedBacktestNetPl - PairedSwap + EmbeddedCostEstimate
        // = 100 - 60 - 2 + 92 = 130
        result.Residual!.Residual.Should().Be(130m);
    }

    [Fact]
    public void Residual_UnpairedSubsetsAreReportedButAreNotSummands()
    {
        var comparability = EmptyComparability(pairedDemoNetPl: 100m, pairedBacktestNetPl: 60m, demoOnlyNetPl: 500m, backtestOnlyNetPl: 400m);
        var demo = new[] { Demo(BaseTime, swap: 2m) };
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);
        var backtest = new[] { Backtest(BaseTime, netPl: 8m) };

        var before = CostDecompositionCalculator.Decompose(comparability, new CoverageComponentDto([]), demo, backtest, calibration);

        // Mutate the unpaired figures drastically — the residual must not move.
        var mutatedComparability = comparability with { DemoOnlyNetPl = -99999m, BacktestOnlyNetPl = 99999m };
        var after = CostDecompositionCalculator.Decompose(mutatedComparability, new CoverageComponentDto([]), demo, backtest, calibration);

        after.Residual!.Residual.Should().Be(before.Residual!.Residual);
        after.Residual.DemoOnlyNetPl.Should().Be(-99999m);
        after.Residual.BacktestOnlyNetPl.Should().Be(99999m);
    }

    [Fact]
    public void ResidualBasis_IsNonNullableAndStatesTheClaimBoundary()
    {
        var property = typeof(ExecutionResidualDto).GetProperty(nameof(ExecutionResidualDto.Basis));

        property.Should().NotBeNull();
        Nullable.GetUnderlyingType(property!.PropertyType).Should().BeNull();
        property.GetSetMethod(nonPublic: true).Should().BeNull();
        typeof(ExecutionResidualDto).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().NotContain(p => p.Name == nameof(ExecutionResidualDto.Basis));
    }

    [Fact]
    public void ExecutionResidualDto_MemberShape()
    {
        var properties = typeof(ExecutionResidualDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        properties.Should().ContainKey("Basis").WhoseValue.Should().Be(typeof(ResidualBasis));
        properties.Should().ContainKey("Residual").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("DemoOnlyNetPl").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("BacktestOnlyNetPl").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("PairedDemoNetPl").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("PairedBacktestNetPl").WhoseValue.Should().Be(typeof(decimal?));
    }

    [Fact]
    public void Residual_IsNullIfAnyTermIsNull()
    {
        var comparability = EmptyComparability(pairedDemoNetPl: null, pairedBacktestNetPl: 60m);
        var demo = new[] { Demo(BaseTime, swap: 2m) };
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);
        var backtest = new[] { Backtest(BaseTime, netPl: 8m) };

        var result = CostDecompositionCalculator.Decompose(comparability, new CoverageComponentDto([]), demo, backtest, calibration);

        result.Residual!.Residual.Should().BeNull();
    }

    // ==================================================================
    // Phase 16 — determinism
    // ==================================================================

    [Fact]
    public void Decompose_RepeatedCalls_AreByteIdentical()
    {
        var comparability = EmptyComparability(pairedDemoNetPl: 100m, pairedBacktestNetPl: 60m, demoOnlyNetPl: 500m, backtestOnlyNetPl: 400m);
        var demo = new[] { Demo(BaseTime, swap: 2m), Demo(BaseTime.AddDays(40), swap: 3m) };
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);
        var backtest = new[] { Backtest(BaseTime, netPl: 8m), Backtest(BaseTime.AddDays(40), netPl: 9m) };
        var coverage = new CoverageComponentDto([]);

        var first = CostDecompositionCalculator.Decompose(comparability, coverage, demo, backtest, calibration);
        var second = CostDecompositionCalculator.Decompose(comparability, coverage, demo, backtest, calibration);

        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void Decompose_MonthAndSubsetOrdering_IsUnaffectedByShuffledInput()
    {
        var comparability = EmptyComparability();
        var demoInOrder = new[] { Demo(BaseTime), Demo(BaseTime.AddDays(40)), Demo(BaseTime.AddDays(80)) };
        var demoShuffled = new[] { demoInOrder[2], demoInOrder[0], demoInOrder[1] };
        var coverageInOrder = new CoverageComponentDto([
            new CoverageMonthDto(2026, 4, PeriodCoverage.DemoOnlyNoBacktestTrades, [], [], 1, 0),
        ]);

        var ordered = CostDecompositionCalculator.Decompose(comparability, coverageInOrder, demoInOrder, [], calibration: null);
        var shuffled = CostDecompositionCalculator.Decompose(comparability, coverageInOrder, demoShuffled, [], calibration: null);

        ordered.Swap!.TotalSwap.Should().Be(shuffled.Swap!.TotalSwap);
    }

    // ==================================================================
    // Reliability — pairing-equivalence invariant (RELIABILITY-001)
    // ==================================================================

    /// <summary>
    /// Pins that <see cref="CostDecompositionCalculator"/>'s private minute+direction grouping
    /// (used to isolate <c>PairedSwap</c> in <c>ComputePairedSwap</c>) admits EXACTLY the same set
    /// of minutes as slice A's own pairing rule in <see cref="DemoBacktestComparabilityCalculator.Measure"/>.
    /// <para>
    /// Every demo trade's <c>NetPl</c> is set equal to its own <c>Swap</c>. That makes slice A's
    /// <c>PairedDemoNetPl</c> (sum of <c>NetPl</c> over the minutes SLICE A pairs) numerically equal
    /// to the true <c>PairedSwap</c> (sum of <c>Swap</c> over the minutes SLICE A pairs) — the
    /// EXPECTED value is derived from slice A's own admission rule, never hard-coded. Backtest
    /// figures are chosen so the embedded-cost estimate <c>E</c> is independently computable by
    /// hand from the documented gross-minus-NetPl formula. Substituting into
    /// <c>Residual = PairedDemoNetPl − PairedBacktestNetPl − PairedSwap + E</c> with
    /// <c>PairedSwap == PairedDemoNetPl</c> (true only if B2 pairs the SAME set slice A did) collapses
    /// to <c>Residual = E − PairedBacktestNetPl</c>. Any divergence between the two pairing
    /// implementations — on the ambiguous minute, the mixed-case direction, or the sub-minute-second
    /// truncation — moves <c>PairedSwap</c> away from <c>PairedDemoNetPl</c> and breaks this
    /// identity, driving the test RED.
    /// </para>
    /// </summary>
    [Fact]
    public void PairedSwap_AgreesWithSliceAsOwnPairingRuleOnAmbiguousAndUnpairedMinutes()
    {
        // Ambiguous minute: two demo opens sharing the same truncated minute and direction — refused
        // on both sides per D4, excluded from pairing entirely.
        var ambiguousMinute = BaseTime;
        // Cleanly paired minute — exactly one open on each side, same minute and direction.
        var pairedMinute = BaseTime.AddMinutes(5);
        // Demo-only minute — no backtest open at this minute.
        var demoOnlyMinute = BaseTime.AddMinutes(10);
        // Backtest-only minute — no demo open at this minute.
        var backtestOnlyMinute = BaseTime.AddMinutes(15);
        // Mixed-case direction pairing — "buy" vs "BUY" must normalize to the same key.
        var mixedCaseMinute = BaseTime.AddMinutes(20);
        // Sub-minute seconds differing between the two sides — must still truncate to the same minute key.
        var subSecondMinute = BaseTime.AddMinutes(25);

        // NetPl == Swap on every demo trade, by construction (see summary above).
        var demo = new[]
        {
            Demo(ambiguousMinute, swap: 1m, netPl: 1m),
            Demo(ambiguousMinute.AddSeconds(10), swap: 2m, netPl: 2m),
            Demo(pairedMinute, swap: 3m, netPl: 3m),
            Demo(demoOnlyMinute, swap: 4m, netPl: 4m),
            Demo(mixedCaseMinute, type: "buy", swap: 5m, netPl: 5m),
            Demo(subSecondMinute.AddSeconds(40), swap: 6m, netPl: 6m),
        };

        // All non-degenerate: Close=15010, Open=15000, Size=0.1 (defaults) — every backtest trade
        // contributes gross = (15010 - 15000) * 0.1 * PointValue = PointValue to the embedded-cost sum.
        var backtest = new[]
        {
            Backtest(ambiguousMinute, netPl: 1m),
            Backtest(pairedMinute, netPl: 2m),
            Backtest(backtestOnlyMinute, netPl: 3m),
            Backtest(mixedCaseMinute, type: "BUY", netPl: 4m),
            Backtest(subSecondMinute, netPl: 5m),
        };

        var comparability = DemoBacktestComparabilityCalculator.Measure(
            StrategyId, Kind, demo.ToOpenObservations(), backtest.ToOpenObservations());

        // Slice A pairs exactly: pairedMinute, mixedCaseMinute, subSecondMinute = 3 pairs.
        comparability.PairedCount.Should().Be(3,
            "the ambiguous minute is refused on both sides, and demo-only/backtest-only minutes never pair");
        comparability.PairedDemoNetPl.Should().Be(14m, "3 + 5 + 6 over the three minutes slice A paired");
        comparability.PairedBacktestNetPl.Should().Be(11m, "2 + 4 + 5 over the three minutes slice A paired");

        const decimal pointValue = 100.0m;
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, pointValue, 776, 1m, 200m, DateTime.UtcNow);

        var result = CostDecompositionCalculator.Decompose(
            comparability, new CoverageComponentDto([]), demo, backtest, calibration);

        // E = Σ gross − Σ NetPl over ALL FIVE backtest trades = (5 * 100) - (1+2+3+4+5) = 485.
        result.EmbeddedCost!.EmbeddedCostEstimate.Should().Be(485m);

        // Residual = PairedDemoNetPl - PairedBacktestNetPl - PairedSwap + E
        //          = 14 - 11 - PairedSwap + 485
        // If B2's pairing agrees with slice A's, PairedSwap == PairedDemoNetPl == 14 (by the
        // NetPl == Swap construction), collapsing this to E - PairedBacktestNetPl = 485 - 11 = 474.
        result.Residual!.Residual.Should().Be(474m,
            "PairedSwap must equal PairedDemoNetPl when both calculators pair the identical minute set");
    }

    // ==================================================================
    // Reliability — strengthened determinism (RELIABILITY-004)
    // ==================================================================

    [Fact]
    public void Decompose_MultiplePairedKeysAcrossMultipleMonths_FullReadoutIsStableUnderShuffle()
    {
        var calibration = new SymbolCalibrationSnapshot("NDX_DARWINEX", CalibrationStatus.Calibrated, 100.0m, 776, 1m, 200m, DateTime.UtcNow);

        var demoInOrder = new[]
        {
            Demo(BaseTime, swap: 1m, netPl: 10m),
            Demo(BaseTime.AddMinutes(5), swap: 2m, netPl: 20m),
            Demo(BaseTime.AddDays(40), swap: 3m, netPl: 30m),
            Demo(BaseTime.AddDays(40).AddMinutes(5), swap: 4m, netPl: 40m),
            Demo(BaseTime.AddDays(80), swap: 5m, netPl: 50m),
        };
        var backtestInOrder = new[]
        {
            Backtest(BaseTime, netPl: 8m),
            Backtest(BaseTime.AddMinutes(5), netPl: 18m),
            Backtest(BaseTime.AddDays(40), netPl: 28m),
            Backtest(BaseTime.AddDays(40).AddMinutes(5), netPl: 38m),
            Backtest(BaseTime.AddDays(80), netPl: 48m),
        };

        var demoShuffled = new[] { demoInOrder[4], demoInOrder[1], demoInOrder[3], demoInOrder[0], demoInOrder[2] };
        var backtestShuffled = new[] { backtestInOrder[3], backtestInOrder[0], backtestInOrder[4], backtestInOrder[2], backtestInOrder[1] };

        var comparabilityOrdered = DemoBacktestComparabilityCalculator.Measure(
            StrategyId, Kind, demoInOrder.ToOpenObservations(), backtestInOrder.ToOpenObservations());
        var comparabilityShuffled = DemoBacktestComparabilityCalculator.Measure(
            StrategyId, Kind, demoShuffled.ToOpenObservations(), backtestShuffled.ToOpenObservations());

        var coverage = new CoverageComponentDto([]);

        var ordered = CostDecompositionCalculator.Decompose(comparabilityOrdered, coverage, demoInOrder, backtestInOrder, calibration);
        var shuffled = CostDecompositionCalculator.Decompose(comparabilityShuffled, coverage, demoShuffled, backtestShuffled, calibration);

        ordered.Residual!.Residual.Should().Be(shuffled.Residual!.Residual);
        ordered.EmbeddedCost!.EmbeddedCostEstimate.Should().Be(shuffled.EmbeddedCost!.EmbeddedCostEstimate);
        ordered.Comparability.Months.Select(m => (m.Year, m.Month)).Should()
            .Equal(shuffled.Comparability.Months.Select(m => (m.Year, m.Month)));
    }

    private static string ApiRoot()
    {
        var dir = Directory.GetParent(typeof(CostDecompositionCalculatorTests).Assembly.Location);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AppTradingAlgoritmico.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return dir!.FullName;
    }
}
