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
    /// <c>CalendarDaysElapsed</c> = a plain date difference. <c>FtmoTradingDaysElapsed</c> = the
    /// number of distinct bookkeeping days, among ALL replayed closes (Unscalable included), with
    /// <c>anchor.FtmoDay &lt;= d &lt;= breachDay</c>. A breach on the anchor day gives <c>0</c> / <c>1</c>.
    /// If the anchor day itself has no replayed close, it is not counted.
    /// </summary>
    internal static (int CalendarDaysElapsed, int FtmoTradingDaysElapsed) ElapsedDays(
        ReplayAnchor anchor,
        DateOnly breachDay,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        var calendarDays = breachDay.DayNumber - anchor.FtmoDay.DayNumber;

        var closeDays = new HashSet<DateOnly>();
        foreach (var trade in trades)
        {
            var day = FtmoDayClock.Attribute(trade.CloseSource, sourceZone, berlinZone).BookkeepingDay;
            if (day >= anchor.FtmoDay && day <= breachDay)
                closeDays.Add(day);
        }

        return (calendarDays, closeDays.Count);
    }
}
