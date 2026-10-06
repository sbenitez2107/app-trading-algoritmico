using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D3 — the daily-loss profile of one projected series. <c>internal static</c>, pure: no I/O.
/// <para>
/// It REPLICATES, and never calls, the daily-floor bookkeeping of <c>FtmoBreachEvaluator.Evaluate</c>: closes in
/// <c>(CloseSource, RowIndex)</c> order; a close with a null net is skipped and never starts a day; when the FTMO
/// day changes the reference becomes the balance carried from the previous day (the initial capital on day 1);
/// the balance is then updated and the loss of that close is <c>reference - balance</c>. The evaluator flags a
/// breach strictly below <c>reference - dailyPct * capital</c>, which is exactly <c>loss &gt; dailyPct * capital</c>.
/// </para>
/// <para>
/// The day of a close is supplied by the caller (<paramref name="dayOf"/>), so a search can share one
/// close-instant to bookkeeping-day dictionary across every candidate instead of recomputing the Berlin clock.
/// </para>
/// </summary>
internal static class FtmoDailyLossProfile
{
    /// <param name="WorstDayLoss">Largest <c>reference - balance</c> over every close; <c>0</c> when no close ever loses.</param>
    /// <param name="WorstDay">The first day that reaches <paramref name="WorstDayLoss"/>; null when it is <c>0</c>.</param>
    /// <param name="FirstExceedingDay">The first day whose loss is strictly past the allowance; null when none is.</param>
    internal readonly record struct Profile(decimal WorstDayLoss, DateOnly? WorstDay, DateOnly? FirstExceedingDay);

    internal static Profile Compute(
        IReadOnlyList<ProjectedTrade> series, Func<DateTime, DateOnly> dayOf, decimal initialCapital, decimal dailyPct)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(dayOf);

        var allowance = dailyPct * initialCapital;
        var balance = initialCapital;
        var reference = initialCapital;
        DateOnly? currentDay = null;

        var worstLoss = 0m;
        DateOnly? worstDay = null;
        DateOnly? firstExceeding = null;

        var n = CloseOrder(series);
        for (var k = 0; k < n; k++)
        {
            var trade = series[order![k]];
            var day = dayOf(trade.CloseSource);
            if (currentDay is null || day != currentDay)
            {
                reference = balance;
                currentDay = day;
            }

            balance += trade.Net.Value;

            var loss = reference - balance;
            if (loss > worstLoss)
            {
                worstLoss = loss;
                worstDay = day;
            }

            if (loss > allowance && firstExceeding is null)
                firstExceeding = day;
        }

        return new Profile(worstLoss, worstDay, firstExceeding);
    }

    [ThreadStatic]
    private static long[]? keys;

    [ThreadStatic]
    private static int[]? order;

    /// <summary>
    /// Fills the thread-owned <c>order</c> with the positions of the series' scalable rows by <c>(CloseSource, RowIndex)</c>
    /// and returns how many there are. A null net never starts a day, so those rows are not in it. Primitive keys and
    /// reused buffers: a search profiles every candidate, and a LINQ sort per call was a large part of its garbage.
    /// </summary>
    private static int CloseOrder(IReadOnlyList<ProjectedTrade> series)
    {
        if (keys is null || keys.Length < series.Count)
        {
            keys = new long[Math.Max(series.Count, 256)];
            order = new int[keys.Length];
        }

        var n = 0;
        for (var r = 0; r < series.Count; r++)
        {
            if (series[r].Net is null)
                continue;

            keys[n] = series[r].CloseSource.Ticks;
            order![n] = r;
            n++;
        }

        Array.Sort(keys, order!, 0, n);

        for (var start = 0; start < n;)
        {
            var end = start + 1;
            while (end < n && keys[end] == keys[start])
                end++;

            for (var i = start + 1; i < end; i++)
            {
                var item = order![i];
                var j = i - 1;
                while (j >= start && series[order[j]].RowIndex > series[item].RowIndex)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = item;
            }

            start = end;
        }

        return n;
    }
}
