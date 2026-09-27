namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-first-breach-timing (design.md Decision 4, 5) — the replay-start anchor and elapsed-day
/// arithmetic. <c>internal static</c>, pure: no I/O.
/// <para>
/// The anchor is the earliest <c>(OpenSource, RowIndex)</c> across ALL rows, Unscalable rows
/// included: open and close times do not depend on FX, so the anchor and the close-day set are
/// identical at both FX-band ends. An ambiguous or invalid open resolves to
/// <see cref="FtmoDayClock.DayAttribution.BookkeepingDay"/> (the earliest candidate day), matching
/// the evaluator's own conservative rule (design.md Decision 2).
/// </para>
/// <para>
/// <see cref="ElapsedDays"/> counts DISTINCT bookkeeping days among ALL replayed closes (Unscalable
/// included) with <c>anchor &lt;= d &lt;= breachDay</c>, as a post-replay count rather than an
/// in-loop running counter. A distinct-day count is used because it stays correct regardless of
/// close order, including if bookkeeping days were ever non-monotonic — but for this zone pair
/// (<c>Asia/Jerusalem</c> source to UTC to <c>Europe/Berlin</c>), instant order is preserved, so a
/// non-monotonic day sequence is not reachable: a brute-force scan of 2005-2030 found 0 of 1560
/// ambiguous and 0 of 1560 invalid source minutes whose DST-transition candidates fall on different
/// Berlin days. The distinct-day count is therefore defensive here, not exercised by any reachable
/// case (design.md Decision 5). No holiday calendar is consulted; there is no such dependency to call.
/// </para>
/// </summary>
internal static class FtmoReplayCalendar
{
    /// <summary>The replay-start anchor: the first replayed trade's OPEN source instant and FTMO trading day.</summary>
    internal readonly record struct ReplayAnchor(DateTime SourceOpen, DateOnly FtmoDay);

    /// <summary>
    /// Builds the anchor from <paramref name="trades"/>. Ties on the identical earliest
    /// <c>OpenSource</c> are broken by the lower <c>RowIndex</c>, deterministically.
    /// </summary>
    internal static ReplayAnchor Build(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades, TimeZoneInfo sourceZone, TimeZoneInfo berlinZone)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        var earliest = trades
            .OrderBy(t => t.OpenSource)
            .ThenBy(t => t.RowIndex)
            .First();

        var attribution = FtmoDayClock.Attribute(earliest.OpenSource, sourceZone, berlinZone);
        return new ReplayAnchor(earliest.OpenSource, attribution.BookkeepingDay);
    }

    /// <summary>
    /// <c>CalendarDaysElapsed</c> = a plain date difference, untouched by the trading-day correction.
    /// <c>FtmoTradingDaysElapsed</c> (ftmo-challenge-race D9, design.md Decision 5) = the number of
    /// distinct bookkeeping days, attributed to a scalable trade's OWN OPEN, among ALL scalable
    /// (non-<c>Unscalable</c>) trades satisfying the cutoff rule at <paramref name="eventSourceClose"/>
    /// (<c>c</c>): <c>Open &lt; c ∨ Close &lt;= c</c>, whose attributed day falls within
    /// <c>[anchor.FtmoDay, breachDay]</c>. FTMO defines a trading day as a day with at least one
    /// position OPENED (<c>SERVICE_FTMO.md:158</c>), not closed — a day containing only a close does
    /// not count, and an <c>Unscalable</c> trade (never actually opened on FTMO) is excluded entirely,
    /// even when it is the only open on the anchor day (the anchor still sits on that day; the day
    /// itself simply contributes <c>0</c> to the count).
    /// <para>
    /// A trade whose own open is STRICTLY after <c>c</c> is never counted, even on the breach day
    /// itself: the cutoff is a point in time, not a day boundary, so an open coming after the account
    /// died cannot retroactively meet the minimum-days requirement. The second disjunct,
    /// <c>Close &lt;= c</c>, is NOT redundant with the first: it exists for the zero-duration case
    /// (<c>Open == Close</c>). When a zero-duration trade's single instant coincides exactly with
    /// <c>c</c> (most notably the breaching trade itself, whose close IS the event), <c>Open &lt; c</c>
    /// is false — the two are equal, not strictly ordered — so only <c>Close &lt;= c</c> saves it,
    /// correctly counting the breach trade's own day. This is a reachable case, not dead code:
    /// measured on real data, 38 of 30,266 backtest trades have <c>OpenTime == CloseTime</c>, including
    /// stop-loss closes of about -US$200 — exactly the kind of close that can breach a daily limit.
    /// The same disjunct also admits a zero-duration trade at exactly <c>c</c> that is NOT itself the
    /// breach trade; this is intentional, not a gap — <c>c</c> is treated as an inclusive boundary
    /// instant (a position opened at, not after, the moment the account breaches still counts),
    /// matching the evaluator's own conservative tie-handling at boundary instants (see the class doc
    /// above). A breach on the anchor day gives <c>0</c> / <c>1</c>. If the anchor day itself has no
    /// OPEN satisfying the cutoff rule, it is not counted.
    /// </para>
    /// </summary>
    internal static (int CalendarDaysElapsed, int FtmoTradingDaysElapsed) ElapsedDays(
        ReplayAnchor anchor,
        DateOnly breachDay,
        DateTime eventSourceClose,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        var calendarDays = breachDay.DayNumber - anchor.FtmoDay.DayNumber;

        var openDays = new HashSet<DateOnly>();
        foreach (var trade in trades)
        {
            if (trade.Net is null)
                continue; // Unscalable — never actually opened on FTMO.

            if (!(trade.OpenSource < eventSourceClose || trade.CloseSource <= eventSourceClose))
                continue; // Opened at or after the cutoff — cannot count toward this event.

            var day = FtmoDayClock.Attribute(trade.OpenSource, sourceZone, berlinZone).BookkeepingDay;
            if (day >= anchor.FtmoDay && day <= breachDay)
                openDays.Add(day);
        }

        return (calendarDays, openDays.Count);
    }
}
