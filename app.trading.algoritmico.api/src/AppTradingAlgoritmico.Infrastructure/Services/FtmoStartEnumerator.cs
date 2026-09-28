namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-multi-start PR2, tasks 2.1 and 2.2 (design.md/proposal.md Decision D1, design.md Decision 4) —
/// enumerates one start per FTMO (Berlin) calendar month from the data (spec.md "Starts Are Enumerated
/// At Monthly Grain From The Data"), and slices a start's own no-leak series (spec.md "A trade still
/// open at a later start's anchor does not leak into that start's replay"). <c>internal static</c>,
/// pure: no I/O.
/// </summary>
internal static class FtmoStartEnumerator
{
    /// <summary>One enumerated start: the first scalable trade opened in its FTMO (Berlin) month.</summary>
    internal readonly record struct Start(DateTime SourceOpen, DateOnly FtmoMonth);

    /// <summary>
    /// Groups every SCALABLE (non-<c>Unscalable</c>) trade by its FTMO (Berlin) calendar month — the
    /// month of <see cref="FtmoDayClock.DayAttribution.BookkeepingDay"/> for its own OPEN instant, never
    /// a naive source-zone month read (spec.md "Month Boundary On Berlin Day"). Per month, the start is
    /// the first trade ordered by <c>(Open, RowIndex)</c>. The range spans every calendar month from the
    /// first month containing a scalable open through the last, inclusive; a month with none is reported
    /// in <c>MonthsWithoutStart</c>, never dropped and never treated as an implicit start.
    /// </summary>
    internal static (IReadOnlyList<Start> Starts, IReadOnlyList<DateOnly> MonthsWithoutStart) Enumerate(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        var scalable = trades
            .Where(t => t.Net is not null) // Unscalable rows never actually opened on FTMO.
            .Select(t => (Trade: t, Month: MonthOf(t.OpenSource, sourceZone, berlinZone)))
            .OrderBy(x => x.Trade.OpenSource)
            .ThenBy(x => x.Trade.RowIndex)
            .ToList();

        if (scalable.Count == 0)
            return (Array.Empty<Start>(), Array.Empty<DateOnly>());

        var firstMonth = scalable.Min(x => x.Month);
        var lastMonth = scalable.Max(x => x.Month);

        // GroupBy preserves first-encountered order within each group; the source is already sorted by
        // (Open, RowIndex), so the group's first element IS the correct tie-break winner (lower RowIndex
        // on a same-instant tie).
        var firstOpenByMonth = scalable
            .GroupBy(x => x.Month)
            .ToDictionary(g => g.Key, g => g.First().Trade.OpenSource);

        var starts = new List<Start>();
        var monthsWithoutStart = new List<DateOnly>();

        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            if (firstOpenByMonth.TryGetValue(month, out var open))
                starts.Add(new Start(open, month));
            else
                monthsWithoutStart.Add(month);
        }

        return (starts, monthsWithoutStart);
    }

    private static DateOnly MonthOf(DateTime sourceOpen, TimeZoneInfo sourceZone, TimeZoneInfo berlinZone)
    {
        var day = FtmoDayClock.Attribute(sourceOpen, sourceZone, berlinZone).BookkeepingDay;
        return new DateOnly(day.Year, day.Month, 1);
    }

    /// <summary>
    /// design.md Decision 4 — a start's series is the projection sorted by <c>(Open, RowIndex)</c>,
    /// sliced from the first position with <c>Open &gt;= startOpen</c>. There is no leak: a trade opened
    /// at an earlier start that is still open (by close time) at this start's anchor has
    /// <c>Open &lt; startOpen</c>, so it is outside this slice — it never affects this start's balance,
    /// overlap flag, flat-book check, anchor, or trading-day count.
    /// </summary>
    internal static IReadOnlyList<FtmoTradeProjector.ProjectedTrade> SliceFromStart(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades, DateTime startOpen)
    {
        ArgumentNullException.ThrowIfNull(trades);

        return trades
            .OrderBy(t => t.OpenSource)
            .ThenBy(t => t.RowIndex)
            .SkipWhile(t => t.OpenSource < startOpen)
            .ToList();
    }
}
