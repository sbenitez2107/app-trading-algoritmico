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

    /// <summary>
    /// Builds the strategy equity curve: one point per CLOSED trade in chronological order,
    /// walking running equity from <paramref name="initialBalance"/> with drawdown measured
    /// against the running peak. Mirrors <see cref="PortfolioAnalyticsCalculator"/>.ComputeEquityCurve
    /// so a single strategy and a portfolio are charted with the exact same logic.
    /// </summary>
    public static IReadOnlyList<StrategyEquityPointDto> ComputeEquityCurve(
        decimal initialBalance,
        IEnumerable<StrategyTrade> trades)
    {
        var closed = trades.Where(t => !t.IsOpen)
            .OrderBy(t => t.CloseTime ?? t.OpenTime)
            .ToList();

        if (closed.Count == 0) return Array.Empty<StrategyEquityPointDto>();

        var points = new List<StrategyEquityPointDto>(closed.Count);
        var equity = initialBalance;
        var peak = initialBalance;
        foreach (var t in closed)
        {
            equity += NetOf(t);
            if (equity > peak) peak = equity;
            var dd = peak - equity;
            var ddPct = peak > 0 ? dd / peak : 0m;
            points.Add(new StrategyEquityPointDto(t.CloseTime ?? t.OpenTime, equity, dd, ddPct));
        }

        return points;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static decimal NetOf(StrategyTrade t) => AnalyticsSeries.NetOf(t);

    private static decimal StandardDeviation(IReadOnlyList<decimal> values) =>
        AnalyticsSeries.StandardDeviation(values);

    private static (int maxWin, int maxLoss, decimal avgWin, decimal avgLoss)
        ComputeStreaks(IReadOnlyList<decimal> nets) => AnalyticsSeries.ComputeStreaks(nets);

    private static (decimal maxDdAmount, decimal maxDdPercent, int stagnationDays, decimal finalEquity)
        ComputeEquityStats(decimal initialBalance, IReadOnlyList<StrategyTrade> chronological) =>
        AnalyticsSeries.ComputeEquityStats(
            initialBalance,
            chronological.Select(t => (t.CloseTime ?? t.OpenTime, NetOf(t))).ToList());

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

    private static decimal ComputeSharpe(decimal initialBalance, IReadOnlyList<StrategyTrade> chronological) =>
        AnalyticsSeries.ComputeSharpe(
            initialBalance,
            chronological.Select(t => (t.CloseTime ?? t.OpenTime, NetOf(t))).ToList());

    private static decimal ComputeSqn(IReadOnlyList<decimal> nets) => AnalyticsSeries.ComputeSqn(nets);

    private static decimal ComputeExposure(IReadOnlyList<StrategyTrade> chronological) =>
        AnalyticsSeries.ComputeExposure(
            chronological
                .Where(t => t.CloseTime.HasValue)
                .Select(t => (t.OpenTime, t.CloseTime!.Value))
                .ToList());

    private static (decimal zScore, decimal zProbability) ComputeZScore(IReadOnlyList<decimal> nets) =>
        AnalyticsSeries.ComputeZScore(nets);
}
