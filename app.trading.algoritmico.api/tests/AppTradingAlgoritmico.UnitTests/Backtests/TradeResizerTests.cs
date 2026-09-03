using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 4 — rescaling onto the lot grid (design.md D3/D7/D8/D9). The resizer scales the
/// normalizer's bands; it never recomputes risk and never invents precision the normalizer lacked.
/// </summary>
public class TradeResizerTests
{
    private static readonly LotGrid Grid = LotGrid.ImoxRetester;
    private static readonly LotGrid OneDecimalGrid = new(sizeDecimals: 1, step: 0.10m, minLot: 0.10m, maxLots: 10m);

    private static BacktestTrade Sl(decimal size, decimal? realizedRisk, decimal profit = -200m)
        => TradeRiskNormalizerEstimateTests.SlTrade(size, realizedRisk, profit);

    private static BacktestTrade NonSl(decimal size, decimal profit = 50m)
        => TradeRiskNormalizerEstimateTests.NonSlTrade(size, profit);

    private static RunRiskProfile Profile(IReadOnlyList<BacktestTrade> trades, LotGrid? grid = null)
    {
        TradeRiskNormalizer.TryNormalize(trades, grid ?? Grid, out var profile).Should().BeTrue();
        return profile!;
    }

    // ---- 4.1: the round trip (D7) ----

    [Fact]
    public void Resize_ToTheRunsOwnEstimate_ReproducesEveryOriginalSizeExactly()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);
        var profile = Profile(trades);
        var target = profile.Estimate.RiskPerTrade!.Value;

        var series = TradeResizer.Resize(profile, target, Grid);

        series.Trades.Should().HaveCount(329);
        series.TargetRiskPerTrade.Should().Be(target);
        series.Grid.Should().Be(Grid);

        // Every size is already ON the grid, so scale = 1 must be the identity. Anything else means
        // the resizer invented precision the normalizer never had.
        for (var i = 0; i < trades.Count; i++)
        {
            series.Trades[i].ResizedSize.Should().Be(trades[i].Size,
                $"row {i} (ticket {trades[i].Ticket}) must survive a unit rescale untouched");
        }

        series.OnTargetCount.Should().Be(329);
        series.RaisedToMinimumCount.Should().Be(0);
        series.CappedAtMaximumCount.Should().Be(0);
        series.MaxAchievedRisk.Should().Be(199.98m, "the largest bounded achieved risk in the export");
        series.MaxAchievedRisk.Should().BeLessThan(200m);

        // The single min-lot trade is Unbounded ABOVE, so its achieved risk has no ceiling at all —
        // which is why MaxAchievedRisk alone would be a misleading guarantee.
        series.UnknownAchievedRiskCount.Should().Be(1);
        series.Trades.Where(t => t.Basis == RiskBasis.Measured)
            .Should().OnlyContain(t => t.AchievedRisk.High <= 199.98m);
    }

    // ---- 4.2: floor, not round-half (D3) ----

    [Fact]
    public void Resize_FloorVersusRoundHalf_OnlyFloorReproducesTheExportsOwnSizing()
    {
        // The rounding rule is testable against the export itself. Each SL close hands us its own
        // per-lot risk (|MAE| / Size); dividing the configured $200 by that gives the UNROUNDED lot
        // count the platform started from. Only one rounding rule turns those back into the sizes
        // the file actually records.
        var rows = RawTradeListFixture.Load(RawTradeListFixture.IstFileName)
            .Where(t => t.CloseType == "SL")
            .ToList();

        const decimal configured = 200.00m;
        var floorReproduced = 0;
        var roundHalfReproduced = 0;
        var floorOverTarget = 0;
        var roundHalfOverTarget = 0;
        var floorMaxRisk = 0m;
        var roundHalfMaxRisk = 0m;

        foreach (var trade in rows)
        {
            var perLot = Math.Abs(trade.RealizedRisk!.Value) / trade.Size;
            var unrounded = configured / perLot / Grid.Step;

            var floored = Math.Floor(unrounded) * Grid.Step;
            var roundedHalf = Math.Round(unrounded, MidpointRounding.AwayFromZero) * Grid.Step;

            if (floored == trade.Size) floorReproduced++;
            if (roundedHalf == trade.Size) roundHalfReproduced++;

            var flooredRisk = floored * perLot;
            var roundedRisk = roundedHalf * perLot;
            floorMaxRisk = Math.Max(floorMaxRisk, flooredRisk);
            roundHalfMaxRisk = Math.Max(roundHalfMaxRisk, roundedRisk);
            if (flooredRisk > configured) floorOverTarget++;
            if (roundedRisk > configured) roundHalfOverTarget++;
        }

        rows.Should().HaveCount(90);
        floorReproduced.Should().Be(90, "FLOOR is the supported rule — it reproduces all 90 recorded sizes");
        roundHalfReproduced.Should().Be(55, "round-half reproduces only 55 of 90, so it is not what produced this file");

        floorOverTarget.Should().Be(0, "FLOOR is the supported rule — no floored size can exceed the target risk");
        floorMaxRisk.Should().Be(199.98m);
        roundHalfOverTarget.Should().Be(35, "round-half would have over-risked 35 of 90 trades");
        roundHalfMaxRisk.Should().Be(217.20m, "round-half tops out $17.20 ABOVE the configured amount");
    }

    [Fact]
    public void Resize_AtANonUnitTarget_FloorNeverOvershootsWhereRoundHalfWould()
    {
        // At target = Â the two rules agree, because every size is already an exact multiple of the
        // step. They separate as soon as the scale is not 1 — which is every real resize.
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);
        var profile = Profile(trades);
        var estimated = profile.Estimate.RiskPerTrade!.Value;
        const decimal target = 100.00m;

        var series = TradeResizer.Resize(profile, target, Grid);

        var scale = target / estimated;
        var rulesDisagree = 0;
        var maskedByTheMinimumLotClamp = 0;
        var roundHalfOverTarget = 0;

        for (var i = 0; i < trades.Count; i++)
        {
            var raw = trades[i].Size * scale / Grid.Step;
            var flooredRule = Math.Floor(raw) * Grid.Step;
            var roundHalfRule = Math.Round(raw, MidpointRounding.AwayFromZero) * Grid.Step;

            if (flooredRule != roundHalfRule)
            {
                rulesDisagree++;
                if (series.Trades[i].ResizedSize == roundHalfRule)
                    maskedByTheMinimumLotClamp++;
            }

            var high = profile.Trades[i].Risk.High;
            if (high is not null && trades[i].Size > 0m && high.Value * roundHalfRule / trades[i].Size > target)
                roundHalfOverTarget++;
        }

        rulesDisagree.Should().Be(176, "the rules disagree on more than half the export at this target");
        maskedByTheMinimumLotClamp.Should().Be(1,
            "exactly one trade is already at the minimum lot, where the clamp raises the floored size to what round-half would have chosen anyway");
        roundHalfOverTarget.Should().Be(175,
            "every disagreement with a KNOWN upper endpoint over-risks the position; the 176th is the min-lot trade, whose achieved risk has no upper endpoint at all");

        series.Trades
            .Where(t => t.AchievedRisk.High is not null)
            .Should().OnlyContain(t => t.AchievedRisk.High <= target,
                "FLOOR is the supported rule — a floored size can never achieve more than the target");
    }

    // ---- 4.3: clamps are labelled and counted, never silent, never a refusal (D8) ----

    [Fact]
    public void Resize_BelowTheMinimumLot_RaisesToMinimumAndOverRisksTheTarget()
    {
        var profile = Profile([Sl(0.10m, 200m), Sl(0.10m, 200m), Sl(0.10m, 200m)]);

        // Target $2 on Â = $200 → scale 0.01 → 0.001 lots, below the 0.01 floor.
        var series = TradeResizer.Resize(profile, 2.00m, Grid);

        series.RaisedToMinimumCount.Should().Be(3);
        series.OnTargetCount.Should().Be(0);
        series.Trades.Should().OnlyContain(t => t.Outcome == ResizeOutcome.RaisedToMinimum);
        series.Trades.Should().OnlyContain(t => t.ResizedSize == Grid.MinLot);

        // 0.01 of 0.10 lots realizing $200 → $20, ten times the $2 asked for. THE point of D8.
        series.Trades.Should().OnlyContain(t => t.AchievedRisk.High == 20m);
        series.MaxAchievedRisk.Should().Be(20m);
        series.MaxAchievedRisk.Should().BeGreaterThan(series.TargetRiskPerTrade,
            "a raised-to-minimum series is OVER-risked, and the number says so rather than the target being echoed back");
    }

    [Fact]
    public void Resize_AboveTheMaximumLots_CapsAndUnderRisksTheTarget()
    {
        var profile = Profile([Sl(0.10m, 200m), Sl(0.10m, 200m), Sl(0.10m, 200m)]);

        // Target $40,000 on Â = $200 → scale 200 → 20 lots, over the ceiling of 10.
        var series = TradeResizer.Resize(profile, 40_000m, Grid);

        series.CappedAtMaximumCount.Should().Be(3);
        series.Trades.Should().OnlyContain(t => t.Outcome == ResizeOutcome.CappedAtMaximum);
        series.Trades.Should().OnlyContain(t => t.ResizedSize == Grid.MaxLots);
        series.Trades.Should().OnlyContain(t => t.AchievedRisk.High == 20_000m);
        series.MaxAchievedRisk.Should().BeLessThan(series.TargetRiskPerTrade, "capping under-risks; the achieved figure is disclosed, not the target");
    }

    [Fact]
    public void Resize_MixedOutcomes_CountsEachAndNeverRefusesTheSeries()
    {
        var trades = new List<BacktestTrade>
        {
            Sl(0.10m, 200m), Sl(0.10m, 200m), Sl(0.10m, 200m), // Â = 200, all OnTarget at scale 1
            NonSl(0.10m),
        };
        var profile = Profile(trades);

        Action act = () => TradeResizer.Resize(profile, 200m, Grid);

        act.Should().NotThrow("clamping is legitimate and unavoidable — a series is never refused for it");
        var series = TradeResizer.Resize(profile, 200m, Grid);
        series.OnTargetCount.Should().Be(4);
        (series.OnTargetCount + series.RaisedToMinimumCount + series.CappedAtMaximumCount)
            .Should().Be(series.Trades.Count, "every trade lands in exactly one outcome");
    }

    [Fact]
    public void Resize_NonPositiveSize_CannotBeScaledAndReportsNoAchievedRisk()
    {
        var trades = new List<BacktestTrade> { Sl(0.10m, 200m), Sl(0.10m, 200m), Sl(0.10m, 200m), NonSl(0m) };
        var profile = Profile(trades);

        Action act = () => TradeResizer.Resize(profile, 200m, Grid);

        act.Should().NotThrow<DivideByZeroException>();
        var row = TradeResizer.Resize(profile, 200m, Grid).Trades[3];
        row.AchievedRisk.Low.Should().BeNull();
        row.AchievedRisk.High.Should().BeNull();
    }

    // ---- 4.5: the series is unreachable by the weight multiplier (D9) ----

    [Fact]
    public void ResizedTradeSeries_ExposesNoConversionToStrategyTrades()
    {
        var type = typeof(ResizedTradeSeries);

        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name is "op_Implicit" or "op_Explicit")
            .Should().BeEmpty("a conversion operator would reopen the double-sizing path this type exists to close");

        type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name)
            .Should().NotContain(["ToStrategyTrades", "AsStrategyTrades", "ToTrades"]);

        typeof(IReadOnlyList<StrategyTrade>).IsAssignableFrom(type)
            .Should().BeFalse("PortfolioMemberInput.Trades is hard-typed, so passing a series must stay a compile error");

        // The element carries no cost fields and no entity identity, so AnalyticsSeries.NetOf
        // cannot bind to it either — the second structural fact behind D9.
        typeof(ResizedTrade).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().NotContain(["Commission", "Swap", "Taxes"]);
        typeof(ResizedTrade).IsAssignableTo(typeof(Domain.Common.BaseEntity)).Should().BeFalse();

        // ...and the series states its own target, so a consumer never has to infer it from a weight.
        type.GetProperty("TargetRiskPerTrade").Should().NotBeNull();
    }

    // ---- review corrections (RELIABILITY-001/002/004) ----

    /// <summary>Three anchors pin Â = 199.98; the 3.00-lot row is the one under examination.</summary>
    private static BacktestTrade[] AnchoredAt199_98(params BacktestTrade[] extra)
        => [Sl(1.00m, 199.98m), Sl(1.00m, 199.98m), Sl(1.00m, 199.98m), .. extra];

    [Fact]
    public void Resize_WhenTheExactLotCountIsIntegral_DoesNotDriftOneStepLowFromDoubleRounding()
    {
        var profile = Profile(AnchoredAt199_98(Sl(3.00m, 199.98m)));
        profile.Estimate.RiskPerTrade.Should().Be(199.98m);

        // 3.00 × 66.66 / 199.98 is exactly 1.00. Computing `scale = 66.66m / 199.98m` FIRST rounds
        // it to 0.3333…3 at 28 digits, and 3.00 × that floors to 0.99 — one step low, and labelled
        // OnTarget. The drift is one-directional, so no `achieved <= target` assertion can see it.
        var series = TradeResizer.Resize(profile, 66.66m, Grid);

        series.Trades.Single(t => t.OriginalSize == 3.00m).ResizedSize.Should().Be(1.00m);
    }

    [Fact]
    public void Resize_NonPositiveSize_IsNeitherCountedAsOverRiskedNorGivenAFabricatedSize()
    {
        var profile = Profile(AnchoredAt199_98(Sl(0m, null)));

        var series = TradeResizer.Resize(profile, 199.98m, Grid);

        var row = series.Trades.Single(t => t.OriginalSize == 0m);
        row.Outcome.Should().Be(ResizeOutcome.Unscalable);
        row.ResizedSize.Should().Be(0m, "a MinLot size here would be invented from a value Resize itself refuses to divide by");
        row.AchievedRisk.High.Should().BeNull();
        series.RaisedToMinimumCount.Should().Be(0, "RaisedToMinimum asserts the row is over-risked, which is unknowable here");
        series.UnscalableCount.Should().Be(1);
    }

    [Fact]
    public void Resize_ZeroTarget_IsRejectedRatherThanReturningAPlausibleSeries()
    {
        var profile = Profile(AnchoredAt199_98());

        var act = () => TradeResizer.Resize(profile, 0m, Grid);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resize_NegativeTarget_IsRejectedRatherThanPinningEveryTradeAtTheMinimumLot()
    {
        var profile = Profile(AnchoredAt199_98());

        var act = () => TradeResizer.Resize(profile, -1m, Grid);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- spec R5: a target the coarse grid cannot express (D8) ----

    [Fact]
    public void Resize_BelowWhatTheCoarseGridCanExpress_RaisesEveryPinnedRowAndOverRisksTheTarget()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.OostFileName);
        TradeRiskNormalizer.TryNormalize(trades, OneDecimalGrid, out var profile).Should().BeTrue();
        profile!.Estimate.RiskPerTrade.Should().Be(200.00m);

        // The target must sit BELOW Â for anything to be raised at all: at target = Â the scale is
        // exactly 1, every size is already on the grid, and D7 makes the resizer the identity. The
        // 33.8% pinned share of this population is a property of its ORIGINAL sizing — reported by
        // the estimator as MinLotPinnedFraction — and only becomes a resize outcome below Â.
        var identity = TradeResizer.Resize(profile, 200.00m, OneDecimalGrid);
        identity.RaisedToMinimumCount.Should().Be(0, "at target = Â the resizer is the identity");

        var series = TradeResizer.Resize(profile, 100m, OneDecimalGrid);

        series.RaisedToMinimumCount.Should().Be(114, "every row already at 0.1 halves to below the minimum lot");
        series.OnTargetCount.Should().Be(223);
        (series.RaisedToMinimumCount + series.OnTargetCount + series.CappedAtMaximumCount).Should().Be(337);

        var raised = series.Trades.Where(t => t.Outcome == ResizeOutcome.RaisedToMinimum).ToList();
        raised.Should().HaveCount(114);
        raised.Should().OnlyContain(t => t.OriginalSize == 0.10m && t.ResizedSize == 0.10m);

        var measured = raised
            .Where(t => t.Basis == RiskBasis.Measured)
            .Select(t => t.AchievedRisk.High!.Value)
            .ToList();

        measured.Should().HaveCount(29);
        measured.Should().OnlyContain(r => r > 100m, "a clamp up to the minimum lot can only over-risk the target");
        measured.Average().Should().BeApproximately(166.52m, 0.01m);
        measured.Max().Should().Be(404.60m);
    }
}
