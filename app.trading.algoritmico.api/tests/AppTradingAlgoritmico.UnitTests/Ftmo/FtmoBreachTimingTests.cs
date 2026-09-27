using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoBreachEvaluator;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-first-breach-timing Phase 5 — <see cref="FtmoBreachTiming"/>: the FX-band earliest merge
/// (design.md Decision 6), the first-limit-to-break pick (design.md Decision 7), and the DTO mapping
/// (design.md Decision 8). Pure, no I/O.
/// </summary>
public class FtmoBreachTimingTests
{
    private static DateTime Src(int day, int hour = 8) => new(2026, 1, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static BreachPoint Point(
        int rowIndex, DateTime sourceTime, decimal balance = 9_000m, decimal level = 9_500m,
        IReadOnlyList<BreachContingencyCause>? causes = null) =>
        new(sourceTime, sourceTime, balance, level, level - balance, rowIndex, DateOnly.FromDateTime(sourceTime), causes ?? []);

    // ---- Earliest merge across the FX band ----

    [Fact]
    public void Earliest_LowEarlierThanHigh_ReturnsLowTaggedFxLow()
    {
        var low = Point(0, Src(10));
        var high = Point(1, Src(11));

        var result = FtmoBreachTiming.Earliest(low, high);

        result!.Value.Point.Should().Be(low);
        result.Value.End.Should().Be(FtmoFxBandEnd.FxLow);
    }

    [Fact]
    public void Earliest_HighEarlierThanLow_ReturnsHighTaggedFxHigh()
    {
        var low = Point(0, Src(11));
        var high = Point(1, Src(10));

        var result = FtmoBreachTiming.Earliest(low, high);

        result!.Value.Point.Should().Be(high);
        result.Value.End.Should().Be(FtmoFxBandEnd.FxHigh);
    }

    [Fact]
    public void Earliest_SameRowOnBothEnds_ReturnsBothEndsWithFxLowValues()
    {
        var low = Point(3, Src(10), balance: 9_100m);
        var high = Point(3, Src(10), balance: 9_050m); // same row, different balance (lot-grid clamp divergence)

        var result = FtmoBreachTiming.Earliest(low, high);

        result!.Value.End.Should().Be(FtmoFxBandEnd.BothEnds);
        result.Value.Point.Balance.Should().Be(low.Balance, "on a same-row tie the reported values are fxLow's, never fxHigh's or a blend");
    }

    [Fact]
    public void Earliest_OneEndNull_ReturnsTheOtherEndAlone()
    {
        var high = Point(1, Src(10));

        var lowNull = FtmoBreachTiming.Earliest(null, high);
        lowNull!.Value.Point.Should().Be(high);
        lowNull.Value.End.Should().Be(FtmoFxBandEnd.FxHigh);

        var low = Point(0, Src(10));
        var highNull = FtmoBreachTiming.Earliest(low, null);
        highNull!.Value.Point.Should().Be(low);
        highNull.Value.End.Should().Be(FtmoFxBandEnd.FxLow);
    }

    [Fact]
    public void Earliest_BothEndsNull_ReturnsNull()
    {
        FtmoBreachTiming.Earliest(null, null).Should().BeNull();
    }

    [Fact]
    public void Earliest_SameCurrencySymbol_SingleEvaluationRun_NoBandComparisonNeeded()
    {
        // Same-currency callers pass the identical point for both ends (design.md's resolved
        // orchestrator note: the identity band (1,1) makes both ends coincide) — pinned as BothEnds,
        // consistent with the same-row branch above, per the orchestrator's resolution.
        var point = Point(0, Src(10));

        var result = FtmoBreachTiming.Earliest(point, point);

        result!.Value.End.Should().Be(FtmoFxBandEnd.BothEnds);
        result.Value.Point.Should().Be(point);
    }

    // ---- First limit to break ----

    [Fact]
    public void FirstLimit_DailyBreaksBeforeMax_ReturnsDaily()
    {
        var daily = Point(0, Src(10));
        var max = Point(1, Src(11));

        FtmoBreachTiming.FirstLimit(daily, max).Should().Be(FtmoFirstBreachingLimit.Daily);
    }

    [Fact]
    public void FirstLimit_SameRow_ReturnsBothSameClose()
    {
        var daily = Point(5, Src(10));
        var max = Point(5, Src(10));

        FtmoBreachTiming.FirstLimit(daily, max).Should().Be(FtmoFirstBreachingLimit.BothSameClose);
    }

    [Fact]
    public void FirstLimit_SameSourceTimeButDifferentRows_IsNotTreatedAsATie()
    {
        var daily = Point(2, Src(10));
        var max = Point(3, Src(10)); // identical SourceTime, different row

        FtmoBreachTiming.FirstLimit(daily, max).Should().NotBe(FtmoFirstBreachingLimit.BothSameClose);
    }

    [Fact]
    public void FirstLimit_NeitherLimitBreached_ReturnsNull()
    {
        FtmoBreachTiming.FirstLimit(null, null).Should().BeNull();
    }

    // ---- DTO mapping ----

    [Fact]
    public void ToDto_MapsAllNineFieldsFromABreachPointAndCalendar()
    {
        var point = Point(4, Src(10), balance: 9_200m, level: 9_500m, causes: [BreachContingencyCause.UnscalableTradeExcluded]);
        var merged = (Point: point, End: FtmoFxBandEnd.FxHigh);

        var dto = FtmoBreachTiming.ToDto(merged, FtmoTradingDaysElapsed: 3, CalendarDaysElapsed: 5);

        dto.SourceCloseTime.Should().Be(point.SourceTime);
        dto.FtmoTradingDay.Should().Be(point.FtmoDay);
        dto.BalanceAfterClose.Should().Be(point.Balance);
        dto.FloorLevel.Should().Be(point.Level);
        dto.PointClass.Should().Be(FtmoBreachPointClass.Contingent);
        dto.Causes.Should().Equal(BreachContingencyCause.UnscalableTradeExcluded);
        dto.FtmoTradingDaysElapsed.Should().Be(3);
        dto.CalendarDaysElapsed.Should().Be(5);
        dto.FxBandEnd.Should().Be(FtmoFxBandEnd.FxHigh);
    }

    [Fact]
    public void ToDto_CleanPoint_PointClassIsClean()
    {
        var point = Point(4, Src(10), causes: []);
        var merged = (Point: point, End: FtmoFxBandEnd.FxLow);

        var dto = FtmoBreachTiming.ToDto(merged, FtmoTradingDaysElapsed: 1, CalendarDaysElapsed: 0);

        dto.PointClass.Should().Be(FtmoBreachPointClass.Clean);
    }
}
