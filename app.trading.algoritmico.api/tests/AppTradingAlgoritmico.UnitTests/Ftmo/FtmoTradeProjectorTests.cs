using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// PR P2, Phase 5 — <see cref="FtmoTradeProjector"/>. Money-per-point rescaling (design.md
/// Decision 2): <c>u = q*target*P_src/(Â*M)</c>, <c>q' = clamp(floor(u/step)*step, min, max)</c>,
/// <c>net' = Profit*(q'*M)/(q*P_src)</c>. The production code deliberately does NOT reuse
/// <see cref="TradeResizer"/>/<see cref="BacktestNetSeries"/>.<c>Bridge</c> on the FTMO side; the
/// identity test below calls both to pin that the two agree when <c>P_src == M</c>.
/// </summary>
public class FtmoTradeProjectorTests
{
    private static BacktestTrade MakeTrade(decimal size, decimal profit, int rowIndex = 0) => new()
    {
        BacktestRunId = Guid.NewGuid(),
        RowIndex = rowIndex,
        Ticket = 1,
        Symbol = "TEST",
        Type = "Long",
        OpenTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        OpenPrice = 100m,
        Size = size,
        // One distinct close per row, so Bridge's date-sorted nets can be matched back by close.
        CloseTime = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Unspecified).AddHours(rowIndex),
        ClosePrice = 101m,
        Profit = profit,
        Balance = 10_000m,
        SampleTypeRaw = "InSample",
        Segment = BacktestSegment.InSample,
        CloseType = "TP",
    };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    public void Project_PSrcEqualsM_ReproducesTradeResizerAndBridge(int pSrcInt, int contractSizeInt)
    {
        // Identity from design.md Decision 2: when P_src == M (FX == 1), the projector's q' and net'
        // equal the PRODUCTION TradeResizer.Resize + BacktestNetSeries.Bridge.Build outputs on the
        // same trades. Both sides are computed by the real code, never re-derived here, so a change
        // to either rounding rule turns this red. The rows cover the on-target path with a
        // non-terminating q'/q (3/7, 6/13), a raise to MinLot and a cap at MaxLots.
        decimal pSrc = pSrcInt;
        decimal contractSize = contractSizeInt;
        const decimal fx = 1m;
        const decimal estimated = 200m;
        const decimal target = 100m;
        var grid = new LotGrid(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 1.00m);

        BacktestTrade[] trades =
        [
            MakeTrade(size: 0.07m, profit: 123.45m, rowIndex: 0), // u = 0.035 -> 0.03, q'/q = 3/7
            MakeTrade(size: 0.01m, profit: -37.77m, rowIndex: 1), // u = 0.005 -> raised to 0.01
            MakeTrade(size: 5.00m, profit: 999.99m, rowIndex: 2), // u = 2.5   -> capped at 1.00
            MakeTrade(size: 1.50m, profit: 100m, rowIndex: 3), //    u = 0.75  -> 0.75
            MakeTrade(size: 0.13m, profit: 77.77m, rowIndex: 4), //  u = 0.065 -> 0.06, q'/q = 6/13
        ];

        var profile = new RunRiskProfile(
            new RunRiskEstimate(
                RunRiskEstimateStatus.Estimated, estimated, ConsistencyFraction: 1m,
                MinLotPinnedFraction: 0m, SlSampleCount: 3),
            [.. trades.Select(t => new NormalizedTrade(
                t.Id, t.RowIndex, t.Ticket, t.CloseType, t.Size, t.Profit,
                RiskBasis.Imputed, new TradeRiskInterval(estimated, estimated), RLow: null, RHigh: null))]);

        var resized = TradeResizer.Resize(profile, target, grid);
        var bridged = BacktestNetSeries.Bridge.Build(
            trades, resized, Guid.NewGuid(), "identity", fundingService: null,
            BacktestSegment.InSample, memberWeight: 1m);
        bridged.Status.Should().Be(BacktestNetSeriesStatus.Built);
        var bridgedNetByClose = bridged.Series!.Nets.ToDictionary(n => n.When, n => n.Net);

        resized.Trades.Select(r => r.Outcome).Should().Equal(
            ResizeOutcome.OnTarget, ResizeOutcome.RaisedToMinimum, ResizeOutcome.CappedAtMaximum,
            ResizeOutcome.OnTarget, ResizeOutcome.OnTarget);

        foreach (var trade in trades)
        {
            var projected = FtmoTradeProjector.Project(
                trade, estimated, target, pSrc, contractSize, fx, grid);
            var reference = resized.Trades.Single(r => r.RowIndex == trade.RowIndex);

            projected.FtmoLots.Should().Be(reference.ResizedSize, $"row {trade.RowIndex} lots");
            projected.Outcome.Should().Be(reference.Outcome, $"row {trade.RowIndex} outcome");
            projected.Net.Should().Be(bridgedNetByClose[trade.CloseTime], $"row {trade.RowIndex} net");
        }
    }

    [Fact]
    public void Project_DaxWorkedExample_Gives013LotsAndNet0234xProfit()
    {
        // design.md's exact worked example: q=0.06, Â=200, target=50, P_src=10, C=1, FX=1.08.
        var grid = new LotGrid(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 1000m);
        var trade = MakeTrade(size: 0.06m, profit: 1000m);
        const decimal estimated = 200m;
        const decimal target = 50m;
        const decimal pSrc = 10m;
        const decimal contractSize = 1m;
        const decimal fx = 1.08m;

        var projected = FtmoTradeProjector.Project(
            trade, estimated, target, pSrc, contractSize, fx, grid);

        projected.FtmoLots.Should().Be(0.13m);
        projected.Net.Should().Be(trade.Profit * 0.234m);
    }

    [Fact]
    public void Project_BtcCappedAtMaxLots_ReturnsMaxNotUnbounded()
    {
        // BTC's grid caps at 5.00, not 1000 — a trade whose computed u exceeds it clamps to 5.00.
        var grid = new LotGrid(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 5.00m);
        var trade = MakeTrade(size: 10m, profit: 500m);
        const decimal estimated = 10m;
        const decimal target = 1000m; // huge target vs. a small Â forces u far above the cap
        const decimal pSrc = 1m;
        const decimal contractSize = 1m;
        const decimal fx = 1m;

        var projected = FtmoTradeProjector.Project(
            trade, estimated, target, pSrc, contractSize, fx, grid);

        projected.FtmoLots.Should().Be(5.00m);
        projected.Outcome.Should().Be(ResizeOutcome.CappedAtMaximum);
    }

    [Fact]
    public void Project_BelowMinLot_RaisesToMinimumAndCountsIt()
    {
        var grid = new LotGrid(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m);
        var trade = MakeTrade(size: 0.01m, profit: 5m);
        const decimal estimated = 1000m; // tiny u forces a floor below MinLot
        const decimal target = 1m;
        const decimal pSrc = 1m;
        const decimal contractSize = 1m;
        const decimal fx = 1m;

        var projected = FtmoTradeProjector.Project(
            trade, estimated, target, pSrc, contractSize, fx, grid);

        projected.FtmoLots.Should().Be(0.01m);
        projected.Outcome.Should().Be(ResizeOutcome.RaisedToMinimum);
    }

    [Fact]
    public void Project_NonPositiveSize_IsUnscalableWithNullNet()
    {
        var grid = LotGrid.ImoxRetester;
        var trade = MakeTrade(size: 0m, profit: 100m);

        var projected = FtmoTradeProjector.Project(
            trade, estimatedRiskPerTrade: 100m, targetRiskPerTrade: 100m,
            sourcePointValue: 1m, ftmoContractSize: 1m, fxRate: 1m, ftmoGrid: grid);

        projected.Outcome.Should().Be(ResizeOutcome.Unscalable);
        projected.Net.Should().BeNull();
    }

    // --- Run-level input guards (RELIABILITY-002). Every one of these is a divisor or a sign the
    // formula depends on; each is a property of the RUN, not of one row, so it is refused before
    // any row is projected, reusing the existing FtmoSimulationRefusal reasons. ---

    private static FtmoSimulationRefusal? Refuse(
        decimal estimated = 200m, decimal target = 100m, decimal pSrc = 1m, decimal contractSize = 1m, decimal fx = 1m) =>
        FtmoTradeProjector.RefuseRunInputs(estimated, target, pSrc, contractSize, fx);

    [Fact]
    public void RefuseRunInputs_AllPositive_ReturnsNull()
    {
        Refuse().Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseRunInputs_NonPositiveEstimatedRisk_RefusesRiskNotEstimable(int value)
    {
        Refuse(estimated: value).Should().Be(FtmoSimulationRefusal.RiskNotEstimable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseRunInputs_NonPositiveTarget_RefusesInvalidRequest(int value)
    {
        Refuse(target: value).Should().Be(FtmoSimulationRefusal.InvalidRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseRunInputs_NonPositiveSourcePointValue_RefusesPointValueNotCalibrated(int value)
    {
        Refuse(pSrc: value).Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseRunInputs_NonPositiveContractSize_RefusesInstrumentSpecMissing(int value)
    {
        Refuse(contractSize: value).Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseRunInputs_NonPositiveFxRate_RefusesInvalidFxBand(int value)
    {
        Refuse(fx: value).Should().Be(FtmoSimulationRefusal.InvalidFxBand);
    }

    [Theory]
    [InlineData("estimatedRiskPerTrade", 0)]
    [InlineData("estimatedRiskPerTrade", -1)]
    [InlineData("targetRiskPerTrade", 0)]
    [InlineData("targetRiskPerTrade", -1)]
    [InlineData("sourcePointValue", 0)]
    [InlineData("sourcePointValue", -1)]
    [InlineData("ftmoContractSize", 0)]
    [InlineData("ftmoContractSize", -1)]
    [InlineData("fxRate", 0)]
    [InlineData("fxRate", -1)]
    public void Project_NonPositiveRunInput_ThrowsArgumentOutOfRangeNamingIt_NeverDivideByZero(string parameter, int value)
    {
        // Defence in depth: a caller that skipped RefuseRunInputs gets a named programming error,
        // not a DivideByZeroException and not a silently negative series.
        var grid = new LotGrid(sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m);
        var trade = MakeTrade(size: 1.00m, profit: 100m);
        decimal V(string name, decimal fallback) => name == parameter ? value : fallback;

        var act = () => FtmoTradeProjector.Project(
            trade,
            V("estimatedRiskPerTrade", 200m),
            V("targetRiskPerTrade", 100m),
            V("sourcePointValue", 1m),
            V("ftmoContractSize", 1m),
            V("fxRate", 1m),
            grid);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(parameter);
    }
}
