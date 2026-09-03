using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 3 — per-trade labelling (design.md D4/D5/D6). The load-bearing claims here are negative
/// ones: a refused run emits NOTHING, and a non-measured risk never becomes a bare number.
/// </summary>
public class TradeRiskNormalizerNormalizeTests
{
    private static readonly LotGrid Grid = LotGrid.ImoxRetester;

    private static BacktestTrade Sl(decimal size, decimal? realizedRisk, decimal profit = -200m)
        => TradeRiskNormalizerEstimateTests.SlTrade(size, realizedRisk, profit);

    private static BacktestTrade NonSl(decimal size, decimal profit = 50m, string closeType = "TrailingStop")
        => TradeRiskNormalizerEstimateTests.NonSlTrade(size, profit, closeType);

    /// <summary>Three SL closes at 0.10 lots realizing $200 each — Â = 200, consistency 3/3.</summary>
    private static List<BacktestTrade> EstimableBase()
        => [Sl(0.10m, 200m), Sl(0.10m, 200m), Sl(0.10m, 200m)];

    // ---- 3.1: a refused run yields nothing to iterate (D4) ----

    [Fact]
    public void TryNormalize_InsufficientSamples_ReturnsFalseAndANullProfile()
    {
        var trades = new List<BacktestTrade> { Sl(0.10m, 200m), Sl(0.10m, 200m), NonSl(0.20m) };

        var normalized = TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        normalized.Should().BeFalse();
        profile.Should().BeNull("a refused run emits NO per-trade rows — not a collection of Unavailable ones a caller could aggregate as though it carried information");
    }

    [Fact]
    public void TryNormalize_Inconsistent_ReturnsFalseAndANullProfile()
    {
        var trades = new List<BacktestTrade>();
        for (var i = 0; i < 7; i++)
            trades.Add(Sl(0.10m, 200m));
        for (var i = 0; i < 3; i++)
            trades.Add(Sl(0.10m, 900m));

        var normalized = TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        normalized.Should().BeFalse();
        profile.Should().BeNull();

        // The evidence still exists — it is just not per-trade.
        var estimate = TradeRiskNormalizer.Estimate(trades, Grid);
        estimate.Status.Should().Be(RunRiskEstimateStatus.Inconsistent);
        estimate.ConsistencyFraction.Should().Be(0.7m);
    }

    [Fact]
    public void TryNormalize_Estimated_ReturnsTrueAndOneRowPerTrade()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(0.20m));

        var normalized = TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        normalized.Should().BeTrue();
        profile.Should().NotBeNull();
        profile!.Trades.Should().HaveCount(4);
        profile.Estimate.RiskPerTrade.Should().Be(200m);
    }

    // ---- 3.2: one trade per basis, and which side each pin opens (D5) ----

    [Fact]
    public void TryNormalize_SlCloseWithRealizedRisk_IsMeasuredAtAPoint()
    {
        var trades = EstimableBase();
        trades.Add(Sl(0.25m, 195m)); // band [195, 202.8) — still contains Â = 200, so the run stays estimable

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile).Should().BeTrue();

        var row = profile!.Trades[3];
        row.Basis.Should().Be(RiskBasis.Measured);
        row.Risk.Low.Should().Be(195m);
        row.Risk.High.Should().Be(195m, "a measurement is a band whose endpoints coincide, not a number pretending to be one");
    }

    [Fact]
    public void TryNormalize_NonSlInsideTheGrid_IsImputedAsABandBelowTheEstimate()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(0.50m));

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var row = profile!.Trades[3];
        row.Basis.Should().Be(RiskBasis.Imputed);
        row.Risk.High.Should().Be(200m, "the band's top is Â itself");
        row.Risk.Low.Should().BeApproximately(200m * 0.50m / 0.51m, 0.0001m);
        row.Risk.Low.Should().NotBeNull();
    }

    [Fact]
    public void TryNormalize_NonSlPinnedAtMinimumLot_OpensTheHighSide()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(Grid.MinLot));

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var row = profile!.Trades[3];
        row.Basis.Should().Be(RiskBasis.Unbounded);
        row.Risk.High.Should().BeNull("a clamp UP to the minimum lot can only RAISE realized risk, so the band is open ABOVE");
        row.Risk.Low.Should().Be(100m, "Â·q/(q+step) = 200·0.01/0.02");
    }

    [Fact]
    public void TryNormalize_NonSlPinnedAtMaximumLots_OpensTheLowSide()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(Grid.MaxLots));

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var row = profile!.Trades[3];
        row.Basis.Should().Be(RiskBasis.Unbounded);
        row.Risk.Low.Should().BeNull("a cap at the maximum can only LOWER realized risk, so the band is open BELOW");
        row.Risk.High.Should().Be(200m);
    }

    [Fact]
    public void TryNormalize_NonPositiveSizeOrMissingRealizedRisk_IsUnavailable()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(0m));           // Size <= 0
        trades.Add(Sl(0.25m, null));     // SL row with no MAE
        trades.Add(Sl(0.25m, 0m));       // SL row with a zero MAE

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        foreach (var row in profile!.Trades.Skip(3))
        {
            row.Basis.Should().Be(RiskBasis.Unavailable);
            row.Risk.Low.Should().BeNull();
            row.Risk.High.Should().BeNull();
        }
    }

    [Fact]
    public void TryNormalize_MeasuredOutranksUnavailable_AndUnavailableOutranksImputed()
    {
        var trades = EstimableBase();
        trades.Add(Sl(0m, 187.50m));   // Size <= 0 but genuinely measured — Measured wins
        trades.Add(NonSl(0m));         // Size <= 0 with nothing measured — Unavailable, not Imputed

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        profile!.Trades[3].Basis.Should().Be(RiskBasis.Measured, "the measurement does not need the size to be valid");
        profile.Trades[3].Risk.Low.Should().Be(187.50m);
        profile.Trades[4].Basis.Should().Be(RiskBasis.Unavailable);
    }

    // ---- 3.3: the labels on real data (D5's evidence) ----

    [Fact]
    public void TryNormalize_TwoDecimalFixture_NoTrailingStopIsEverMeasured()
    {
        var trades = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile).Should().BeTrue();

        var rows = profile!.Trades;
        rows.Should().HaveCount(329);

        var trailing = trades
            .Select((t, i) => (Trade: t, Row: rows[i]))
            .Where(p => p.Trade.CloseType == "TrailingStop")
            .ToList();

        trailing.Should().HaveCount(96);
        trailing.Should().OnlyContain(p => p.Row.Basis != RiskBasis.Measured,
            "a trailing stop moves the EXIT, not the sizing — its loss is not the risk the trade was sized on");
        trailing.Should().OnlyContain(p => p.Row.Basis == RiskBasis.Imputed,
            "none of the 96 sits on a grid edge in this export");

        var sl = trades
            .Select((t, i) => (Trade: t, Row: rows[i]))
            .Where(p => p.Trade.CloseType == "SL")
            .ToList();

        sl.Should().HaveCount(90);
        sl.Should().OnlyContain(p => p.Row.Basis == RiskBasis.Measured);
    }

    [Fact]
    public void TryNormalize_TwoDecimalFixture_TheSlLabelIsCleanAndTrailingStopIsNot()
    {
        // WHY the SL label can be trusted, measured rather than posited. The reference is the run's
        // own median SL |MAE| ($196.43) — what a full stop-out actually costs on this run.
        // The raw MAE column is read for BOTH populations here; the entity keeps it only for SL
        // rows, which is exactly the distinction under examination.
        var rows = RawTradeListFixture.LoadRows(RawTradeListFixture.IstFileName);
        var sl = rows.Where(r => r.Trade.CloseType == "SL").ToList();
        var trailing = rows.Where(r => r.Trade.CloseType == "TrailingStop").ToList();

        var slMedian = Median(sl.Select(r => r.RawMae).ToList());
        slMedian.Should().Be(196.43m);
        var floor = 0.75m * slMedian;

        sl.Should().HaveCount(90);
        sl.Count(r => r.RawMae < floor)
            .Should().Be(0, "not one of the 90 SL closes is a partial loss dressed as a stop-out");

        trailing.Should().HaveCount(96);
        trailing.Count(r => r.RawMae < floor)
            .Should().Be(74, "77% of trailing stops never came close to a full stop");
        trailing.Count(r => r.Trade.Profit > 0)
            .Should().Be(28, "and 28 are outright PROFITABLE exits — a loss category they are not");
    }

    // ---- 3.4: R bounds, and the swap that bites (D6) ----

    [Fact]
    public void TryNormalize_PositiveProfit_RBoundsDivideHighFirst()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(0.50m, profit: 400m));

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var row = profile!.Trades[3];
        row.RLow.Should().Be(400m / 200m, "R is SMALLEST when risk is largest");
        row.RHigh.Should().BeApproximately(400m / (200m * 0.50m / 0.51m), 0.0001m);
        row.RLow.Should().BeLessThan(row.RHigh!.Value);
    }

    [Fact]
    public void TryNormalize_NegativeProfit_RBoundsSwap()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(0.50m, profit: -100m));

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var row = profile!.Trades[3];

        // Dividing a NEGATIVE profit by the band's endpoints inverts the ordering: the naive
        // [P/High, P/Low] would report RLow = -0.50 and RHigh = -0.51, an interval whose low is
        // above its high.
        row.RLow.Should().BeApproximately(-0.51m, 0.0001m, "P/Low — the WORST R comes from the SMALLEST risk when the trade lost money");
        row.RHigh.Should().Be(-100m / 200m);
        row.RLow.Should().BeLessThan(row.RHigh!.Value, "an interval's low is never above its high, in either sign of Profit");
    }

    [Fact]
    public void TryNormalize_NullEndpoint_YieldsANullBoundAndNeverDivides()
    {
        var trades = EstimableBase();
        trades.Add(NonSl(Grid.MinLot, profit: 400m));   // High open
        trades.Add(NonSl(Grid.MaxLots, profit: 400m));  // Low open
        trades.Add(NonSl(0m, profit: 400m));            // both open

        Action act = () => TradeRiskNormalizer.TryNormalize(trades, Grid, out _);
        act.Should().NotThrow<DivideByZeroException>();

        TradeRiskNormalizer.TryNormalize(trades, Grid, out var profile);

        var pinnedLow = profile!.Trades[3];
        pinnedLow.RHigh.Should().Be(4m, "400 / 100, the only endpoint that exists");
        pinnedLow.RLow.Should().BeNull("risk is unbounded above, so R is unbounded below");

        var pinnedHigh = profile.Trades[4];
        pinnedHigh.RLow.Should().Be(2m);
        pinnedHigh.RHigh.Should().BeNull();

        var unavailable = profile.Trades[5];
        unavailable.RLow.Should().BeNull();
        unavailable.RHigh.Should().BeNull();
    }

    // ---- D6 structural guard: no member yields a bare number ----

    [Fact]
    public void TradeRiskInterval_ExposesNoScalarAccessor()
    {
        var type = typeof(TradeRiskInterval);

        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().NotContain(["Value", "Midpoint", "Mean", "Median", "Center", "Risk"]);

        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name is "op_Implicit" or "op_Explicit")
            .Should().BeEmpty("an implicit conversion to decimal is exactly the bare number this type exists to prevent");

        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(decimal))
            .Should().BeEmpty("every endpoint is nullable; a non-nullable decimal member would read as THE risk");

        // BindingFlags.Static is load-bearing. Without it a `static decimal Collapse(interval)`
        // slips through — the same scalar accessor D6 forbids, reached from the other side.
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.ReturnType == typeof(decimal))
            .Should().BeEmpty("static or instance, a method handing back a bare decimal collapses the interval");

        // The name blacklist above only catches members someone thought to name obviously. Pin the
        // decimal-valued surface positively instead: the two endpoints are the whole of it, so a
        // `decimal? Average` or any other nullable-decimal member fails here whatever it is called.
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(decimal?))
            .Select(p => p.Name)
            .Should().BeEquivalentTo(["Low", "High"]);

        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.ReturnType == typeof(decimal?))
            .Should().BeEmpty("a decimal?-returning method is a scalar accessor that merely admits null");
    }

    [Fact]
    public void NormalizedTrade_CarriesRBoundsAndNeverAScalarR()
    {
        typeof(NormalizedTrade).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().Contain(["RLow", "RHigh"]).And.NotContain("R");
    }

    private static decimal Median(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
    }
}
