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

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(11), trades, Jerusalem, Berlin);

        tradingDays.Should().Be(7);
        calendarDays.Should().Be(10);
    }

    [Fact]
    public void ElapsedCalendarDays_IsAPlainDateDifference()
    {
        var trades = new[] { Trade(0, D(1), D(1), net: 100m) };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(46), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, _) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(46), trades, Jerusalem, Berlin);

        calendarDays.Should().Be(45);
    }

    [Fact]
    public void BreachOnTheAnchorDay_GivesZeroCalendarDaysAndOneTradingDay()
    {
        var trades = new[] { Trade(0, D(1), D(1), net: 100m) };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(1), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(1), trades, Jerusalem, Berlin);

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

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(6), trades, Jerusalem, Berlin);

        calendarDays.Should().Be(5);
        tradingDays.Should().Be(3); // days 1, 2, 6 — not days 3, 4, 5
    }

    /// <summary>
    /// ftmo-challenge-race PR1 (hard rule 3 — the ONE permitted existing-assertion edit): trade 0
    /// opens day 1 (the anchor day) and closes day 3; trade 1 opens AND closes day 3 (the breach).
    /// Counting by OPEN (not close, D9) now attributes a trading day to day 1 (trade 0's open) AND
    /// day 3 (trade 1's open): 1 becomes 2.
    /// </summary>
    [Fact]
    public void AnchorDayItselfHasNoReplayedClose_IsNotCountedInFtmoTradingDaysElapsed()
    {
        var trades = new[]
        {
            Trade(0, D(1), D(3), net: 100m),
            Trade(1, D(3), D(3), net: -10m), // breach close, same day as the only close in the set
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(3), Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(3), trades, Jerusalem, Berlin);

        anchor.FtmoDay.Should().Be(FtmoDayClock.Attribute(D(1), Jerusalem, Berlin).BookkeepingDay);
        calendarDays.Should().Be(2);
        tradingDays.Should().Be(2); // day 1 (trade 0's open) AND day 3 (trade 1's open)
    }

    // =====================================================================
    // ftmo-challenge-race PR1, Phase 1.1 — the cutoff-rule correction (design.md Decision 5;
    // spec.md's MODIFIED "Elapsed Time Is Measured From The Replay-Start Anchor" requirement).
    // =====================================================================

    [Fact]
    public void ElapsedFtmoTradingDays_CountsDistinctDaysWithAScalablePositionOpened_NotClosed()
    {
        // 6 distinct open days (1-6, each trade closing same day as it opens), plus 2 further trades
        // that REUSE an already-counted open day (1 and 2) but close on two brand-new days (7 and 8).
        // Old (by-close) would count 8 distinct days (1-6 plus 7,8); by-open counts only 6.
        var trades = new List<ProjectedTrade>();
        for (var day = 1; day <= 6; day++)
            trades.Add(Trade(day, D(day), D(day), net: 10m));
        trades.Add(Trade(7, D(1, 9), D(7), net: 5m)); // reuses open day 1, closes new day 7
        trades.Add(Trade(8, D(2, 9), D(8), net: -50m)); // reuses open day 2, closes new day 8 (breach)

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(8), Jerusalem, Berlin).BookkeepingDay;
        var cutoff = D(8);

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, cutoff, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(6, "FTMO trading days are counted by OPEN, not by close");
    }

    /// <summary>
    /// The falsification-bearing cutoff-rule test (hard rule 5): a position opened AFTER the breach's
    /// own close instant, on the same FTMO day as the breach, must not be counted even though its day
    /// is inside the elapsed window.
    /// </summary>
    [Fact]
    public void ElapsedFtmoTradingDays_APositionOpenedLaterOnTheBreachDayAfterTheBreachClose_IsNotCounted()
    {
        var breachClose = D(5, 10);
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: 100m), // anchor day
            Trade(1, D(3), breachClose, net: -900m), // the breaching trade itself; opens day 3
            Trade(2, D(5, 11), D(5, 12), net: 10m), // opens strictly AFTER the breach close, same day
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(breachClose, Jerusalem, Berlin).BookkeepingDay;

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachClose, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(2, "only days 1 and 3 have an open before the cutoff; day 5's only open is after it");
    }

    [Fact]
    public void ElapsedFtmoTradingDays_ADayContainingOnlyACloseNoOpen_IsNotCounted()
    {
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: 100m), // anchor day, opens and closes day 1
            Trade(1, D(1, 9), D(3), net: -10m), // opens day 1 (already counted) but closes day 3
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachClose = D(3);
        var breachDay = FtmoDayClock.Attribute(breachClose, Jerusalem, Berlin).BookkeepingDay;

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachClose, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(1, "day 3 has only a close (no open) and must not be counted");
    }

    [Fact]
    public void ElapsedFtmoTradingDays_AnUnscalableOpen_DoesNotCountAsATradingDay()
    {
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: 100m), // anchor day, scalable
            Trade(1, D(2), D(2), net: null), // Unscalable — the ONLY open on day 2
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachClose = D(2);
        var breachDay = FtmoDayClock.Attribute(breachClose, Jerusalem, Berlin).BookkeepingDay;

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachClose, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(1, "an Unscalable open never actually opened on FTMO");
    }

    /// <summary>
    /// Guards against a no-op "fix" (hard rule 5): the shipped (pre-PR1) count-by-close implementation
    /// over the SAME series as the first scenario above produces a DIFFERENT number (8) than the
    /// by-open count (6), proving the two definitions are observably distinct.
    /// </summary>
    [Fact]
    public void ElapsedFtmoTradingDays_AFlatCountByCloseImplementation_WouldFailThisRequirement()
    {
        var trades = new List<ProjectedTrade>();
        for (var day = 1; day <= 6; day++)
            trades.Add(Trade(day, D(day), D(day), net: 10m));
        trades.Add(Trade(7, D(1, 9), D(7), net: 5m));
        trades.Add(Trade(8, D(2, 9), D(8), net: -50m));

        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(D(8), Jerusalem, Berlin).BookkeepingDay;

        var (_, byOpenTradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, D(8), trades, Jerusalem, Berlin);

        var byCloseDays = trades
            .Select(t => FtmoDayClock.Attribute(t.CloseSource, Jerusalem, Berlin).BookkeepingDay)
            .Where(d => d >= anchor.FtmoDay && d <= breachDay)
            .Distinct()
            .Count();

        byCloseDays.Should().Be(8);
        byOpenTradingDays.Should().Be(6);
        byOpenTradingDays.Should().NotBe(byCloseDays, "counting by open must be observably distinct from counting by close");
    }

    [Fact]
    public void ElapsedFtmoTradingDays_CalendarDaysElapsedIsUnaffectedByTheTradingDayCorrection()
    {
        var trades = new[] { Trade(0, D(1), D(1), net: 100m) };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachClose = D(46);
        var breachDay = FtmoDayClock.Attribute(breachClose, Jerusalem, Berlin).BookkeepingDay;

        var (calendarDays, _) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachClose, trades, Jerusalem, Berlin);

        calendarDays.Should().Be(45, "calendar days is a plain date difference, unaffected by the trading-day correction");
    }

    // =====================================================================
    // Post-PR1 correction (RELIABILITY-001, RELIABILITY-002) — pins the second cutoff-rule disjunct
    // (`trade.CloseSource <= eventSourceClose`) and the anchor-day-with-only-Unscalable-open case.
    // =====================================================================

    /// <summary>
    /// RELIABILITY-001: a zero-duration trade (<c>Open == Close == c</c>) whose close IS the breach
    /// event must still count its day. <c>Open &lt; c</c> is false here (they're equal), so only the
    /// <c>Close &lt;= c</c> disjunct saves it — this is not dead code. Measured on real data: 38 of
    /// 30,266 backtest trades have <c>OpenTime == CloseTime</c>, including stop-loss closes of about
    /// -US$200, exactly the kind of close that can breach a daily limit.
    /// </summary>
    [Fact]
    public void ElapsedFtmoTradingDays_ZeroDurationBreachTrade_CountsItsOwnDay()
    {
        var breachInstant = D(5);
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: 100m), // anchor day
            Trade(1, breachInstant, breachInstant, net: -900m), // zero-duration: Open == Close == c
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        var breachDay = FtmoDayClock.Attribute(breachInstant, Jerusalem, Berlin).BookkeepingDay;

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachInstant, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(2, "the zero-duration breach trade's own day (day 5) must count, alongside the anchor day (day 1)");
    }

    /// <summary>
    /// RELIABILITY-002: the anchor day is built from the earliest OPEN across ALL rows, Unscalable
    /// included (see <see cref="FtmoReplayCalendar"/> class doc) — so the anchor can sit on a day whose
    /// only open is Unscalable. That day must still contribute 0 to <c>FtmoTradingDaysElapsed</c>,
    /// since an Unscalable trade never actually opened on FTMO.
    /// </summary>
    [Fact]
    public void ElapsedFtmoTradingDays_AnchorDayWithOnlyAnUnscalableOpen_ContributesZeroTradingDays()
    {
        var trades = new[]
        {
            Trade(0, D(1), D(1), net: null), // anchor day — the ONLY open here is Unscalable
            Trade(1, D(2), D(2), net: 10m), // the only scalable open
        };
        var anchor = FtmoReplayCalendar.Build(trades, Jerusalem, Berlin);
        anchor.FtmoDay.Should().Be(FtmoDayClock.Attribute(D(1), Jerusalem, Berlin).BookkeepingDay,
            "the anchor is built from the earliest open across ALL rows, Unscalable included");
        var breachClose = D(2);
        var breachDay = FtmoDayClock.Attribute(breachClose, Jerusalem, Berlin).BookkeepingDay;

        var (_, tradingDays) = FtmoReplayCalendar.ElapsedDays(anchor, breachDay, breachClose, trades, Jerusalem, Berlin);

        tradingDays.Should().Be(1, "the anchor day's only open is Unscalable and contributes 0; only day 2 counts");
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
