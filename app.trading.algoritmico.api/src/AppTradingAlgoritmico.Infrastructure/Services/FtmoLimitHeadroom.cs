using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D4 — how much of each loss allowance a group used, as fractions (0.8 = 80% of the allowance).
/// <c>internal static</c>, pure, and OUTSIDE the evaluator: no row DTO carries a minimum balance, so the max-loss
/// figure is its own pass over each start's phase boundaries.
/// </summary>
internal static class FtmoLimitHeadroom
{
    /// <summary>One kind's figures, never blended with the other kind's. The ranking uses the WORST max-loss, never the median.</summary>
    internal readonly record struct KindHeadroom(decimal DailyUsed, decimal WorstMaxUsed, decimal MedianMaxUsed)
    {
        /// <summary><c>1 - max(daily used, worst max used)</c>. Can be 0 or negative; callers must not test it for truthiness.</summary>
        internal decimal Headroom => 1m - Math.Max(DailyUsed, WorstMaxUsed);
    }

    /// <summary>
    /// (a) Worst daily loss over the daily allowance <c>dailyPct x capital</c>, at the worse FX end of the merged
    /// window. Start-independent: a day split by a phase boundary can differ per phase, and that is disclosed.
    /// </summary>
    internal static decimal DailyUsed(MergedSeries merged, Func<DateTime, DateOnly> dayOf, GroupParams p)
    {
        var worst = new[] { merged.Low, merged.High }
            .Max(end => FtmoDailyLossProfile.Compute(end, dayOf, p.InitialCapital, p.DailyPct).WorstDayLoss);
        return worst / (p.DailyPct * p.InitialCapital);
    }

    /// <summary>
    /// (b) One start's deepest drawdown over the max allowance, over its phases and both FX ends. P1 is
    /// <c>Open >= StartSourceOpen</c> and <c>Close &lt;= Phase1.OutcomeSourceClose</c>; P2 and funded use the subset rule
    /// <c>Open >= T and Close > T</c> (T = the previous phase's close). Each phase restarts at capital; null nets are skipped.
    /// </summary>
    internal static decimal StartMaxUsed(MergedSeries merged, FtmoMultiStartRowDto start, GroupParams p)
    {
        var p1Close = start.Phase1.OutcomeSourceClose;
        var p2Close = start.Phase2.OutcomeSourceClose;
        var phases = new List<Func<ProjectedTrade, bool>>
        {
            t => t.OpenSource >= (start.Phase1.StartSourceOpen ?? start.StartSourceOpen)
                && (p1Close is null || t.CloseSource <= p1Close),
        };

        if (start.Phase2.Outcome != FtmoPhaseOutcome.NotStarted && p1Close is { } t1)
            phases.Add(t => t.OpenSource >= t1 && t.CloseSource > t1 && (p2Close is null || t.CloseSource <= p2Close));

        if (start.Funded.StartSourceOpen is not null && p2Close is { } t2)
        {
            var fundedClose = start.Funded.OutcomeSourceClose;
            phases.Add(t => t.OpenSource >= t2 && t.CloseSource > t2 && (fundedClose is null || t.CloseSource <= fundedClose));
        }

        return phases
            .SelectMany(inPhase => new[] { merged.Low, merged.High }.Select(end => PhaseUsed(end.Where(inPhase), p)))
            .Max();
    }

    /// <summary>Worst and median of <see cref="StartMaxUsed"/> across starts; an even count averages the two middle values.</summary>
    internal static (decimal Worst, decimal Median) MaxUsed(MergedSeries merged, IReadOnlyList<FtmoMultiStartRowDto> starts, GroupParams p)
    {
        if (starts.Count == 0)
            return (0m, 0m);

        var used = starts.Select(s => StartMaxUsed(merged, s, p)).Order().ToList();
        var mid = used.Count / 2;
        var median = used.Count % 2 == 1 ? used[mid] : (used[mid - 1] + used[mid]) / 2m;
        return (used[^1], median);
    }

    internal static KindHeadroom Compute(MergedSeries merged, IReadOnlyList<FtmoMultiStartRowDto> starts, Func<DateTime, DateOnly> dayOf, GroupParams p)
    {
        var (worst, median) = MaxUsed(merged, starts, p);
        return new KindHeadroom(DailyUsed(merged, dayOf, p), worst, median);
    }

    /// <summary>The rank scalar: the headroom of the WORSE kind (the lower one). Empty input has no headroom to rank.</summary>
    internal static decimal Rank(IEnumerable<KindHeadroom> kinds) => kinds.Min(k => k.Headroom);

    private static decimal PhaseUsed(IEnumerable<ProjectedTrade> rows, GroupParams p)
    {
        var balance = p.InitialCapital;
        var min = balance;
        foreach (var trade in rows.OrderBy(t => t.CloseSource).ThenBy(t => t.RowIndex))
        {
            if (trade.Net is null)
                continue;

            balance += trade.Net.Value;
            min = Math.Min(min, balance);
        }

        return (p.InitialCapital - min) / (p.MaxPct * p.InitialCapital);
    }
}
