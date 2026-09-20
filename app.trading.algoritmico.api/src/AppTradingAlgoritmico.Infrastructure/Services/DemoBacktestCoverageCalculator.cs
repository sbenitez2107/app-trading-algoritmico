using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Classifies every calendar month inside the DENSE window — the earliest to the latest open
/// timestamp across the UNION of the demo and backtest trade sets — as one of the four
/// <see cref="PeriodCoverage"/> values (spec Definitions; design D11). Stateless and pure: no I/O,
/// no <c>DbContext</c>, no randomness, no seed.
/// <para>
/// Deliberately NOT built on a <c>Dictionary&lt;(int,int), …&gt;</c> keyed only by months a trade
/// lands in — that shape (slice A's own precedent) makes <see cref="PeriodCoverage.NoTradesEitherSide"/>
/// unreachable by construction, reproducing exactly the silent-omission failure this capability
/// exists to catch (design D11 "Rejected: observed months only"). The window's endpoints are
/// derived from the trade data itself; no lookback or max-window setting exists or may be added.
/// </para>
/// <para>
/// Callers MUST establish that at least one trade exists on at least one side before calling this
/// (spec "The Decomposition Status Discloses Which Component Was Producible At All" — checked by
/// <see cref="CostDecompositionReadService"/> before window derivation is attempted). If both sets
/// are empty this returns an empty component rather than an empty window row.
/// </para>
/// </summary>
internal static class DemoBacktestCoverageCalculator
{
    public static CoverageComponentDto Compute(
        IReadOnlyList<CostObservation> demo,
        IReadOnlyList<CostObservation> backtest)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(backtest);

        var demoByMonth = GroupByMonth(demo);
        var backtestByMonth = GroupByMonth(backtest);

        if (demoByMonth.Count == 0 && backtestByMonth.Count == 0)
            return new CoverageComponentDto([]);

        var allMonthKeys = demoByMonth.Keys.Union(backtestByMonth.Keys).ToList();
        var first = allMonthKeys.Min();
        var last = allMonthKeys.Max();

        var months = new List<CoverageMonthDto>();
        for (var cursor = first; cursor.CompareTo(last) <= 0; cursor = NextMonth(cursor))
        {
            var demoTimes = demoByMonth.TryGetValue(cursor, out var demoList) ? demoList : [];
            var backtestTimes = backtestByMonth.TryGetValue(cursor, out var backtestList) ? backtestList : [];

            var periodCoverage = (demoTimes.Count > 0, backtestTimes.Count > 0) switch
            {
                (true, true) => PeriodCoverage.BothSidesTraded,
                (true, false) => PeriodCoverage.DemoOnlyNoBacktestTrades,
                (false, true) => PeriodCoverage.BacktestOnlyNoDemoTrades,
                (false, false) => PeriodCoverage.NoTradesEitherSide,
            };

            months.Add(new CoverageMonthDto(
                cursor.Year,
                cursor.Month,
                periodCoverage,
                demoTimes,
                backtestTimes,
                demoTimes.Count,
                backtestTimes.Count));
        }

        return new CoverageComponentDto(months);
    }

    private static (int Year, int Month) NextMonth((int Year, int Month) key)
        => key.Month == 12 ? (key.Year + 1, 1) : (key.Year, key.Month + 1);

    private static Dictionary<(int Year, int Month), List<DateTime>> GroupByMonth(IReadOnlyList<CostObservation> observations)
    {
        var groups = new Dictionary<(int, int), List<DateTime>>();
        foreach (var observation in observations)
        {
            var key = (observation.OpenTime.Year, observation.OpenTime.Month);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(observation.OpenTime);
        }

        return groups;
    }
}
