using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>One member of a portfolio: its strategy, raw allocation weight, trades, and source broker.</summary>
public sealed record PortfolioMemberInput(
    Guid StrategyId,
    string StrategyName,
    decimal Weight,
    IReadOnlyList<StrategyTrade> Trades,
    string? Broker = null);

/// <summary>
/// Pure, stateless calculator for portfolio-level analytics. Each member's per-trade net is scaled
/// by its NORMALIZED weight and all members are merged into ONE chronological stream; the combined
/// KPIs, equity curve and monthly returns are then computed on that merged stream using the shared
/// <see cref="AnalyticsSeries"/> primitives — the exact same math a single strategy uses. Weights
/// are RAW position-size multipliers, NOT shares of a total (see <c>EffectiveWeights</c>): N
/// strategies at weight 1 sum their full P/L, exactly like an SQX portfolio.
/// </summary>
public static class PortfolioAnalyticsCalculator
{
    private const int CalendarDaysPerYear = 365;

    private readonly record struct WeightedEvent(DateTime When, DateTime Open, DateTime? Close, decimal Net, string Symbol);

    /// <summary>
    /// How <see cref="CorrelationMatrixCore"/> aligns two members' daily net series before taking
    /// their coefficient. Private: the choice is stated by each public adapter, never by a caller.
    /// </summary>
    private enum AlignmentMode
    {
        /// <summary>
        /// The union of every member's trading days; a member with no trade on a day contributes 0.
        /// The real-account path's shipped alignment — never change it, its output is pinned.
        /// </summary>
        Union,

        /// <summary>
        /// Per pair, only the days on which BOTH members closed a trade. Removes co-absence from
        /// the coefficient instead of disclosing it, which a sparse backtest series needs.
        /// </summary>
        Intersection,
    }

    /// <summary>
    /// Whether a low percentile is taken verbatim or gated on the series actually holding enough
    /// NEGATIVE observations to reach the index the percentile reads (see
    /// <see cref="SupportedPercentile"/>).
    ///
    /// Required, never defaulted, at every call site: a default would let a future caller inherit
    /// one path's behaviour silently. A <c>bool</c> was rejected because
    /// <c>ComputeMonthlyVar(nets, capital, true)</c> does not say what is true.
    /// </summary>
    private enum PercentilePolicy
    {
        /// <summary>
        /// Take the percentile verbatim. What the real-account path passes, so a sparse live
        /// account keeps reporting its shipped number — including a possible <c>0.00</c>.
        /// </summary>
        Unconditional,

        /// <summary>
        /// Withhold the figure (<c>null</c>, never <c>0</c>) when the negative-observation count
        /// cannot reach the percentile's read index.
        /// </summary>
        RequireSupport,
    }

    /// <summary>Builds the full combined <see cref="PortfolioAnalyticsDto"/> from weighted members.</summary>
    public static PortfolioAnalyticsDto Compute(decimal initialCapital, IReadOnlyList<PortfolioMemberInput> members)
    {
        var memberCount = members.Count;
        var norm = EffectiveWeights(members);
        var events = BuildWeightedEvents(norm, members);

        var nets = events.Select(e => e.Net).ToList();
        var closedCount = nets.Count;

        var netProfit = nets.Sum();
        var grossProfit = nets.Where(n => n > 0).Sum();
        var grossLoss = nets.Where(n => n < 0).Sum();
        var winCount = nets.Count(n => n > 0);
        var lossCount = nets.Count(n => n < 0);
        var breakevenCount = nets.Count(n => n == 0);

        var averageTrade = closedCount > 0 ? nets.Average() : 0m;
        var averageWin = winCount > 0 ? nets.Where(n => n > 0).Average() : 0m;
        var averageLoss = lossCount > 0 ? nets.Where(n => n < 0).Average() : 0m;
        var largestWin = closedCount > 0 ? nets.Max() : 0m;
        var largestLoss = closedCount > 0 ? nets.Min() : 0m;
        var stdDev = AnalyticsSeries.StandardDeviation(nets);

        var winRate = closedCount > 0 ? (decimal)winCount / closedCount : 0m;
        var profitFactor = grossLoss != 0 ? grossProfit / Math.Abs(grossLoss) : 0m;
        var payoutRatio = averageLoss != 0 ? averageWin / Math.Abs(averageLoss) : 0m;
        var winsLossesRatio = lossCount > 0 ? (decimal)winCount / lossCount : 0m;
        var expectancy = averageTrade;
        var rExpectancy = averageLoss != 0 ? expectancy / Math.Abs(averageLoss) : 0m;

        var (maxWinStreak, maxLossStreak, avgWinStreak, avgLossStreak) = AnalyticsSeries.ComputeStreaks(nets);

        var dated = events.Select(e => (e.When, e.Net)).ToList();
        var (maxDdAmount, maxDdPercent, stagnationDays, finalEquity) =
            AnalyticsSeries.ComputeEquityStats(initialCapital, dated);

        var firstAt = closedCount > 0 ? events[0].When : (DateTime?)null;
        var lastAt = closedCount > 0 ? events[^1].When : (DateTime?)null;
        var daysSpanned = firstAt is not null && lastAt is not null
            ? Math.Max(1, (int)(lastAt.Value - firstAt.Value).TotalDays)
            : 0;

        var totalReturn = initialCapital > 0 ? netProfit / initialCapital : 0m;

        decimal cagr = 0m;
        if (initialCapital > 0 && daysSpanned > 0 && finalEquity > 0)
        {
            var years = (double)daysSpanned / CalendarDaysPerYear;
            if (years > 0)
            {
                var ratio = (double)(finalEquity / initialCapital);
                cagr = (decimal)(Math.Pow(ratio, 1.0 / years) - 1.0);
            }
        }

        var returnDdRatio = maxDdPercent != 0 ? totalReturn / maxDdPercent : 0m;
        var annualReturnMaxDdRatio = maxDdPercent != 0 ? cagr / maxDdPercent : 0m;
        var yearlyAvgProfit = daysSpanned > 0 ? netProfit * 365m / daysSpanned : 0m;
        var monthlyAvgProfit = yearlyAvgProfit / 12m;
        var dailyAvgProfit = daysSpanned > 0 ? netProfit / daysSpanned : 0m;
        var ahpr = ComputeAhpr(initialCapital, events);
        var sharpe = AnalyticsSeries.ComputeSharpe(initialCapital, dated);
        var sqn = AnalyticsSeries.ComputeSqn(nets);
        var exposure = AnalyticsSeries.ComputeExposure(
            events.Where(e => e.Close.HasValue).Select(e => (e.Open, e.Close!.Value)).ToList());
        var (zScore, zProbability) = AnalyticsSeries.ComputeZScore(nets);

        var contributions = BuildContributions(members, norm, netProfit);

        var bySymbol = events
            .GroupBy(e => e.Symbol)
            .Select(g =>
            {
                var net = g.Sum(e => e.Net);
                return new SymbolBreakdownDto(
                    Symbol: g.Key,
                    NetProfit: net,
                    ReturnPercent: initialCapital > 0 ? net / initialCapital : 0m,
                    TradeCount: g.Count());
            })
            .OrderByDescending(s => Math.Abs(s.NetProfit))
            .ToList();

        return new PortfolioAnalyticsDto(
            InitialCapital: initialCapital,
            FirstTradeAt: firstAt,
            LastTradeAt: lastAt,
            DaysSpanned: daysSpanned,
            MemberCount: memberCount,
            TradeCount: closedCount,
            WinCount: winCount,
            LossCount: lossCount,
            BreakevenCount: breakevenCount,
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
            Ahpr: ahpr,
            MaxConsecutiveWins: maxWinStreak,
            MaxConsecutiveLosses: maxLossStreak,
            AverageConsecutiveWins: avgWinStreak,
            AverageConsecutiveLosses: avgLossStreak,
            TotalReturn: totalReturn,
            Cagr: cagr,
            DailyAvgProfit: dailyAvgProfit,
            MonthlyAvgProfit: monthlyAvgProfit,
            YearlyAvgProfit: yearlyAvgProfit,
            MaxDrawdownAmount: maxDdAmount,
            MaxDrawdownPercent: maxDdPercent,
            ReturnDrawdownRatio: returnDdRatio,
            AnnualReturnMaxDdRatio: annualReturnMaxDdRatio,
            StagnationInDays: stagnationDays,
            SharpeRatio: sharpe,
            Sqn: sqn,
            Exposure: exposure,
            ZScore: zScore,
            ZProbability: zProbability,
            FinalEquity: finalEquity,
            Members: contributions,
            BySymbol: bySymbol);
    }

    /// <summary>Base instrument code from a raw broker symbol (e.g. "XAUUSD_M1_UTC02" → "XAUUSD").</summary>
    private static string NormalizeSymbol(string? item)
    {
        if (string.IsNullOrWhiteSpace(item)) return "—";
        var s = item.Trim().ToUpperInvariant();
        var sep = s.IndexOfAny(['_', '.', ' ']);
        return sep > 0 ? s[..sep] : s;
    }

    /// <summary>
    /// Monthly performance series on the merged weighted stream. The bucketing, compounding and
    /// per-month drawdown/win-loss math live in <see cref="AnalyticsSeries.BuildMonthlyReturns"/>,
    /// so a portfolio is measured exactly like a single strategy.
    /// </summary>
    public static IReadOnlyList<MonthlyReturnDto> ComputeMonthlyReturns(
        decimal initialCapital, IReadOnlyList<PortfolioMemberInput> members)
    {
        var events = BuildWeightedEvents(EffectiveWeights(members), members);
        return AnalyticsSeries.BuildMonthlyReturns(
            initialCapital, events.Select(e => (e.When, e.Net)).ToList());
    }

    /// <summary>
    /// Per-member CONTRIBUTION curves: for each member, the cumulative weighted net P/L it has
    /// added to the portfolio, one point per closed trade in chronological order.
    ///
    /// Deliberately NOT each strategy's standalone equity curve. A standalone curve runs on the
    /// account's own initial balance and therefore assumes the strategy traded the whole capital
    /// alone; drawn against the combined curve those numbers would not reconcile. Weighting every
    /// net by the member's normalized weight makes the decomposition exact: the final
    /// contributions sum to the combined curve's gain over initial capital.
    ///
    /// Members with no closed trades keep a row with an empty series, so the UI can still list
    /// them instead of silently dropping them.
    /// </summary>
    public static IReadOnlyList<PortfolioMemberEquityCurveDto> ComputeMemberEquityCurves(
        IReadOnlyList<PortfolioMemberInput> members)
    {
        var norm = EffectiveWeights(members);
        var result = new List<PortfolioMemberEquityCurveDto>(members.Count);

        for (var i = 0; i < members.Count; i++)
        {
            var weight = norm[i];
            var closed = members[i].Trades
                .Where(t => !t.IsOpen)
                .OrderBy(t => t.CloseTime ?? t.OpenTime)
                .ToList();

            var cumulative = 0m;
            var points = new List<PortfolioContributionPointDto>(closed.Count);
            foreach (var t in closed)
            {
                cumulative += weight * AnalyticsSeries.NetOf(t);
                points.Add(new PortfolioContributionPointDto(t.CloseTime ?? t.OpenTime, cumulative));
            }

            result.Add(new PortfolioMemberEquityCurveDto(
                StrategyId: members[i].StrategyId,
                StrategyName: members[i].StrategyName,
                RawWeight: weight,
                FinalContribution: cumulative,
                Points: points));
        }

        return result;
    }

    /// <summary>Forward-walked combined equity curve: one point per closed trade (chronological).</summary>
    public static IReadOnlyList<PortfolioEquityPointDto> ComputeEquityCurve(
        decimal initialCapital, IReadOnlyList<PortfolioMemberInput> members)
    {
        var events = BuildWeightedEvents(EffectiveWeights(members), members);
        if (events.Count == 0) return Array.Empty<PortfolioEquityPointDto>();

        var points = new List<PortfolioEquityPointDto>(events.Count);
        var equity = initialCapital;
        var peak = initialCapital;
        foreach (var e in events)
        {
            equity += e.Net;
            if (equity > peak) peak = equity;
            var dd = peak - equity;
            var ddPct = peak > 0 ? dd / peak : 0m;
            points.Add(new PortfolioEquityPointDto(e.When, equity, dd, ddPct));
        }

        return points;
    }

    /// <summary>
    /// Historical Value-at-Risk over the portfolio's daily NET-P/L series. Each member's per-trade
    /// net is weighted and merged into one dense daily series; VaR is the negated low percentile of
    /// the most-recent <paramref name="windowDays"/> daily observations. Reported as a positive loss
    /// magnitude in currency and as % of <paramref name="initialCapital"/>, with a per-broker
    /// (per funding service) standalone breakdown.
    /// </summary>
    public static PortfolioRiskDto ComputeVaR(
        decimal initialCapital, IReadOnlyList<PortfolioMemberInput> members, int windowDays = 250)
    {
        var norm = EffectiveWeights(members);
        var weighted = members.Select((m, i) => (m, w: norm[i])).ToList();

        var fullDaily = WindowedDailyNets(initialCapital, weighted, 0);
        var maxDdPercent = MaxDrawdownPercentFromDaily(initialCapital, fullDaily);
        var dailyNets = windowDays > 0 && fullDaily.Count > windowDays
            ? fullDaily.Skip(fullDaily.Count - windowDays).ToList()
            : fullDaily;
        // Unconditional: the real-account path never withholds, so every figure below is present
        // and bit-identical to its pre-policy value. `.Value` rather than `?? 0m` on purpose — an
        // impossible null must fail loudly, not silently become a published zero.
        var (gatedVar95, gatedVar99, worst, best) = VarFromDaily(dailyNets, PercentilePolicy.Unconditional);
        var var95 = gatedVar95!.Value;
        var var99 = gatedVar99!.Value;

        var byService = weighted
            .GroupBy(x => string.IsNullOrWhiteSpace(x.m.Broker) ? "—" : x.m.Broker!)
            .Select(g =>
            {
                var grp = g.ToList();
                var groupNets = WindowedDailyNets(initialCapital, grp, windowDays);
                var (gatedGroupVar95, _, _, _) = VarFromDaily(groupNets, PercentilePolicy.Unconditional);
                var gv95 = gatedGroupVar95!.Value;
                var net = grp.Sum(x => x.w * x.m.Trades.Where(t => !t.IsOpen).Sum(AnalyticsSeries.NetOf));
                var monthly = ComputeMonthlyVar(groupNets, initialCapital, PercentilePolicy.Unconditional);
                return new ServiceRiskDto(
                    Service: g.Key,
                    StrategyCount: grp.Count,
                    NetProfit: net,
                    Var95: gv95,
                    Var95Percent: initialCapital > 0 ? gv95 / initialCapital : 0m,
                    MonthlyVarInsufficientHistory: monthly.insufficientHistory,
                    MonthlyVarObservationDays: groupNets.Count,
                    MonthlyVarOverlappingWindows: monthly.overlappingWindows,
                    MonthlyVarIndependentWindows: monthly.independentWindows,
                    MonthlyVar95: monthly.monthlyVar95,
                    MonthlyVar95Percent: monthly.monthlyVar95Percent);
            })
            .OrderByDescending(s => s.Var95)
            .ToList();

        return new PortfolioRiskDto(
            InitialCapital: initialCapital,
            Method: "Historical",
            WindowDays: windowDays,
            ObservationDays: dailyNets.Count,
            Var95: var95,
            Var95Percent: initialCapital > 0 ? var95 / initialCapital : 0m,
            Var99: var99,
            Var99Percent: initialCapital > 0 ? var99 / initialCapital : 0m,
            WorstDay: worst,
            BestDay: best,
            MaxDrawdownPercent: maxDdPercent,
            ByService: byService,
            Guardrails: Array.Empty<ServiceGuardrailDto>());
    }

    /// <summary>Average holding-period return: mean of each event's net over the equity before it.</summary>
    private static decimal ComputeAhpr(decimal initialCapital, List<WeightedEvent> events)
    {
        if (events.Count == 0 || initialCapital <= 0) return 0m;
        var equity = initialCapital;
        var sum = 0m;
        foreach (var e in events)
        {
            if (equity != 0) sum += e.Net / equity;
            equity += e.Net;
        }
        return sum / events.Count;
    }

    /// <summary>Max drawdown (as a fraction of the running peak) over a daily NET series.</summary>
    private static decimal MaxDrawdownPercentFromDaily(decimal initialCapital, List<decimal> nets)
    {
        var equity = initialCapital;
        var peak = initialCapital;
        var maxDdPct = 0m;
        foreach (var n in nets)
        {
            equity += n;
            if (equity > peak) { peak = equity; continue; }
            var ddPct = peak > 0 ? (peak - equity) / peak : 0m;
            if (ddPct > maxDdPct) maxDdPct = ddPct;
        }
        return maxDdPct;
    }

    /// <summary>
    /// Pearson correlation matrix between member strategies' daily NET series, aligned over the union
    /// of all trading days (a day with no trade for a strategy contributes 0). Unweighted — measures
    /// the strategies' intrinsic co-movement. Lower correlations = better diversification.
    /// </summary>
    public static PortfolioCorrelationDto ComputeCorrelation(IReadOnlyList<PortfolioMemberInput> members)
    {
        var labels = members.Select(m => m.StrategyName).ToList();
        var n = members.Count;
        if (n == 0)
            return new PortfolioCorrelationDto(labels, Array.Empty<IReadOnlyList<decimal>>(), 0, 0m);

        // Per-member day → net. Building the maps is the adapter's job (it knows StrategyTrade);
        // aligning them and taking the coefficients is the shared core's.
        var dayMaps = new List<Dictionary<DateTime, decimal>>(n);
        foreach (var m in members)
        {
            var map = new Dictionary<DateTime, decimal>();
            foreach (var t in m.Trades)
            {
                if (t.IsOpen) continue;
                var day = (t.CloseTime ?? t.OpenTime).Date;
                map[day] = map.GetValueOrDefault(day) + AnalyticsSeries.NetOf(t);
            }
            dayMaps.Add(map);
        }

        var (matrix, obs, avg) = CorrelationMatrixCore(dayMaps, AlignmentMode.Union);
        return new PortfolioCorrelationDto(labels, matrix, obs, avg);
    }

    /// <summary>
    /// The correlation matrix over per-member <c>day → net</c> maps, aligned per
    /// <paramref name="mode"/>. Source-agnostic: it never sees a trade, so a real-account series
    /// and a backtest-derived one share ONE alignment and rounding implementation.
    ///
    /// <c>ObservationDays</c> is the union day span in both modes — it describes the sample the
    /// matrix was drawn from, not any single pair's overlap. Under
    /// <see cref="AlignmentMode.Intersection"/> a pair with fewer than two shared days or a
    /// constant shared series yields <c>0</c> here, exactly as <see cref="Pearson"/> already does;
    /// mapping that to a withheld cell belongs to the caller that can express one.
    /// </summary>
    private static (IReadOnlyList<IReadOnlyList<decimal>> Matrix, int ObservationDays, decimal AverageCorrelation)
        CorrelationMatrixCore(IReadOnlyList<Dictionary<DateTime, decimal>> dayMaps, AlignmentMode mode)
    {
        var n = dayMaps.Count;

        var allDays = new SortedSet<DateTime>();
        foreach (var map in dayMaps)
            foreach (var day in map.Keys)
                allDays.Add(day);

        var days = allDays.ToList();
        var obs = days.Count;

        // Union-aligned daily-net series per member (0 on non-trading days). Intersection aligns
        // per PAIR instead, so there is no single series to precompute.
        var unionSeries = mode == AlignmentMode.Union
            ? dayMaps.Select(map => days.Select(d => (double)map.GetValueOrDefault(d)).ToArray()).ToList()
            : [];

        var matrix = new List<IReadOnlyList<decimal>>(n);
        var offDiagSum = 0.0;
        var offDiagCount = 0;
        for (var i = 0; i < n; i++)
        {
            var row = new List<decimal>(n);
            for (var j = 0; j < n; j++)
            {
                if (i == j) { row.Add(1m); continue; }
                var (x, y) = mode == AlignmentMode.Union
                    ? (unionSeries[i], unionSeries[j])
                    : IntersectionAligned(dayMaps[i], dayMaps[j]);
                var c = Pearson(x, y);
                row.Add(Math.Round((decimal)c, 4));
                if (i < j) { offDiagSum += c; offDiagCount++; }
            }
            matrix.Add(row);
        }

        var avg = offDiagCount > 0 ? Math.Round((decimal)(offDiagSum / offDiagCount), 4) : 0m;
        return (matrix, obs, avg);
    }

    /// <summary>Both members' nets over ONLY the days on which both closed a trade, chronological.</summary>
    private static (double[] X, double[] Y) IntersectionAligned(
        Dictionary<DateTime, decimal> left, Dictionary<DateTime, decimal> right)
    {
        var shared = left.Keys.Where(right.ContainsKey).OrderBy(d => d).ToList();
        return (
            shared.Select(d => (double)left[d]).ToArray(),
            shared.Select(d => (double)right[d]).ToArray());
    }

    /// <summary>Pearson correlation of two equal-length series. Returns 0 if either is constant.</summary>
    private static double Pearson(double[] x, double[] y)
    {
        var len = x.Length;
        if (len < 2) return 0;
        double mx = x.Average(), my = y.Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (var k = 0; k < len; k++)
        {
            var dx = x[k] - mx;
            var dy = y[k] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }
        if (sxx == 0 || syy == 0) return 0;
        return sxy / Math.Sqrt(sxx * syy);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Dense daily NET series for a weighted member subset, trimmed to the last N days.</summary>
    private static List<decimal> WindowedDailyNets(
        decimal initialCapital, IReadOnlyList<(PortfolioMemberInput m, decimal w)> weighted, int windowDays)
    {
        var events = new List<(DateTime When, decimal Net)>();
        foreach (var (m, w) in weighted)
            foreach (var t in m.Trades)
                if (!t.IsOpen)
                    events.Add((t.CloseTime ?? t.OpenTime, w * AnalyticsSeries.NetOf(t)));

        var nets = AnalyticsSeries.BuildDailyNetSeries(initialCapital, events).Select(p => p.Net).ToList();
        if (windowDays > 0 && nets.Count > windowDays)
            nets = nets.Skip(nets.Count - windowDays).ToList();
        return nets;
    }

    /// <summary>
    /// VaR95/VaR99 (positive losses) + worst/best single day from a daily NET series.
    ///
    /// <paramref name="policy"/> decides only whether the two percentiles may be WITHHELD, never
    /// what they are: under <see cref="PercentilePolicy.Unconditional"/> both are always present
    /// and bit-identical to the ungated read. Worst/best are extremes, not percentiles, so no
    /// policy applies to them.
    /// </summary>
    private static (decimal? var95, decimal? var99, decimal worst, decimal best) VarFromDaily(
        List<decimal> nets, PercentilePolicy policy)
    {
        if (nets.Count == 0)
        {
            // An absent series supports nothing. The live path keeps its shipped zeros.
            var empty = policy == PercentilePolicy.RequireSupport ? (decimal?)null : 0m;
            return (empty, empty, 0m, 0m);
        }

        var sorted = nets.OrderBy(x => x).ToList();
        var p95 = LowPercentile(sorted, 0.05, policy);
        var p99 = LowPercentile(sorted, 0.01, policy);
        var worst = -sorted[0];        // most negative day, as a positive loss
        var best = sorted[^1];
        return (p95 is null ? null : -p95.Value, p99 is null ? null : -p99.Value, worst, best);
    }

    /// <summary>Applies <paramref name="policy"/>: the percentile verbatim, or the density-gated one.</summary>
    private static decimal? LowPercentile(IReadOnlyList<decimal> sorted, double p, PercentilePolicy policy)
        => policy == PercentilePolicy.RequireSupport
            ? SupportedPercentile(sorted, p)
            : Percentile(sorted, p);

    /// <summary>
    /// Monthly VaR95 estimate from a dense daily NET series (`portfolio-monthly-var` spec):
    /// rolling <see cref="AnalyticsSeries.MonthlyVarHorizonDays"/>-calendar-day window sums, 5th
    /// percentile taken directly with NO √t scaling (KB §5 trap 1 — the series is already
    /// calendar-day dense, so a 30-element window already spans Darwinex's stated monthly horizon).
    /// Requires <see cref="AnalyticsSeries.MinHistoryDays"/> of history; below that, no numeric
    /// estimate is produced. Guardrail-agnostic — computed unconditionally for every service so any
    /// broker can back a future `VarTarget` readout.
    ///
    /// The 30-day horizon, the 90-day history floor and the 5th-percentile method are the same for
    /// every source. The DENSITY GATE is not: <paramref name="policy"/> selects it, and it lives
    /// HERE rather than in a caller so the count that gates is the same single derivation that gets
    /// reported. The real-account path passes <see cref="PercentilePolicy.Unconditional"/>, so its
    /// output is bit-identical to this function's pre-policy behaviour — which is the property that
    /// holds, NOT that this function is untouched.
    /// </summary>
    private static (bool insufficientHistory, int overlappingWindows, int independentWindows,
        decimal? monthlyVar95, decimal? monthlyVar95Percent) ComputeMonthlyVar(
        List<decimal> dailyNets, decimal initialCapital, PercentilePolicy policy)
    {
        const int horizon = AnalyticsSeries.MonthlyVarHorizonDays;
        var n = dailyNets.Count;
        if (n < AnalyticsSeries.MinHistoryDays)
            return (true, 0, 0, null, null);

        var sums = AnalyticsSeries.RollingWindowSums(dailyNets, horizon).OrderBy(x => x).ToList();
        var overlappingWindows = n - horizon + 1;
        var independentWindows = n / horizon;

        var p05 = LowPercentile(sums, 0.05, policy);
        if (p05 is null)
            return (false, overlappingWindows, independentWindows, null, null);

        var monthlyVar95 = -p05.Value;
        var monthlyVar95Percent = initialCapital > 0 ? monthlyVar95 / initialCapital : 0m;
        return (false, overlappingWindows, independentWindows, monthlyVar95, monthlyVar95Percent);
    }

    /// <summary>Linear-interpolated percentile of an ascending-sorted list. p in [0,1].</summary>
    private static decimal Percentile(IReadOnlyList<decimal> sorted, double p)
    {
        if (sorted.Count == 0) return 0m;
        if (sorted.Count == 1) return sorted[0];
        var rank = p * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var frac = (decimal)(rank - lo);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
    }

    /// <summary>
    /// <see cref="Percentile"/>, withheld (<c>null</c>) when the series does not hold enough
    /// NEGATIVE observations for that percentile to be a loss estimate at all.
    ///
    /// <see cref="Percentile"/> reads <c>sorted[floor(p * (N-1))]</c>. On an ascending sort the
    /// negatives occupy indices <c>0 .. negativeCount-1</c>, then the zero-net observations, then
    /// the positives — and a positive sorts ABOVE the zero block, so it can never supply the mass
    /// that index needs. The read is therefore supported exactly when
    /// <c>negativeCount &gt;= floor(p * (N-1)) + 1</c>.
    ///
    /// Three things this deliberately is NOT. It is not a hard-coded share (a bare <c>5%</c> is
    /// what produced the error this gate corrects); it is not a relation against the NON-ZERO
    /// count (a series can clear any non-zero-share bar while its negative count still fails —
    /// measured on both committed fixtures, where such a gate would publish a figure that is
    /// exactly <c>0.00</c>); and it is not a single verdict per series (the relation is a function
    /// of <paramref name="p"/>, so a series can support <c>p = 0.01</c> while failing
    /// <c>p = 0.05</c>, the more extreme percentile reading DEEPER into the negative block).
    ///
    /// <see cref="Percentile"/>'s own body is untouched, so a caller that does not ask for the gate
    /// never reaches it and no shipped number moves.
    /// </summary>
    private static decimal? SupportedPercentile(IReadOnlyList<decimal> sorted, double p)
    {
        if (sorted.Count == 0) return null;

        var negativeCount = 0;
        for (var i = 0; i < sorted.Count; i++)
            if (sorted[i] < 0m) negativeCount++;

        var required = (int)Math.Floor(p * (sorted.Count - 1)) + 1;
        return negativeCount >= required ? Percentile(sorted, p) : null;
    }

    /// <summary>
    /// The density of ONE dense daily net series: the counts that decide whether a percentile is
    /// supported, and the counts an operator needs to read a withheld figure.
    ///
    /// <see cref="NegativeDayCount"/> and <see cref="NegativeWindowCount"/> are the ONLY two that
    /// gate (see <see cref="SupportedPercentile"/>). <see cref="DenseDayCount"/> and
    /// <see cref="NonZeroDayCount"/> are reported for the operator and MUST NEVER enter the
    /// predicate — a non-zero-day gate is measurably wrong in the reporting direction.
    ///
    /// Day-level only, by design. It cannot report a TRADE count (many trades collapse into one
    /// calendar day, and a day holding a win and a loss can net positive) and it cannot know which
    /// trades were excluded upstream. Those are trade-level facts owned by whoever built the
    /// series, and a consumer that reports both must compose them at its own boundary.
    /// </summary>
    /// <param name="DenseDayCount">Elements in the dense first-to-last calendar-day series.</param>
    /// <param name="NegativeDayCount">Days whose net is strictly negative — a GATING count.</param>
    /// <param name="NonZeroDayCount">Days on which anything closed. Reported, never gating.</param>
    /// <param name="NegativeWindowCount">
    /// Rolling <see cref="AnalyticsSeries.MonthlyVarHorizonDays"/>-day window sums that are
    /// strictly negative — the monthly path's GATING count. Zero when the series is shorter than
    /// one window, by absence of windows rather than by their sign.
    /// </param>
    internal readonly record struct SeriesDensity(
        int DenseDayCount,
        int NegativeDayCount,
        int NonZeroDayCount,
        int NegativeWindowCount);

    /// <summary>
    /// Measures a dense daily net series ONCE, so the count that gated a figure is the same count
    /// reported beside it. Re-deriving these numbers on a second code path is the failure this
    /// exists to prevent: both derivations would look plausible and their divergence would be
    /// invisible.
    /// </summary>
    internal static SeriesDensity Measure(IReadOnlyList<decimal> denseDailyNets)
    {
        var negativeDays = 0;
        var nonZeroDays = 0;
        foreach (var net in denseDailyNets)
        {
            if (net < 0m) negativeDays++;
            if (net != 0m) nonZeroDays++;
        }

        var negativeWindows = 0;
        foreach (var sum in AnalyticsSeries.RollingWindowSums(denseDailyNets, AnalyticsSeries.MonthlyVarHorizonDays))
            if (sum < 0m) negativeWindows++;

        return new SeriesDensity(
            DenseDayCount: denseDailyNets.Count,
            NegativeDayCount: negativeDays,
            NonZeroDayCount: nonZeroDays,
            NegativeWindowCount: negativeWindows);
    }


    /// <summary>
    /// Per-member effective weight = a RAW position-size multiplier (SQX-style): 1.0 combines the
    /// strategy at full size, 2.0 = double, 0.5 = half, 0 = excluded. NOT normalized — so a portfolio
    /// of N strategies at weight 1 sums their full P/L exactly like an SQX portfolio, and drawdown /
    /// Sharpe / profit factor are recomputed on the merged full stream (capturing diversification).
    /// </summary>
    private static decimal[] EffectiveWeights(IReadOnlyList<PortfolioMemberInput> members)
    {
        var result = new decimal[members.Count];
        for (var i = 0; i < members.Count; i++)
            result[i] = members[i].Weight > 0 ? members[i].Weight : 0m;
        return result;
    }

    /// <summary>Merges every member's CLOSED trades, scaling each net by the member's raw weight.</summary>
    private static List<WeightedEvent> BuildWeightedEvents(decimal[] norm, IReadOnlyList<PortfolioMemberInput> members)
    {
        var events = new List<WeightedEvent>();
        for (var i = 0; i < members.Count; i++)
        {
            var w = norm[i];
            foreach (var t in members[i].Trades)
            {
                if (t.IsOpen) continue;
                events.Add(new WeightedEvent(
                    When: t.CloseTime ?? t.OpenTime,
                    Open: t.OpenTime,
                    Close: t.CloseTime,
                    Net: w * AnalyticsSeries.NetOf(t),
                    Symbol: NormalizeSymbol(t.Item)));
            }
        }

        // Stable chronological order (matches the per-strategy calculator's OrderBy).
        return events.OrderBy(e => e.When).ToList();
    }

    private static List<PortfolioMemberContributionDto> BuildContributions(
        IReadOnlyList<PortfolioMemberInput> members, decimal[] weights, decimal portfolioNetProfit)
    {
        var totalWeight = weights.Sum();
        var contributions = new List<PortfolioMemberContributionDto>(members.Count);
        for (var i = 0; i < members.Count; i++)
        {
            var closed = members[i].Trades.Where(t => !t.IsOpen).ToList();
            var rawNet = closed.Sum(AnalyticsSeries.NetOf);
            var weightedNet = weights[i] * rawNet;

            contributions.Add(new PortfolioMemberContributionDto(
                StrategyId: members[i].StrategyId,
                StrategyName: members[i].StrategyName,
                RawWeight: members[i].Weight,
                // Share of total allocation weight (display only — the combination uses raw weights).
                NormalizedWeight: totalWeight > 0 ? weights[i] / totalWeight : 0m,
                TradeCount: closed.Count,
                NetProfit: rawNet,
                WeightedNetProfit: weightedNet,
                ContributionPercent: portfolioNetProfit != 0 ? weightedNet / portfolioNetProfit : 0m));
        }

        return contributions;
    }
}
