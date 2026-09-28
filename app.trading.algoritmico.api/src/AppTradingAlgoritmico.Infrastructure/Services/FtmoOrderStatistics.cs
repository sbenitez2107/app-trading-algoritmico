using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-multi-start PR2, task 2.3 (design.md Decision 7) — nearest-rank order statistics,
/// <c>r = ceil(p*n)</c> (1-based) on sorted ints. <c>n = 0</c> reports every quantile as null; every
/// reported quantile is an observed value, never an interpolation (spec.md "Aggregates Are Counts,
/// Shares, And Order Statistics"). <c>internal static</c>, pure: no I/O.
/// </summary>
internal static class FtmoOrderStatistics
{
    /// <summary>Computes min/Q1/median/Q3/max by nearest rank over <paramref name="sortedValues"/>.</summary>
    /// <param name="sortedValues">Values already sorted ascending; the caller owns the sort.</param>
    internal static FtmoOrderStatisticsDto Compute(IReadOnlyList<int> sortedValues)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);

        var n = sortedValues.Count;
        if (n == 0)
            return new FtmoOrderStatisticsDto(0, null, null, null, null, null);

        return new FtmoOrderStatisticsDto(
            n,
            NearestRank(sortedValues, 0.00m),
            NearestRank(sortedValues, 0.25m),
            NearestRank(sortedValues, 0.50m),
            NearestRank(sortedValues, 0.75m),
            NearestRank(sortedValues, 1.00m));
    }

    private static int NearestRank(IReadOnlyList<int> sortedValues, decimal p)
    {
        var n = sortedValues.Count;
        var r = (int)Math.Ceiling(p * n);
        r = Math.Max(1, r); // p = 0 (Min) always resolves to rank 1, never rank 0.
        return sortedValues[r - 1];
    }
}
