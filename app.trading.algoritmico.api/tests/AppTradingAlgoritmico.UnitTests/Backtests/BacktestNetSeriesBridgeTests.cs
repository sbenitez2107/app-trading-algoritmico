using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 2A — the dated bridge (design.md D1/D2/D3). It pairs an already-sized
/// <see cref="ResizedTradeSeries"/> against the source rows the caller still holds, by
/// <c>RowIndex</c> LOOKUP, and it is the first consumer that discharges slice 2a's D9 obligation:
/// possession of a <see cref="BacktestNetSeries"/> is proof the member's weight was checked.
/// </summary>
public class BacktestNetSeriesBridgeTests
{
    private static readonly Guid StrategyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ---- 2.2: the net is Profit rescaled linearly in volume (D2/D7) ----

    [Fact]
    public void Build_AtTheRunsOwnEstimate_ReproducesEverySourceProfitExactlyAndItsCloseDate()
    {
        var source = RawTradeListFixture.Load(RawTradeListFixture.IstFileName);
        TradeRiskNormalizer.TryNormalize(source, LotGrid.ImoxRetester, out var profile).Should().BeTrue();
        var target = profile!.Estimate.RiskPerTrade!.Value;
        target.Should().Be(199.98m, "the IST export's own Â — slice 2a's measured anchor");

        var resized = TradeResizer.Resize(profile, target, LotGrid.ImoxRetester);

        var result = BacktestNetSeries.Bridge.Build(
            source, resized, StrategyId, "IST", fundingService: null,
            BacktestSegment.InSampleTest, memberWeight: 1m);

        result.Status.Should().Be(BacktestNetSeriesStatus.Built);
        var series = result.Series!;
        series.Nets.Should().HaveCount(329);
        series.TradeCount.Should().Be(329);
        series.ExcludedUnscalableCount.Should().Be(0);
        series.TargetRiskPerTrade.Should().Be(target);
        series.Segment.Should().Be(BacktestSegment.InSampleTest);

        // At target = Â the resizer is the identity, so ResizedSize/OriginalSize == 1 for every row
        // and net_i must be the source Profit VERBATIM — not rounded, not re-derived.
        var byRow = source.ToDictionary(t => t.RowIndex);
        var expected = resized.Trades
            .Select(r => new DatedNet(byRow[r.RowIndex].CloseTime, byRow[r.RowIndex].Profit))
            .OrderBy(d => d.When)
            .ToList();

        series.Nets.Should().Equal(expected);
        series.Nets.Sum(n => n.Net).Should().Be(source.Sum(t => t.Profit));
    }

    [Fact]
    public void Build_AtHalfTheRunsEstimate_ScalesEveryNetByTheRowsOwnSizeRatio()
    {
        var source = HandBuiltSource();
        var resized = HandBuiltResized(source, scale: 0.5m);

        var series = BuildOrThrow(source, resized);

        // net_i = Profit * (ResizedSize / OriginalSize) — linear in volume, per row, never a
        // series-level scalar applied uniformly.
        series.Nets.Select(n => n.Net).Should().Equal(-100m, 25m, 75m);
    }

    // ---- 2.3 / 2.4: a pairing failure THROWS; it is never a status (D1) ----

    [Fact]
    public void Build_ResizedRowWithNoSourceMatch_ThrowsArgumentExceptionNamingTheRowIndex()
    {
        var source = HandBuiltSource();
        var resized = HandBuiltResized(source, scale: 1m) with
        {
            Trades =
            [
                Row(rowIndex: 0, original: 1m, resized: 1m),
                Row(rowIndex: 77, original: 1m, resized: 1m),
            ],
        };

        var act = () => BacktestNetSeries.Bridge.Build(
            source, resized, StrategyId, "M", null, BacktestSegment.InSampleTest, 1m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*77*")
            .Which.Message.Should().Contain("no source trade", "the message must name WHICH failure, not merely that one occurred");
    }

    [Fact]
    public void Build_DuplicatedSourceRowIndex_ThrowsArgumentExceptionNamingTheRowIndex()
    {
        // The concatenated-runs wiring error: slice 1's unique (BacktestRunId, RowIndex) index
        // cannot produce this from ONE run, but a caller that merged two runs' rows can.
        var source = HandBuiltSource();
        source.Add(Trade(rowIndex: 1, profit: 999m, size: 1m, day: 20));
        var resized = HandBuiltResized(HandBuiltSource(), scale: 1m);

        var act = () => BacktestNetSeries.Bridge.Build(
            source, resized, StrategyId, "M", null, BacktestSegment.InSampleTest, 1m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RowIndex 1*")
            .Which.Message.Should().Contain("more than once");
    }

    /// <summary>
    /// DEFENSIVE GUARD — HAND-CONSTRUCTED, and there is NO production producer of this shape today.
    /// <para>
    /// <c>TradeResizer.Resize</c> adds a row for every source trade unconditionally (its
    /// <c>rows.Add</c> sits inside the <c>foreach</c> but OUTSIDE the outcome switch), so every real
    /// <see cref="ResizedTradeSeries"/> satisfies <c>Trades.Count == source.Count</c>. A
    /// fixture-driven version of this test would therefore be GREEN under the rejected positional
    /// zip and would prove nothing. The subset must be assembled by hand, with a non-contiguous
    /// <c>RowIndex</c> set, or the lookup semantics are untestable.
    /// </para>
    /// </summary>
    [Fact]
    public void Build_HandBuiltNonContiguousSubset_PairsByLookupAndDoesNotRefuseTheDifferingCount()
    {
        var source = HandBuiltSource();
        source.Add(Trade(rowIndex: 3, profit: 400m, size: 1m, day: 4));
        source.Add(Trade(rowIndex: 4, profit: 500m, size: 1m, day: 5));

        var resized = HandBuiltResized(source, scale: 1m) with
        {
            // Rows 0 and 3 only: a strict, NON-CONTIGUOUS subset. Under a positional zip this pairs
            // row 3 against source[1] and publishes a complete, plausible-looking, wrong series.
            Trades = [Row(rowIndex: 0, original: 1m, resized: 1m), Row(rowIndex: 3, original: 1m, resized: 1m)],
        };

        var series = BuildOrThrow(source, resized);

        series.Nets.Select(n => n.Net).Should().Equal(-200m, 400m);
        series.TradeCount.Should().Be(2, "the trade count is the RESIZED row count, the denominator actually converted");
    }

    // ---- 2.6: the non-unit-weight refusal (D3) ----

    /// <summary>
    /// ONE test for THREE spec scenarios, deliberately: `backtest-net-series-bridge`'s
    /// "Non-unit weight is refused, not applied", its "A zero weight is an error, not an exclusion",
    /// its "Weight refusal is not a throw", and `trade-risk-normalization`'s pointer scenario
    /// "The obligation is discharged by the bridge capability" all assert the same behaviour. Three
    /// separate tests would be three copies to keep in step.
    /// </summary>
    [Theory]
    [InlineData(1.5)]   // double-sizes
    [InlineData(0.5)]   // half-sizes
    [InlineData(0.0)]   // "excluded" — a member is excluded by not being passed, never by a weight
    public void Build_NonUnitWeight_IsRefusedAsAStatusNamingMemberAndWeight_AndNeverThrows(double weight)
    {
        var source = HandBuiltSource();
        var resized = HandBuiltResized(source, scale: 1m);
        var offered = (decimal)weight;

        var act = () => BacktestNetSeries.Bridge.Build(
            source, resized, StrategyId, "Member A", null, BacktestSegment.InSampleTest, offered);

        act.Should().NotThrow("the weight refusal is a STATUS the caller inspects, not the throw a pairing failure is");

        var result = act();
        result.Status.Should().Be(BacktestNetSeriesStatus.NonUnitWeight);
        result.Series.Should().BeNull("a refused member has no series at all — not an empty one, not a flagged one");
        result.OfferedWeight.Should().Be(offered);
        result.StrategyId.Should().Be(StrategyId);
        result.Label.Should().Be("Member A");

        BacktestNetSeries.Bridge.TryBuild(
                source, resized, StrategyId, "Member A", null, BacktestSegment.InSampleTest, offered, out var series)
            .Should().BeFalse();
        series.Should().BeNull();
    }

    [Fact]
    public void Build_UnitWeight_ConvertsWithEveryNetUnscaled()
    {
        var source = HandBuiltSource();
        var resized = HandBuiltResized(source, scale: 1m);

        var series = BuildOrThrow(source, resized);

        series.Nets.Select(n => n.Net).Should().Equal(-200m, 50m, 150m);
    }

    // ---- 2.7: Unscalable rows are excluded and counted, never contributed as 0 (D2) ----

    [Fact]
    public void Build_UnscalableRows_ContributeNoNetAtAllAndAreCounted()
    {
        var source = HandBuiltSource();
        source.Add(Trade(rowIndex: 3, profit: 42m, size: 0m, day: 4));
        var resized = HandBuiltResized(source, scale: 1m) with
        {
            Trades =
            [
                Row(0, 1m, 1m),
                Row(1, 1m, 1m),
                Row(2, 1m, 1m),
                Row(3, 0m, 0m, ResizeOutcome.Unscalable),
            ],
            UnscalableCount = 1,
        };

        var series = BuildOrThrow(source, resized);

        series.Nets.Should().HaveCount(3);
        series.Nets.Select(n => n.Net).Should().NotContain(0m, "a zero net is a BREAKEVEN trade — a different claim entirely");
        series.ExcludedUnscalableCount.Should().Be(1);
        series.TradeCount.Should().Be(4);

        // The reconciliation an operator has to be able to perform (P3).
        (series.TradeCount - series.ExcludedUnscalableCount).Should().Be(series.Nets.Count);
    }

    // ---- 2.9: the structural half of D3, asserted by reflection ----

    [Fact]
    public void BacktestNetSeries_HasNoPublicConstructorNoScalingMemberAndNoDensity()
    {
        var type = typeof(BacktestNetSeries);

        type.IsSealed.Should().BeTrue();
        type.IsClass.Should().BeTrue("a struct has a `default` instance no matter how private its constructor");
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().BeEmpty("the ONLY factory is the nested Bridge — that is what makes 'every instance had its weight checked' a fact about the type system");

        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name)
            .ToList();

        members.Should().NotContain(n => n.Contains("Scale", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Weight", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Multiply", StringComparison.OrdinalIgnoreCase));
        members.Should().NotContain("Density", "density is measured ONCE, in Infrastructure, by the code that gates on it (D4)");
    }

    [Fact]
    public void PortfolioMemberInput_StillCannotBindAnAlreadySizedSeries()
    {
        // Slice 2a's D9 fact (1), re-asserted here because this slice is the first consumer.
        var tradesParameter = typeof(PortfolioMemberInput)
            .GetConstructors().Single()
            .GetParameters().Single(p => p.Name == "Trades");

        tradesParameter.ParameterType.Should().Be(typeof(IReadOnlyList<StrategyTrade>));
        tradesParameter.ParameterType.IsAssignableFrom(typeof(ResizedTradeSeries)).Should().BeFalse();
        tradesParameter.ParameterType.IsAssignableFrom(typeof(BacktestNetSeries)).Should().BeFalse();
    }

    // ---- helpers ----

    private static BacktestNetSeries BuildOrThrow(IReadOnlyList<BacktestTrade> source, ResizedTradeSeries resized)
    {
        var result = BacktestNetSeries.Bridge.Build(
            source, resized, StrategyId, "M", null, BacktestSegment.InSampleTest, memberWeight: 1m);
        result.Status.Should().Be(BacktestNetSeriesStatus.Built);
        return result.Series!;
    }

    /// <summary>Three rows, sizes 1.00, profits -200 / +50 / +150, on three consecutive days.</summary>
    private static List<BacktestTrade> HandBuiltSource() =>
    [
        Trade(rowIndex: 0, profit: -200m, size: 1m, day: 1),
        Trade(rowIndex: 1, profit: 50m, size: 1m, day: 2),
        Trade(rowIndex: 2, profit: 150m, size: 1m, day: 3),
    ];

    internal static BacktestTrade Trade(int rowIndex, decimal profit, decimal size, int day)
        => new()
        {
            Id = Guid.NewGuid(),
            BacktestRunId = Guid.Empty,
            RowIndex = rowIndex,
            Ticket = 1000 + rowIndex,
            Symbol = "XAUUSD",
            Type = "Buy",
            OpenTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1),
            OpenPrice = 1000m,
            Size = size,
            CloseTime = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(day - 1),
            ClosePrice = 1010m,
            Profit = profit,
            Balance = 100_000m,
            SampleTypeRaw = "IST",
            Segment = BacktestSegment.InSampleTest,
            CloseType = profit < 0 ? "SL" : "PT",
            RealizedRisk = profit < 0 ? 200m : null,
            CreatedAt = DateTime.UtcNow,
        };

    private static ResizedTrade Row(
        int rowIndex, decimal original, decimal resized, ResizeOutcome outcome = ResizeOutcome.OnTarget)
        => new(rowIndex, 1000 + rowIndex, original, resized, TradeRiskInterval.Unknown, outcome, RiskBasis.Measured);

    /// <summary>
    /// A hand-built series over every source row at a uniform size ratio. Hand-built on purpose:
    /// these tests are about the BRIDGE's pairing and weight contracts, not about the resizer.
    /// </summary>
    private static ResizedTradeSeries HandBuiltResized(IReadOnlyList<BacktestTrade> source, decimal scale)
        => new(
            TargetRiskPerTrade: 200m * scale,
            Grid: LotGrid.ImoxRetester,
            Trades: source.Select(t => Row(t.RowIndex, t.Size, t.Size * scale)).ToList(),
            OnTargetCount: source.Count,
            RaisedToMinimumCount: 0,
            CappedAtMaximumCount: 0,
            MaxAchievedRisk: null,
            UnknownAchievedRiskCount: source.Count,
            UnscalableCount: 0);
}
