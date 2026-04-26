using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Pure computation layer for performance KPIs. Stateless — fed already-loaded trades
/// from the persistence layer. Kept separate from the EF service so it can be tested
/// without a database.
/// </summary>
public static class StrategyAnalyticsCalculator
{
    private const int TradingDaysPerYear = 252;
    private const int CalendarDaysPerYear = 365;

    /// <summary>Builds the full <see cref="StrategyAnalyticsDto"/> from raw trades.</summary>
    /// <param name="initialBalance">Starting equity used as the baseline for all percentage calculations.</param>
    /// <param name="trades">All trades (open + closed) for the strategy. Order does not matter — the calculator sorts internally.</param>
    public static StrategyAnalyticsDto Compute(decimal initialBalance, IEnumerable<StrategyTrade> trades)
    {
        var allTrades = trades.ToList();
        var open = allTrades.Where(t => t.IsOpen).ToList();
        var closed = allTrades.Where(t => !t.IsOpen)
            .OrderBy(t => t.CloseTime ?? t.OpenTime)
            .ToList();

        var totalProfit = allTrades.Sum(t => t.Profit);
        var totalCommission = allTrades.Sum(t => t.Commission);
        var totalSwap = allTrades.Sum(t => t.Swap);
        var totalTaxes = allTrades.Sum(t => t.Taxes);
        var netProfit = totalProfit + totalCommission + totalSwap + totalTaxes;

        // Per-trade net (used by every per-trade aggregate downstream).
        var nets = closed.Select(NetOf).ToList();

        var winCount = nets.Count(n => n > 0);
        var lossCount = nets.Count(n => n < 0);
        var breakevenCount = nets.Count(n => n == 0);

        var grossProfit = nets.Where(n => n > 0).Sum();
        var grossLoss = nets.Where(n => n < 0).Sum();

        var averageTrade = nets.Count > 0 ? nets.Average() : 0m;
        var averageWin = winCount > 0 ? nets.Where(n => n > 0).Average() : 0m;
        var averageLoss = lossCount > 0 ? nets.Where(n => n < 0).Average() : 0m;
        var largestWin = nets.Count > 0 ? nets.Max() : 0m;
        var largestLoss = nets.Count > 0 ? nets.Min() : 0m;
        var stdDev = StandardDeviation(nets);

        var winRate = closed.Count > 0 ? (decimal)winCount / closed.Count : 0m;
        var profitFactor = grossLoss != 0 ? grossProfit / Math.Abs(grossLoss) : 0m;
        var payoutRatio = averageLoss != 0 ? averageWin / Math.Abs(averageLoss) : 0m;
        var winsLossesRatio = lossCount > 0 ? (decimal)winCount / lossCount : 0m;
        var expectancy = averageTrade;
        var rExpectancy = averageLoss != 0 ? expectancy / Math.Abs(averageLoss) : 0m;

        // Streaks — single linear pass over closed trades in chronological order.
        var (maxWinStreak, maxLossStreak, avgWinStreak, avgLossStreak) = ComputeStreaks(nets);

        // Equity curve, drawdown, stagnation — also linear, also chronological.
        var (maxDdAmount, maxDdPercent, stagnationDays, finalEquity) = ComputeEquityStats(initialBalance, closed);

        // Returns — derived from equity curve endpoints + time span.
        var firstTradeAt = closed.Count > 0 ? closed[0].CloseTime ?? closed[0].OpenTime : (DateTime?)null;
        var lastTradeAt = closed.Count > 0 ? closed[^1].CloseTime ?? closed[^1].OpenTime : (DateTime?)null;
        var daysSpanned = firstTradeAt is not null && lastTradeAt is not null
            ? Math.Max(1, (int)(lastTradeAt.Value - firstTradeAt.Value).TotalDays)
            : 0;

        var totalReturn = initialBalance > 0 ? netProfit / initialBalance : 0m;

        decimal cagr = 0m;
        if (initialBalance > 0 && daysSpanned > 0 && finalEquity > 0)
        {
            var years = (double)daysSpanned / CalendarDaysPerYear;
            if (years > 0)
            {
                var ratio = (double)(finalEquity / initialBalance);
                cagr = (decimal)(Math.Pow(ratio, 1.0 / years) - 1.0);
            }
        }

        var yearlyAvgProfit = daysSpanned > 0 ? netProfit * CalendarDaysPerYear / daysSpanned : 0m;
        var monthlyAvgProfit = yearlyAvgProfit / 12m;
        var dailyAvgProfit = daysSpanned > 0 ? netProfit / daysSpanned : 0m;

        var ahpr = ComputeAhpr(initialBalance, closed);

        var returnDdRatio = maxDdPercent != 0 ? totalReturn / maxDdPercent : 0m;
        var annualReturnMaxDdRatio = maxDdPercent != 0 ? cagr / maxDdPercent : 0m;

        var sharpe = ComputeSharpe(initialBalance, closed);
        var sqn = ComputeSqn(nets);
        var exposure = ComputeExposure(closed);

        var (zScore, zProbability) = ComputeZScore(nets);

        return new StrategyAnalyticsDto(
            InitialBalance: initialBalance,
            FirstTradeAt: firstTradeAt,
            LastTradeAt: lastTradeAt,
            DaysSpanned: daysSpanned,
            TradeCount: allTrades.Count,
            ClosedCount: closed.Count,
            OpenCount: open.Count,
            WinCount: winCount,
            LossCount: lossCount,
            BreakevenCount: breakevenCount,
            TotalProfit: totalProfit,
            TotalCommission: totalCommission,
            TotalSwap: totalSwap,
            TotalTaxes: totalTaxes,
            NetProfit: netProfit,
            GrossProfit: grossProfit,
            GrossLoss: grossLoss,
            AverageTrade: averageTrade,
            AverageWin: averageWin,
            AverageLoss: averageLoss,
            LargestWin: largestWin,
            LargestLoss: largestLoss,
            StandardDeviation: stdDev,
            WinRate: winRate,
            ProfitFactor: profitFactor,
            PayoutRatio: payoutRatio,
            WinsLossesRatio: winsLossesRatio,
            Expectancy: expectancy,
            RExpectancy: rExpectancy,
            MaxConsecutiveWins: maxWinStreak,
            MaxConsecutiveLosses: maxLossStreak,
            AverageConsecutiveWins: avgWinStreak,
            AverageConsecutiveLosses: avgLossStreak,
            TotalReturn: totalReturn,
            Cagr: cagr,
            YearlyAvgProfit: yearlyAvgProfit,
            MonthlyAvgProfit: monthlyAvgProfit,
            DailyAvgProfit: dailyAvgProfit,
            Ahpr: ahpr,
            MaxDrawdownAmount: maxDdAmount,
            MaxDrawdownPercent: maxDdPercent,
            ReturnDrawdownRatio: returnDdRatio,
            AnnualReturnMaxDdRatio: annualReturnMaxDdRatio,
            StagnationInDays: stagnationDays,
            SharpeRatio: sharpe,
            Sqn: sqn,
            Exposure: exposure,
            ZScore: zScore,
            ZProbability: zProbability);
    }

    /// <summary>
    /// Builds the monthly compounding return series.
    /// Each bucket's `ReturnPercent` is computed against the equity at the start of that
    /// month — so the values naturally compound (if Feb starts at $105k after a +5% Jan,
    /// Feb's % is over $105k, not over the original $100k).
    /// </summary>
    public static IReadOnlyList<MonthlyReturnDto> ComputeMonthlyReturns(
        decimal initialBalance,
        IEnumerable<StrategyTrade> trades)
    {
        var closed = trades.Where(t => !t.IsOpen)
            .OrderBy(t => t.CloseTime ?? t.OpenTime)
            .ToList();

        if (closed.Count == 0) return Array.Empty<MonthlyReturnDto>();

        var groups = closed
            .GroupBy(t =>
            {
                var ts = t.CloseTime ?? t.OpenTime;
                return new { ts.Year, ts.Month };
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .ToList();

        var equity = initialBalance;
        var result = new List<MonthlyReturnDto>(groups.Count);

        foreach (var g in groups)
        {
            var profit = g.Sum(NetOf);
            var equityStart = equity;
            var equityEnd = equityStart + profit;
            var pct = equityStart != 0 ? profit / equityStart : 0m;

            result.Add(new MonthlyReturnDto(
                Year: g.Key.Year,
                Month: g.Key.Month,
                EquityStart: equityStart,
                EquityEnd: equityEnd,
                Profit: profit,
                ReturnPercent: pct,
                TradeCount: g.Count()));

            equity = equityEnd;
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static decimal NetOf(StrategyTrade t) =>
        t.Profit + t.Commission + t.Swap + t.Taxes;

    private static decimal StandardDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0m;
        var avg = values.Average();
        var sumSq = values.Sum(v => (v - avg) * (v - avg));
        return (decimal)Math.Sqrt((double)sumSq / (values.Count - 1));
    }

    private static (int maxWin, int maxLoss, decimal avgWin, decimal avgLoss)
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

    private static (decimal maxDdAmount, decimal maxDdPercent, int stagnationDays, decimal finalEquity)
        ComputeEquityStats(decimal initialBalance, IReadOnlyList<StrategyTrade> chronological)
    {
        var equity = initialBalance;
        var peak = initialBalance;
        var peakAt = chronological.Count > 0
            ? chronological[0].CloseTime ?? chronological[0].OpenTime
            : DateTime.UtcNow;

        var maxDdAmount = 0m;
        var maxDdPercent = 0m;
        var maxStagnation = 0;

        foreach (var t in chronological)
        {
            equity += NetOf(t);
            var when = t.CloseTime ?? t.OpenTime;

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

        // Trailing stagnation: if the last trade did not push to a new peak, the gap
        // between the last peak and the most recent trade also counts.
        if (chronological.Count > 0)
        {
            var lastTradeAt = chronological[^1].CloseTime ?? chronological[^1].OpenTime;
            var trailing = (int)(lastTradeAt - peakAt).TotalDays;
            if (trailing > maxStagnation) maxStagnation = trailing;
        }

        return (maxDdAmount, maxDdPercent, maxStagnation, equity);
    }

    private static decimal ComputeAhpr(decimal initialBalance, IReadOnlyList<StrategyTrade> chronological)
    {
        if (chronological.Count == 0 || initialBalance <= 0) return 0m;

        var equity = initialBalance;
        var holdingReturns = new List<decimal>(chronological.Count);
        foreach (var t in chronological)
        {
            var ret = NetOf(t) / equity;
            holdingReturns.Add(ret);
            equity += NetOf(t);
        }

        return holdingReturns.Count > 0 ? holdingReturns.Average() : 0m;
    }

    private static decimal ComputeSharpe(decimal initialBalance, IReadOnlyList<StrategyTrade> chronological)
    {
        if (chronological.Count < 2 || initialBalance <= 0) return 0m;

        // Aggregate trades into per-day net P/L.
        var byDay = chronological
            .GroupBy(t => (t.CloseTime ?? t.OpenTime).Date)
            .OrderBy(g => g.Key)
            .ToList();

        if (byDay.Count < 2) return 0m;

        var first = byDay[0].Key;
        var last = byDay[^1].Key;
        var totalDays = (int)(last - first).TotalDays + 1;

        // Build a dense daily return series. Days with no trades contribute 0.
        // The denominator for each day's return is the equity at the start of that day,
        // matching the holding-period-return convention.
        var profitByDay = byDay.ToDictionary(g => g.Key, g => g.Sum(NetOf));

        var dailyReturns = new List<double>(totalDays);
        var equity = (double)initialBalance;
        for (var i = 0; i < totalDays; i++)
        {
            var day = first.AddDays(i);
            var profit = profitByDay.TryGetValue(day, out var p) ? (double)p : 0.0;
            var ret = equity > 0 ? profit / equity : 0.0;
            dailyReturns.Add(ret);
            equity += profit;
        }

        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / (dailyReturns.Count - 1);
        var std = Math.Sqrt(variance);
        if (std == 0) return 0m;

        // Annualise using sqrt(252) — equity-trading convention.
        return (decimal)(mean / std * Math.Sqrt(TradingDaysPerYear));
    }

    private static decimal ComputeSqn(IReadOnlyList<decimal> nets)
    {
        if (nets.Count < 2) return 0m;
        var mean = nets.Average();
        var std = StandardDeviation(nets);
        if (std == 0) return 0m;
        return mean / std * (decimal)Math.Sqrt(nets.Count);
    }

    private static decimal ComputeExposure(IReadOnlyList<StrategyTrade> chronological)
    {
        if (chronological.Count == 0) return 0m;

        // Merge overlapping (open, close) intervals — concurrent trades count once.
        var intervals = chronological
            .Where(t => t.CloseTime.HasValue)
            .Select(t => (Open: t.OpenTime, Close: t.CloseTime!.Value))
            .OrderBy(i => i.Open)
            .ToList();

        if (intervals.Count == 0) return 0m;

        // The denominator is `(latestClose - earliestOpen)` — the wall-clock window from
        // the very first trade open to the very last trade close. Using `intervals[^1].Close`
        // is wrong because intervals are ordered by Open, not Close, so a long trade that
        // started earlier may close later than the "last" interval.
        var earliestOpen = intervals[0].Open;
        var latestClose = intervals.Max(i => i.Close);
        var totalSpan = (latestClose - earliestOpen).TotalSeconds;
        if (totalSpan <= 0) return 0m;

        var mergedSeconds = 0.0;
        var currentStart = intervals[0].Open;
        var currentEnd = intervals[0].Close;

        foreach (var (open, close) in intervals.Skip(1))
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

    private static (decimal zScore, decimal zProbability) ComputeZScore(IReadOnlyList<decimal> nets)
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
    private static double Erf(double x)
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
}
