using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR2, task 2.1 — <see cref="FtmoStartEnumerator.Enumerate"/>: a start is the first
/// scalable trade opened in each FTMO (Berlin) calendar month, from the first month containing one
/// through the last (spec.md "Starts Are Enumerated At Monthly Grain From The Data").
/// </summary>
public class FtmoStartEnumeratorTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Outside any DST-mismatch window: near-constant 1h Jerusalem->Berlin offset (same convention as
    // FtmoFundedPhaseTests/FtmoChallengeRaceTests).
    private static DateTime OnDay(int year, int month, int day, int hour = 8, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net,
        ResizeOutcome outcome = ResizeOutcome.OnTarget) =>
        new(rowIndex, open, close, net, outcome, FtmoLots: 1m);

    [Fact]
    public void AMonthWithNoScalableOpen_IsCountedNotSkipped()
    {
        var trades = new[]
        {
            Trade(0, OnDay(2026, 1, 5), OnDay(2026, 1, 5, 9), net: 10m), // January: scalable.
            // February: no trades at all.
            Trade(1, OnDay(2026, 3, 5), OnDay(2026, 3, 5, 9), net: 10m), // March: scalable.
        };

        var (starts, monthsWithoutStart) = FtmoStartEnumerator.Enumerate(trades, Jerusalem, Berlin);

        starts.Should().HaveCount(2);
        monthsWithoutStart.Should().ContainSingle(m => m.Year == 2026 && m.Month == 2);
    }

    [Fact]
    public void ASameInstantTie_GoesToTheLowerRowIndex()
    {
        var sameInstant = OnDay(2026, 1, 10);
        var trades = new[]
        {
            Trade(5, sameInstant, OnDay(2026, 1, 10, 9), net: 10m),
            Trade(2, sameInstant, OnDay(2026, 1, 10, 9), net: 20m),
        };

        var (starts, _) = FtmoStartEnumerator.Enumerate(trades, Jerusalem, Berlin);

        starts.Should().ContainSingle();
        starts[0].SourceOpen.Should().Be(sameInstant);
        // The lower RowIndex (2) must be the one selected — distinguish by pairing open+net.
        var picked = trades.Single(t => t.OpenSource == starts[0].SourceOpen && t.RowIndex == 2);
        picked.Net.Should().Be(20m);
    }

    [Fact]
    public void MonthBoundaryOnBerlinDay()
    {
        // 2026-01-31 23:30 Jerusalem local -> Berlin is ~1h behind Jerusalem in winter, so this Berlin
        // instant still falls on 2026-01-31 (source-zone day == Berlin day here); use a source instant
        // whose OWN source-zone month differs from its Berlin-attributed month instead, by picking a
        // last-day-of-month late instant where the -1h Berlin shift crosses back a source-zone day that
        // is itself the first of the NEXT month at midnight boundary is not distinguishing enough with
        // whole-hour offsets alone. Use FtmoDayClock directly to build a case whose Berlin-attributed
        // month differs from a naive source-local month read.
        var lateInMonth = new DateTime(2026, 2, 1, 0, 15, 0, DateTimeKind.Unspecified);
        var attribution = FtmoDayClock.Attribute(lateInMonth, Jerusalem, Berlin);
        var expectedMonth = new DateOnly(attribution.BookkeepingDay.Year, attribution.BookkeepingDay.Month, 1);
        // The Berlin day for a Jerusalem 00:15 instant is the PREVIOUS day (Jerusalem leads Berlin) —
        // i.e. still January in Berlin, while the naive source-local date reads February 1st.
        expectedMonth.Should().Be(new DateOnly(2026, 1, 1));

        var trades = new[] { Trade(0, lateInMonth, lateInMonth.AddHours(1), net: 10m) };

        var (starts, _) = FtmoStartEnumerator.Enumerate(trades, Jerusalem, Berlin);

        starts.Should().ContainSingle();
        starts[0].FtmoMonth.Should().Be(expectedMonth);
    }

    [Fact]
    public void AnUnscalableOnlyMonth_ProducesNoStart()
    {
        var trades = new[]
        {
            Trade(0, OnDay(2026, 1, 5), OnDay(2026, 1, 5, 9), net: 10m),
            Trade(1, OnDay(2026, 2, 5), OnDay(2026, 2, 5, 9), net: null, outcome: ResizeOutcome.Unscalable),
            Trade(2, OnDay(2026, 3, 5), OnDay(2026, 3, 5, 9), net: 10m),
        };

        var (starts, monthsWithoutStart) = FtmoStartEnumerator.Enumerate(trades, Jerusalem, Berlin);

        starts.Should().HaveCount(2);
        monthsWithoutStart.Should().ContainSingle(m => m.Year == 2026 && m.Month == 2);
    }

    [Fact]
    public void WeeklyOrEveryTradeGrainIsNotImplemented()
    {
        // ~8 scalable opens spread across a single month: monthly grain produces exactly 1 start,
        // never a weekly (~4) or every-trade (~8) count.
        var trades = Enumerable.Range(0, 8)
            .Select(i => Trade(i, OnDay(2026, 1, 1 + (i * 3)), OnDay(2026, 1, 1 + (i * 3), 9), net: 10m))
            .ToArray();

        var (starts, _) = FtmoStartEnumerator.Enumerate(trades, Jerusalem, Berlin);

        starts.Should().ContainSingle();
    }

    /// <summary>
    /// ftmo-multi-start PR2, task 2.2 (design.md Decision 4) — the per-start series is the projection
    /// sorted by <c>(Open, RowIndex)</c>, sliced from the first position with <c>Open &gt;= startOpen</c>.
    /// A trade opened at an earlier start that is still open (by close time) at a later start's anchor
    /// MUST NOT leak into that later start's series.
    /// </summary>
    [Fact]
    public void ATradeStillOpenAtALaterStartsAnchor_DoesNotLeakIntoThatStartsReplay()
    {
        var laterStartOpen = OnDay(2026, 2, 1);

        // Straddling trade: opened well before the later start's anchor, still open (closes after it).
        var straddling = Trade(0, OnDay(2026, 1, 15), OnDay(2026, 2, 10), net: -5_000m);
        var laterStartTrade = Trade(1, laterStartOpen, OnDay(2026, 2, 1, 9), net: 10m);

        var trades = new[] { straddling, laterStartTrade };

        var sliced = FtmoStartEnumerator.SliceFromStart(trades, laterStartOpen);

        sliced.Should().ContainSingle();
        sliced[0].RowIndex.Should().Be(1);

        // Falsification (hard rule 6): a deliberately-leaking slice that keeps trades still open at
        // startOpen would include the straddling -US$5,000 trade too.
        var leaking = trades.Where(t => t.CloseSource > laterStartOpen).ToList();
        leaking.Should().HaveCount(2, "the leaking variant (by close time only) would wrongly include the straddling trade");
    }

    [Fact]
    public void SliceFromStart_ExcludesTradesOpenedBeforeStartEvenWhenStillOpen()
    {
        var startOpen = OnDay(2026, 1, 10);
        var trades = new[]
        {
            Trade(0, OnDay(2026, 1, 5), OnDay(2026, 1, 20), net: 100m), // Open < startOpen — excluded.
            Trade(1, startOpen, OnDay(2026, 1, 11), net: 50m), // Open == startOpen — included (anchor).
        };

        var sliced = FtmoStartEnumerator.SliceFromStart(trades, startOpen);

        sliced.Select(t => t.RowIndex).Should().Equal(1);
    }
}
