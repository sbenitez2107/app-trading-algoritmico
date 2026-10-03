using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Short-distance SL exclusion (change <c>point-value-calibration-short-sl</c>). SQX exports prices
/// rounded to the price grid while MAE uses the unrounded SL level, so SLs under 200 ticks can
/// breach the 0.5% spread gate. Fixtures here use fractional prices and build
/// <see cref="BacktestTrade.RealizedRisk"/> from an UNROUNDED distance.
/// </summary>
public class SymbolPointValueCalibratorShortSlTests
{
    private const string Symbol = "USATECHIDXUSD_M1_UTC02";
    private static readonly DateTime CalibratedAt = new(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);

    private static BacktestTrade SlTrade(decimal openPrice, decimal closePrice, decimal mae) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = Guid.NewGuid(),
        RowIndex = 0,
        Ticket = 1,
        Symbol = Symbol,
        Type = "Buy",
        OpenTime = DateTime.UtcNow,
        OpenPrice = openPrice,
        Size = 1m,
        CloseTime = DateTime.UtcNow,
        ClosePrice = closePrice,
        Profit = 999m,
        Balance = 0,
        SampleTypeRaw = "IST",
        Segment = BacktestSegment.InSampleTest,
        CloseType = "SL",
        RealizedRisk = mae,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>NQ-like: tick 0.1, 5 long SLs (30.0 points, exactly 10) + 2 short SLs (8.0 rounded, 8.06 real).</summary>
    private static List<BacktestTrade> NqLike(decimal open = 21500.1m, decimal closeLong = 21470.1m, decimal closeShort = 21492.1m)
    {
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 5; i++)
            trades.Add(SlTrade(open, closeLong, 300m));
        for (var i = 0; i < 2; i++)
            trades.Add(SlTrade(open, closeShort, 80.6m)); // 80.6 / 8.0 = 10.075 -> 0.75% off
        return trades;
    }

    [Fact]
    public void Calibrate_NqLikeShortSlRoundingOutliers_CalibratesAtTen()
    {
        var result = SymbolPointValueCalibrator.Calibrate(Symbol, NqLike(), CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.PointValue.Should().Be(10m);
        result.SampleCount.Should().Be(5);
        result.MinObserved.Should().Be(10m);
        result.MaxObserved.Should().Be(10m);
    }

    [Fact]
    public void Calibrate_LongSlGenuineOnePercentSpread_StaysInconsistent()
    {
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 5; i++)
            trades.Add(SlTrade(21500.1m, 21470.1m, 300m));  // 10
        for (var i = 0; i < 2; i++)
            trades.Add(SlTrade(21500.1m, 21470.1m, 303m));  // 10.1

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.Inconsistent);
        result.PointValue.Should().BeNull();
        result.SampleCount.Should().Be(7);
        result.MinObserved.Should().Be(10m);
        result.MaxObserved.Should().Be(10.1m);
    }

    [Fact]
    public void Calibrate_AllSamplesUnder200Ticks_InsufficientSamples()
    {
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 4; i++)
            trades.Add(SlTrade(21500.1m, 21492.1m, 80.6m));

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.InsufficientSamples);
        result.SampleCount.Should().Be(0);
        result.PointValue.Should().BeNull();
        result.MinObserved.Should().BeNull();
        result.MaxObserved.Should().BeNull();
    }

    [Fact]
    public void Calibrate_ExactlyTwoHundredTicks_IsRetained()
    {
        var trades = new List<BacktestTrade>
        {
            SlTrade(21500.1m, 21480.1m, 200m), // distance exactly 20.0 = 200 ticks -> retained
            SlTrade(21500.1m, 21480.1m, 200m),
            SlTrade(21500.1m, 21480.1m, 200m),
            SlTrade(21500.1m, 21480.2m, 210m), // distance 19.9 = 199 ticks -> excluded (would be 10.55)
        };

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.SampleCount.Should().Be(3);
        result.PointValue.Should().Be(10m);
    }

    [Fact]
    public void Calibrate_PricesAtStorageScaleFive_SameResultAsScaleOne()
    {
        var scaleOne = SymbolPointValueCalibrator.Calibrate(Symbol, NqLike(), CalibratedAt);

        var scaleFive = SymbolPointValueCalibrator.Calibrate(
            Symbol,
            NqLike(open: 21500.10000m, closeLong: 21470.10000m, closeShort: 21492.10000m),
            CalibratedAt);

        scaleFive.Status.Should().Be(CalibrationStatus.Calibrated);
        scaleFive.SampleCount.Should().Be(5);
        scaleFive.PointValue.Should().Be(10m);
        scaleFive.Should().BeEquivalentTo(scaleOne);
    }

    [Fact]
    public void Calibrate_GoldLikeTwoDecimalPrices_CalibratesAtHundred()
    {
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 4; i++)
            trades.Add(SlTrade(2000.01m, 1995.01m, 500m));   // 5.00 points >= 2.00, exactly 100
        for (var i = 0; i < 3; i++)
            trades.Add(SlTrade(2000.01m, 1998.51m, 150.8m)); // 1.50 points < 2.00, 100.53 -> breaches the gate

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.PointValue.Should().Be(100m);
        result.SampleCount.Should().Be(4);
    }

    [Fact]
    public void Calibrate_DaxLikeLongSls_EverySampleRetained()
    {
        var trades = new List<BacktestTrade>
        {
            SlTrade(18000.1m, 17963.1m, 370m),  // 37.0
            SlTrade(18000.1m, 17950.1m, 500m),  // 50.0
            SlTrade(18000.1m, 17940.1m, 600m),  // 60.0
            SlTrade(18000.1m, 17920.1m, 800m),  // 80.0
            SlTrade(18000.1m, 17900.1m, 1000m), // 100.0
        };

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, CalibratedAt);

        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.PointValue.Should().Be(10m);
        result.SampleCount.Should().Be(5);
    }

    [Fact]
    public void Calibrate_NqLikeReversedOrder_IdenticalResult()
    {
        var forward = NqLike();
        var reversed = NqLike();
        reversed.Reverse();

        var a = SymbolPointValueCalibrator.Calibrate(Symbol, forward, CalibratedAt);
        var b = SymbolPointValueCalibrator.Calibrate(Symbol, reversed, CalibratedAt);

        b.Should().BeEquivalentTo(a);
        a.Status.Should().Be(CalibrationStatus.Calibrated);
    }
}
