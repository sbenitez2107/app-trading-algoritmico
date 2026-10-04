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

        foreach (var trade in series.OrderBy(t => t.CloseSource).ThenBy(t => t.RowIndex))
        {
            if (trade.Net is null)
                continue;

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
}
