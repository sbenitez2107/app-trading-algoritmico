using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>One calendar day on the dense (zero-filled) daily net-P/L series.</summary>
/// <param name="Day">The calendar day (date only).</param>
/// <param name="Net">Net P/L realized that day (sum of closed-trade nets; 0 on no-trade days).</param>
/// <param name="EquityStart">Running decimal equity at the START of the day (baseline + all prior nets).</param>
internal readonly record struct DailyNetPoint(DateTime Day, decimal Net, decimal EquityStart);

/// <summary>
/// Shared, stateless analytics primitives reused by both <see cref="StrategyAnalyticsCalculator"/>
/// and <see cref="PortfolioAnalyticsCalculator"/>. The financial math lives here ONCE so a portfolio
/// of weighted strategy nets is measured with the exact same logic as a single strategy — there is
/// no second copy of the formulas to drift. Inputs are plain (date, net) / (open, close) sequences,
/// so weighted portfolio streams flow through unchanged.
/// </summary>
internal static class AnalyticsSeries
{
    private const int TradingDaysPerYear = 252;

    /// <summary>Per-trade net P/L: gross profit plus all costs (commission, swap, taxes are signed).</summary>
    internal static decimal NetOf(StrategyTrade t) => t.Profit + t.Commission + t.Swap + t.Taxes;

    // -------------------------------------------------------------------------
    // Dense daily series
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a DENSE daily net-P/L series from dated net events: one <see cref="DailyNetPoint"/>
    /// per calendar day between the first and last event date (inclusive), zero-profit days filled
    /// in. <see cref="DailyNetPoint.EquityStart"/> is the running equity at the start of each day,
    /// starting from <paramref name="initialBalance"/>.
    /// </summary>
    public static IReadOnlyList<DailyNetPoint> BuildDailyNetSeries(
        decimal initialBalance,
        IReadOnlyList<(DateTime When, decimal Net)> events)
    {
        if (events.Count == 0) return Array.Empty<DailyNetPoint>();

        var byDay = events
            .GroupBy(e => e.When.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var first = byDay[0].Key;
        var last = byDay[^1].Key;
        var totalDays = (int)(last - first).TotalDays + 1;

        var profitByDay = byDay.ToDictionary(g => g.Key, g => g.Sum(e => e.Net));

        var series = new List<DailyNetPoint>(totalDays);
        var equity = initialBalance;
        for (var i = 0; i < totalDays; i++)
        {
            var day = first.AddDays(i);
            var net = profitByDay.TryGetValue(day, out var p) ? p : 0m;
            series.Add(new DailyNetPoint(day, net, equity));
            equity += net;
        }

        return series;
    }

    /// <summary>Convenience overload bucketing closed trades by <c>CloseTime ?? OpenTime</c> date.</summary>
    public static IReadOnlyList<DailyNetPoint> BuildDailyNetSeries(
        decimal initialBalance,
        IReadOnlyList<StrategyTrade> chronological)
        => BuildDailyNetSeries(
            initialBalance,
            chronological.Select(t => (t.CloseTime ?? t.OpenTime, NetOf(t))).ToList());

    /// <summary>
    /// Annualised Sharpe ratio over the dense daily-return series (days with no trades contribute a
    /// zero return). The denominator for each day's return is the equity at the start of that day
    /// (holding-period convention). Kept in double precision so the value is bit-stable, then
    /// annualised with sqrt(252). Returns 0 for fewer than 2 events or non-positive baseline.
    /// </summary>
    public static decimal ComputeSharpe(decimal initialBalance, IReadOnlyList<(DateTime When, decimal Net)> events)
    {
        if (events.Count < 2 || initialBalance <= 0) return 0m;

        var series = BuildDailyNetSeries(initialBalance, events);
        if (series.Count < 2) return 0m;

        var dailyReturns = new List<double>(series.Count);
        var equity = (double)initialBalance;
        foreach (var pt in series)
        {
            var profit = (double)pt.Net;
            var ret = equity > 0 ? profit / equity : 0.0;
            dailyReturns.Add(ret);
            equity += profit;
        }

        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / (dailyReturns.Count - 1);
        var std = Math.Sqrt(variance);
        if (std == 0) return 0m;

        return (decimal)(mean / std * Math.Sqrt(TradingDaysPerYear));
    }

    // -------------------------------------------------------------------------
    // Per-trade net aggregates
    // -------------------------------------------------------------------------

    public static decimal StandardDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0m;
        var avg = values.Average();
        var sumSq = values.Sum(v => (v - avg) * (v - avg));
        return (decimal)Math.Sqrt((double)sumSq / (values.Count - 1));
    }

    public static (int maxWin, int maxLoss, decimal avgWin, decimal avgLoss)
        ComputeStreaks(IReadOnlyList<decimal> nets)
    {
        if (nets.Count == 0) return (0, 0, 0m, 0m);

        var winRuns = new List<int>();
        var lossRuns = new List<int>();
        var currentWin = 0;
        var currentLoss = 0;

        foreach (var n in nets)
        {
            if (n > 0)
            {
                currentWin++;
                if (currentLoss > 0) { lossRuns.Add(currentLoss); currentLoss = 0; }
            }
            else if (n < 0)
            {
                currentLoss++;
                if (currentWin > 0) { winRuns.Add(currentWin); currentWin = 0; }
            }
            else
            {
                // Breakeven breaks both streaks.
                if (currentWin > 0) { winRuns.Add(currentWin); currentWin = 0; }
                if (currentLoss > 0) { lossRuns.Add(currentLoss); currentLoss = 0; }
            }
        }
        if (currentWin > 0) winRuns.Add(currentWin);
        if (currentLoss > 0) lossRuns.Add(currentLoss);

        var maxWin = winRuns.Count > 0 ? winRuns.Max() : 0;
        var maxLoss = lossRuns.Count > 0 ? lossRuns.Max() : 0;
        var avgWin = winRuns.Count > 0 ? (decimal)winRuns.Average() : 0m;
        var avgLoss = lossRuns.Count > 0 ? (decimal)lossRuns.Average() : 0m;
        return (maxWin, maxLoss, avgWin, avgLoss);
    }

    public static decimal ComputeSqn(IReadOnlyList<decimal> nets)
    {
        if (nets.Count < 2) return 0m;
        var mean = nets.Average();
        var std = StandardDeviation(nets);
        if (std == 0) return 0m;
        return mean / std * (decimal)Math.Sqrt(nets.Count);
    }

    public static (decimal zScore, decimal zProbability) ComputeZScore(IReadOnlyList<decimal> nets)
    {
        // Standard formula for runs of wins/losses.
        // Z = (N*(R - 0.5) - X) / sqrt(X*(X-N) / (N-1))   where X = 2*W*L/N
        var n = nets.Count(v => v != 0); // Excludes breakeven trades.
        if (n < 2) return (0m, 0m);

        var w = nets.Count(v => v > 0);
        var l = nets.Count(v => v < 0);
        if (w == 0 || l == 0) return (0m, 0m);

        var runs = 1;
        var prevSign = 0;
        foreach (var net in nets)
        {
            if (net == 0) continue;
            var sign = net > 0 ? 1 : -1;
            if (prevSign != 0 && sign != prevSign) runs++;
            prevSign = sign;
        }

        var x = 2.0 * w * l / n;
        var denomInside = x * (x - n) / (n - 1);
        if (denomInside <= 0) return (0m, 0m);
        var denom = Math.Sqrt(denomInside);
        if (denom == 0) return (0m, 0m);

        var z = (n * (runs - 0.5) - x) / denom;
        // Two-tailed normal CDF approximation: P = erf(|z| / sqrt(2)).
        var probability = Erf(Math.Abs(z) / Math.Sqrt(2));
        return ((decimal)z, (decimal)probability);
    }

    /// <summary>Abramowitz &amp; Stegun 7.1.26 approximation of the error function.</summary>
    public static double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return sign * y;
    }

    // -------------------------------------------------------------------------
    // Equity / drawdown / exposure (chronological)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks a chronological (date, net) sequence from <paramref name="initialBalance"/> and returns
    /// max drawdown (amount + % against the running peak), longest stagnation in days, and final equity.
    /// </summary>
    public static (decimal maxDdAmount, decimal maxDdPercent, int stagnationDays, decimal finalEquity)
        ComputeEquityStats(decimal initialBalance, IReadOnlyList<(DateTime When, decimal Net)> chronological)
    {
        var equity = initialBalance;
        var peak = initialBalance;
        var peakAt = chronological.Count > 0 ? chronological[0].When : DateTime.UtcNow;

        var maxDdAmount = 0m;
        var maxDdPercent = 0m;
        var maxStagnation = 0;

        foreach (var (when, net) in chronological)
        {
            equity += net;

            if (equity > peak)
            {
                var stagnation = (int)(when - peakAt).TotalDays;
                if (stagnation > maxStagnation) maxStagnation = stagnation;

                peak = equity;
                peakAt = when;
            }
            else
            {
                var dd = peak - equity;
                if (dd > maxDdAmount) maxDdAmount = dd;
                var ddPct = peak > 0 ? dd / peak : 0m;
                if (ddPct > maxDdPercent) maxDdPercent = ddPct;
            }
        }

        // Trailing stagnation: if the last point did not push to a new peak, the gap between
        // the last peak and the most recent point also counts.
        if (chronological.Count > 0)
        {
            var lastAt = chronological[^1].When;
            var trailing = (int)(lastAt - peakAt).TotalDays;
            if (trailing > maxStagnation) maxStagnation = trailing;
        }

        return (maxDdAmount, maxDdPercent, maxStagnation, equity);
    }

    /// <summary>
    /// Fraction of wall-clock time at least one position was open. Overlapping intervals merge
    /// (concurrent trades count once). Denominator is earliest-open to latest-close.
    /// </summary>
    public static decimal ComputeExposure(IReadOnlyList<(DateTime Open, DateTime Close)> intervals)
    {
        if (intervals.Count == 0) return 0m;

        var ordered = intervals.OrderBy(i => i.Open).ToList();

        var earliestOpen = ordered[0].Open;
        var latestClose = ordered.Max(i => i.Close);
        var totalSpan = (latestClose - earliestOpen).TotalSeconds;
        if (totalSpan <= 0) return 0m;

        var mergedSeconds = 0.0;
        var currentStart = ordered[0].Open;
        var currentEnd = ordered[0].Close;

        foreach (var (open, close) in ordered.Skip(1))
        {
            if (open <= currentEnd)
            {
                if (close > currentEnd) currentEnd = close;
            }
            else
            {
                mergedSeconds += (currentEnd - currentStart).TotalSeconds;
                currentStart = open;
                currentEnd = close;
            }
        }
        mergedSeconds += (currentEnd - currentStart).TotalSeconds;

        return (decimal)(mergedSeconds / totalSpan);
    }
}
