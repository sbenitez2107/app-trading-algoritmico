using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-first-breach-timing Phase 4 — <see cref="FtmoReplayCalendar"/>: the replay-start anchor
/// (design.md Decision 4) and elapsed-day arithmetic (design.md Decision 5). Pure, no I/O.
/// </summary>
public class FtmoReplayCalendarTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static DateTime D(int dayOffset, int hour = 8, int minute = 0) =>
        new DateTime(2026, 1, 1, hour, minute, 0, DateTimeKind.Unspecified).AddDays(dayOffset - 1);

    private static ProjectedTrade Trade(int rowIndex, DateTime open, DateTime close, decimal? net) =>
        new(rowIndex, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, FtmoLots: 1m);

    [Fact]
    public void Build_EarliestOpenAcrossAllRows_IncludingUnscalable_IsTheAnchor()
    {
        var trades = new[]
        {
            Trade(0, D(15, 9), D(15, 10), net: 100m),
            Trade(1, D(10, 8), D(10, 9), net: null), // Unscalable, but its OPEN is earliest
            Trade(2, D(12, 8), D(12, 9), net: 50m),
        };

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);

        anchor.SourceOpen.Should().Be(D(10, 8));
    }

    [Fact]
    public void Build_TiedOpenSourceAcrossTwoRows_TieBrokenByRowIndex()
    {
        var tiedOpen = D(10, 8);
        var trades = new[]
        {
            Trade(2, tiedOpen, D(10, 9), net: 100m),
            Trade(1, tiedOpen, D(11, 9), net: 50m),
        };

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);

        anchor.SourceOpen.Should().Be(tiedOpen);
        // RowIndex 1 (lower) wins the tie — verifiable via the FtmoDay attributed from that same open.
        var expectedAttribution = FtmoDayClock.Attribute(tiedOpen, Jerusalem, Berlin);
        anchor.FtmoDay.Should().Be(expectedAttribution.BookkeepingDay);
    }

    [Fact]
    public void Build_AmbiguousOrInvalidOpenSource_AnchorUsesTheEarliestCandidateDay()
    {
        var ambiguousOpen = new DateTime(2013, 10, 27, 1, 30, 0, DateTimeKind.Unspecified);
        var trades = new[] { Trade(0, ambiguousOpen, ambiguousOpen.AddHours(1), net: 100m) };

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);

        var attribution = FtmoDayClock.Attribute(ambiguousOpen, Jerusalem, Berlin);
        anchor.FtmoDay.Should().Be(attribution.BookkeepingDay);
    }

    [Fact]
    public void ElapsedFtmoTradingDays_CountsOnlyDistinctBookkeepingDaysWithAReplayedClose()
    {
        var trades = new List<ProjectedTrade>
        {
            Trade(0, D(1), D(1), net: 100m), // anchor day
        };
        // 6 more distinct close days between day 1 and day 11 inclusive -> 7 distinct days total.
        int[] closeDays = [2, 3, 4, 5, 6, 11];
        for (var i = 0; i < closeDays.Length; i++)
            trades.Add(Trade(i + 1, D(closeDays[i]), D(closeDays[i]), net: 10m));

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(11), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(7);
        calendarDays.Should().Be(10);
    }

    [Fact]
    public void ElapsedCalendarDays_IsAPlainDateDifference()
    {
        var trades = new[] { Trade(0, D(1), D(1), net: 100m) };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(46), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, _) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, trades, Jerusalem, Berlin);

        calendarDays.Should().Be(45);
    }

    [Fact]
    public void BreachOnTheAnchorDay_GivesZeroCalendarDaysAndOneTradingDay()
    {
        var trades = new[] { Trade(0, D(1), D(1), net: 100m) };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(1), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, trades, Jerusalem, Berlin);

        calendarDays.Should().Be(0);
        tradingDays.Should().Be(1);
    }

    [Fact]
    public void WeekendGapBetweenAnchorAndBreach_StillCountsOnlyDaysWithAReplayedClose()
    {
        // Anchor day 1 (Thursday-equivalent here), a close on day 2, then a multi-day gap (no closes
        // on days 3-5, "the weekend"), then the breach on day 6.
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: 100m),
            Trade(1, D(2), D(2), net: 10m),
            Trade(2, D(6), D(6), net: -10m),
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(6), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, trades, Jerusalem, Berlin);

        calendarDays.Should().Be(5);
        tradingDays.Should().Be(3); // days 1, 2, 6 — not days 3, 4, 5
    }

    [Fact]
    public void AnchorDayItselfHasNoReplayedClose_IsNotCountedInFtmoTradingDaysElapsed()
    {
        // First trade opens day 1 (the anchor day) but closes day 3 — no close lands on day 1 itself.
        var trades = new[]
        {
            Trade(0, D(1), D(3), net: 100m),
            Trade(1, D(3), D(3), net: -10m), // breach close, same day as the only close in the set
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(3), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, trades, Jerusalem, Berlin);

        anchor.FtmoDay.Should().Be(FtmoDayClock.Attribute(D(1), Jerusalem, Berlin).BookkeepingDay);
        calendarDays.Should().Be(2);
        tradingDays.Should().Be(1); // only day 3 has a close; the anchor day (1) contributes none
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void DateTimeKindGuard_NonUnspecifiedOpenTime_Throws(DateTimeKind kind)
    {
        var open = new DateTime(2026, 1, 10, 8, 0, 0, kind);
        var trades = new[] { new ProjectedTrade(0, open, open.AddHours(1), 100m, ResizeOutcome.OnTarget, 1m) };

        var act = () => FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DateTimeKindGuard_UnspecifiedOpenTime_IsAccepted()
    {
        var trades = new[] { Trade(0, D(10), D(10, 9), net: 100m) };

        var act = () => FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);

        act.Should().NotThrow();
    }
}
