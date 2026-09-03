using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 2 — the run estimates its own risked amount from its own SL closes (design.md D1/D2).
/// Every "real data" number here is a measurement of a committed fixture, not a target invented to
/// make an implementation pass.
/// </summary>
public class TradeRiskNormalizerEstimateTests
{
    private static readonly LotGrid TwoDecimalGrid = LotGrid.ImoxRetester;
    private static readonly LotGrid OneDecimalGrid = new(sizeDecimals: 1, step: 0.10m, minLot: 0.10m, maxLots: 10m);

    // ---- 2.1: the 2-decimal export estimates its own amount ----

    [Fact]
    public void Estimate_TwoDecimalFixture_RecoversTheRunsOwnAmountFromNinetySlCloses()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        estimate.Status.Should().Be(RunRiskEstimateStatus.Estimated);
        estimate.SlSampleCount.Should().Be(90);
        estimate.ConsistencyFraction.Should().Be(1m, "all 90 feasible bands contain the estimate");

        // D1's tie-break is "smallest value among the most-covered candidates", and the candidates
        // are the bands' LOWER endpoints. The feasible band for this run is [199.98, 200.16) — it
        // brackets the configured $200 (asserted below in the strict-intersection test) but 200.00
        // is not itself a lower endpoint of any band, so the estimator cannot and must not return
        // it. 199.98 is the deterministic answer D1 defines.
        estimate.RiskPerTrade.Should().Be(199.98m);
    }

    [Fact]
    public void Estimate_TwoDecimalFixture_NeverExceedsTheConfiguredAmount()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);

        var maxRealized = trades
            .Where(t => t.CloseType == "SL")
            .Max(t => Math.Abs(t.RealizedRisk!.Value));

        maxRealized.Should().Be(199.98m, "floor sizing tops out just under the target; round-half would straddle it");
        maxRealized.Should().BeLessThan(200m);
    }

    // ---- 2.3: WHY the robust form, not the strict intersection ----

    [Fact]
    public void Estimate_StrictIntersection_HoldsOnTheFineGridAndCollapsesOnTheCoarseOne()
    {
        // This is D1's rejected alternative, computed here rather than shipped. The strict
        // intersection is not wrong on the fine grid — it is simply not a general rule, because on
        // the coarse grid it returns nothing at all while the robust form still returns an answer.
        var fine = StrictIntersection(RawTradeListFixture.Load(RawTradeListFixture.IstFileName), TwoDecimalGrid);
        var coarse = StrictIntersection(RawTradeListFixture.Load(RawTradeListFixture.OostFileName), OneDecimalGrid);

        fine.Low.Should().Be(199.98m);
        fine.High.Should().Be(200.16m);
        fine.Low.Should().BeLessThan(fine.High, "the 2-decimal export's 90 bands share a common point");
        fine.Low.Should().BeLessThanOrEqualTo(200.00m).And.BeLessThan(200.16m);
        (fine.Low <= 200.00m && 200.00m < fine.High)
            .Should().BeTrue("the feasible band brackets the configured $200 without ever being told it");

        coarse.Low.Should().BeGreaterThanOrEqualTo(coarse.High, "the 1-decimal export has NO common point");

        // The robust form answers on both — that is the entire argument for it.
        var fineEstimate = TradeRiskNormalizer.Estimate(RawTradeListFixture.Load(RawTradeListFixture.IstFileName), TwoDecimalGrid);
        var coarseEstimate = TradeRiskNormalizer.Estimate(RawTradeListFixture.Load(RawTradeListFixture.OostFileName), OneDecimalGrid);

        fineEstimate.RiskPerTrade.Should().Be(199.98m, "the smallest point stabbing all 90 bands");
        coarseEstimate.RiskPerTrade.Should().Be(200.00m, "the best-supported point where no common point exists");
    }

    // ---- 2.4: WHY MAE and never Profit (D2) ----

    [Fact]
    public void Estimate_ProfitAsSource_BreaksTheIntersectionWhereRealizedRiskHolds()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);
        var sl = trades.Where(t => t.CloseType == "SL").ToList();

        var maeBreak = FirstEmptyIntersectionIndex(sl.Select(t => (Math.Abs(t.RealizedRisk!.Value), t.Size)), TwoDecimalGrid);
        var profitBreak = FirstEmptyIntersectionIndex(sl.Select(t => (Math.Abs(t.Profit), t.Size)), TwoDecimalGrid);

        maeBreak.Should().BeNull("MAE holds through all 90 SL closes");
        profitBreak.Should().Be(33, "spread and commission break the Profit-sourced intersection at the 33rd SL close");

        BandsContaining(sl.Select(t => (Math.Abs(t.RealizedRisk!.Value), t.Size)), TwoDecimalGrid, 200m)
            .Should().Be(90, "100% of MAE-derived bands contain $200");
        BandsContaining(sl.Select(t => (Math.Abs(t.Profit), t.Size)), TwoDecimalGrid, 200m)
            .Should().Be(62, "only 69% of Profit-derived bands do");
    }

    [Fact]
    public void Estimate_Ticket1851_UsesMaeNotProfit()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);

        var trade = trades.Single(t => t.Ticket == 1851);

        trade.Profit.Should().Be(-174.70m);
        trade.RealizedRisk.Should().Be(173.76m, "the ~$0.94 difference is spread and commission, not risk");
    }

    // ---- 2.5: the coarse grid clears the gate ----

    [Fact]
    public void Estimate_OneDecimalPopulation_IsEstimatedAtNinetyThreePercent()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.OostFileName);

        var estimate = TradeRiskNormalizer.Estimate(trades, OneDecimalGrid);

        estimate.Status.Should().Be(RunRiskEstimateStatus.Estimated);
        estimate.RiskPerTrade.Should().Be(200.00m);
        estimate.SlSampleCount.Should().Be(95);
        estimate.ConsistencyFraction.Should().BeApproximately(0.9263m, 0.0001m, "88 of 95 — clears the 85% floor");
        (estimate.ConsistencyFraction * 95m).Should().BeApproximately(88m, 0.0001m);
        estimate.ConsistencyFraction.Should().BeGreaterThan(TradeRiskNormalizer.MinimumConsistencyFraction);
    }

    // ---- 2.7: WHERE the coarse grid's inconsistency lives (the D5 Unbounded mechanism) ----

    [Fact]
    public void Estimate_OneDecimalPopulation_EveryInconsistentTradeSitsAtTheMinimumLot()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.OostFileName);
        var estimate = TradeRiskNormalizer.Estimate(trades, OneDecimalGrid);
        var estimated = estimate.RiskPerTrade!.Value;

        var sl = trades.Where(t => t.CloseType == "SL").ToList();
        var inconsistent = sl
            .Where(t => !BandContains(Math.Abs(t.RealizedRisk!.Value), t.Size, OneDecimalGrid, estimated))
            .ToList();

        inconsistent.Should().HaveCount(7);
        inconsistent.Should().OnlyContain(t => t.Size == OneDecimalGrid.MinLot,
            "a clamp UP to the minimum lot destroys the inversion — that is the overshoot mechanism, not a modelling failure");
        inconsistent.Min(t => Math.Abs(t.RealizedRisk!.Value)).Should().Be(229.40m);
        inconsistent.Max(t => Math.Abs(t.RealizedRisk!.Value)).Should().Be(404.60m);

        // Min lot is NECESSARY but not SUFFICIENT: most pinned trades are perfectly consistent.
        var pinnedSl = sl.Where(t => t.Size == OneDecimalGrid.MinLot).ToList();
        pinnedSl.Should().HaveCount(29);
        sl.Except(pinnedSl).Should().HaveCount(66);
        sl.Except(pinnedSl)
            .Should().OnlyContain(t => BandContains(Math.Abs(t.RealizedRisk!.Value), t.Size, OneDecimalGrid, estimated),
                "none of the 66 non-pinned SL closes is inconsistent");
    }

    // ---- 2.6: pinning is reported, never gating (D11) ----

    [Fact]
    public void Estimate_MinLotPinnedFraction_SeparatesTheGridsWhereConsistencyCannot()
    {
        var fine = TradeRiskNormalizer.Estimate(
            RawTradeListFixture.Load(RawTradeListFixture.IstFileName), TwoDecimalGrid);
        var coarse = TradeRiskNormalizer.Estimate(
            RawTradeListFixture.Load(RawTradeListFixture.OostFileName), OneDecimalGrid);

        // Denominator is ALL trades, not just the SL closes: the question is whether the grid can
        // express the target at all, which every trade answers.
        fine.MinLotPinnedFraction.Should().BeApproximately(1m / 329m, 0.00001m, "1 of 329 — 0.3%");
        coarse.MinLotPinnedFraction.Should().BeApproximately(114m / 337m, 0.00001m, "114 of 337 — 33.8%");

        // Two orders of magnitude apart, while consistency (100% vs 93%) passes on both. That is
        // the whole reason they are two numbers.
        coarse.MinLotPinnedFraction.Should().BeGreaterThan(100m * fine.MinLotPinnedFraction);
        coarse.Status.Should().Be(RunRiskEstimateStatus.Estimated, "pinning never gates");
    }

    // ---- 2.8: refusals keep their evidence (D4) ----

    [Fact]
    public void Estimate_TwoSlCloses_InsufficientSamplesWithNullRiskPerTrade()
    {
        var trades = new List<BacktestTrade>
        {
            SlTrade(size: 0.10m, realizedRisk: 200m),
            SlTrade(size: 0.10m, realizedRisk: 200m),
            NonSlTrade(size: 0.20m),
        };

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        estimate.Status.Should().Be(RunRiskEstimateStatus.InsufficientSamples);
        estimate.RiskPerTrade.Should().BeNull();
        estimate.SlSampleCount.Should().Be(2, "below the floor of 3");
    }

    [Fact]
    public void Estimate_ThreeSlCloses_ClearsTheSampleFloor()
    {
        var trades = Enumerable.Range(0, 3).Select(_ => SlTrade(size: 0.10m, realizedRisk: 200m)).ToList();

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        TradeRiskNormalizer.MinimumSlSamples.Should().Be(3);
        estimate.Status.Should().Be(RunRiskEstimateStatus.Estimated);
        estimate.RiskPerTrade.Should().Be(200m);
    }

    [Fact]
    public void Estimate_ZeroSlCloses_NeverFallsBackToTheConfiguredAmount()
    {
        var trades = Enumerable.Range(0, 40).Select(_ => NonSlTrade(size: 0.20m)).ToList();

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        estimate.Status.Should().Be(RunRiskEstimateStatus.InsufficientSamples);
        estimate.SlSampleCount.Should().Be(0);
        estimate.RiskPerTrade.Should().BeNull("$200 is a configured intent, never an inferred measurement");
        estimate.RiskPerTrade.Should().NotBe(200m);
        estimate.ConsistencyFraction.Should().Be(0m);
    }

    [Fact]
    public void Estimate_BelowEightyFivePercentConsistency_InconsistentWithTheFractionKept()
    {
        // Ten SL closes at 0.10 lots: seven agree on $200, three sit far outside every band the
        // others produce. 7/10 = 70%, under the 85% floor.
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 7; i++)
            trades.Add(SlTrade(size: 0.10m, realizedRisk: 200m));
        for (var i = 0; i < 3; i++)
            trades.Add(SlTrade(size: 0.10m, realizedRisk: 900m));

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        estimate.Status.Should().Be(RunRiskEstimateStatus.Inconsistent);
        estimate.RiskPerTrade.Should().BeNull();
        estimate.SlSampleCount.Should().Be(10);
        estimate.ConsistencyFraction.Should().Be(0.7m, "the measured fraction must survive the refusal — it IS the diagnosis");
        estimate.ConsistencyFraction.Should().BeLessThan(TradeRiskNormalizer.MinimumConsistencyFraction);
    }

    [Fact]
    public void Estimate_ExactlyAtTheGate_IsNotRefused()
    {
        TradeRiskNormalizer.MinimumConsistencyFraction.Should().Be(0.85m);

        // 17 of 20 = 85.0% exactly.
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 17; i++)
            trades.Add(SlTrade(size: 0.10m, realizedRisk: 200m));
        for (var i = 0; i < 3; i++)
            trades.Add(SlTrade(size: 0.10m, realizedRisk: 900m));

        var estimate = TradeRiskNormalizer.Estimate(trades, TwoDecimalGrid);

        estimate.ConsistencyFraction.Should().Be(0.85m);
        estimate.Status.Should().Be(RunRiskEstimateStatus.Estimated, "the gate is a floor, not a strict threshold");
    }

    // ---- synthetic trades ----

    internal static BacktestTrade SlTrade(decimal size, decimal? realizedRisk, decimal profit = -200m)
        => Trade(size, "SL", realizedRisk, profit);

    internal static BacktestTrade NonSlTrade(decimal size, decimal profit = 50m, string closeType = "TrailingStop")
        => Trade(size, closeType, realizedRisk: null, profit);

    private static BacktestTrade Trade(decimal size, string closeType, decimal? realizedRisk, decimal profit)
        => new()
        {
            Id = Guid.NewGuid(),
            BacktestRunId = Guid.NewGuid(),
            RowIndex = 0,
            Ticket = 1,
            Symbol = "XAUUSD_M1_UTC02",
            Type = "Buy",
            OpenTime = new DateTime(2016, 1, 4, 7, 16, 0, DateTimeKind.Utc),
            OpenPrice = 1000m,
            Size = size,
            CloseTime = new DateTime(2016, 1, 4, 15, 25, 0, DateTimeKind.Utc),
            ClosePrice = 990m,
            Profit = profit,
            Balance = 100_000m,
            SampleTypeRaw = "IST",
            Segment = BacktestSegment.InSampleTest,
            CloseType = closeType,
            RealizedRisk = realizedRisk,
            CreatedAt = DateTime.UtcNow,
        };

    // ---- helpers: the rejected alternatives, computed in the test rather than shipped ----

    private static (decimal Low, decimal High) StrictIntersection(IEnumerable<BacktestTrade> trades, LotGrid grid)
    {
        var low = decimal.MinValue;
        var high = decimal.MaxValue;

        foreach (var (risk, size) in trades.Where(t => t.CloseType == "SL").Select(t => (Math.Abs(t.RealizedRisk!.Value), t.Size)))
        {
            low = Math.Max(low, risk);
            high = Math.Min(high, risk * (size + grid.Step) / size);
        }

        return (low, high);
    }

    /// <summary>1-based index of the sample at which the running intersection first empties, or null.</summary>
    private static int? FirstEmptyIntersectionIndex(IEnumerable<(decimal Risk, decimal Size)> samples, LotGrid grid)
    {
        var low = decimal.MinValue;
        var high = decimal.MaxValue;
        var index = 0;

        foreach (var (risk, size) in samples)
        {
            index++;
            low = Math.Max(low, risk);
            high = Math.Min(high, risk * (size + grid.Step) / size);

            if (low >= high)
                return index;
        }

        return null;
    }

    private static int BandsContaining(IEnumerable<(decimal Risk, decimal Size)> samples, LotGrid grid, decimal value)
        => samples.Count(s => BandContains(s.Risk, s.Size, grid, value));

    private static bool BandContains(decimal risk, decimal size, LotGrid grid, decimal value)
        => risk <= value && value < risk * (size + grid.Step) / size;
}
