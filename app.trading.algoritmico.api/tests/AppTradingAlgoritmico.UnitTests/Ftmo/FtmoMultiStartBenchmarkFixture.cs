using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.1 — test-only support. Builds a deterministic (closed-form, no
/// <see cref="Random"/>) synthetic trade series shaped like the real gold series: ~1,000 trades from
/// 2016-01 to 2025-12, H1 holding times of 1-72 hours, exactly 5 zero-duration trades, no overlapping
/// trades. Two P/L profiles proxy the benchmark's two extremes (design.md Decision 1):
/// <list type="bullet">
/// <item><see cref="Profile.Fast"/> — every trade nets +1% of capital, so ANY monthly start reaches
/// the phase-1 target (+10%) after 10 trades (~30-50 days at this series' spacing) — the scanner's
/// best case (early exit).</item>
/// <item><see cref="Profile.Never"/> — every trade nets 0, so no start ever reaches the target, and
/// the balance never breaches either floor. The scanner exhausts the whole suffix — the worst case.</item>
/// </list>
/// This is a benchmark proxy, not a correctness fixture: it exists only to give
/// <see cref="FtmoChallengeRace.RunChain"/>/<see cref="FtmoBreachEvaluator.Evaluate"/> a realistic
/// trade count and time span to measure against.
/// </summary>
public static class FtmoMultiStartBenchmarkFixture
{
    public enum Profile
    {
        Fast,
        Never,
    }

    public const int TradeCount = 1000;
    public const int ZeroDurationCount = 5;

    private static readonly DateTime SeriesStart = new(2016, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime SeriesEnd = new(2025, 12, 31, 23, 0, 0, DateTimeKind.Unspecified);

    // Five zero-duration rows, spread across the series (the real gold sample has 38/30,266
    // zero-duration rows — this fixture pins exactly 5, per design.md Decision 1).
    private static readonly int[] ZeroDurationIndexes = [100, 300, 500, 700, 900];

    /// <summary>
    /// Builds the deterministic 1,000-row series for <paramref name="profile"/>. Rows are evenly
    /// spaced (~87.7h apart) across the decade — comfortably wider than the 1-72h holding window, so
    /// no two rows ever overlap by construction.
    /// </summary>
    internal static ProjectedTrade[] Build(Profile profile)
    {
        var totalHours = (SeriesEnd - SeriesStart).TotalHours;
        var spacingHours = totalHours / (TradeCount - 1);
        var zeroDuration = new HashSet<int>(ZeroDurationIndexes);

        var trades = new ProjectedTrade[TradeCount];
        for (var i = 0; i < TradeCount; i++)
        {
            var open = SeriesStart.AddHours(i * spacingHours);
            var isZeroDuration = zeroDuration.Contains(i);
            var holdHours = isZeroDuration ? 0 : 1 + (i * 53 % 72);
            var close = open.AddHours(holdHours);

            var net = profile switch
            {
                Profile.Fast => 100m, // +1% of the 10,000 capital used by the benchmark
                Profile.Never => 0m,
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, message: null),
            };

            trades[i] = new ProjectedTrade(i, open, close, net, ResizeOutcome.OnTarget, FtmoLots: 1m);
        }

        return trades;
    }

    /// <summary>
    /// One start per calendar month containing at least one trade (design.md Data Flow's monthly
    /// grain, simplified for the benchmark proxy: every month here has trades by construction). Each
    /// start is the first trade's <c>OpenSource</c> in that month.
    /// </summary>
    internal static DateTime[] MonthlyStartOpens(IReadOnlyList<ProjectedTrade> trades)
    {
        return trades
            .OrderBy(t => t.OpenSource)
            .ThenBy(t => t.RowIndex)
            .GroupBy(t => new DateOnly(t.OpenSource.Year, t.OpenSource.Month, 1))
            .Select(g => g.First().OpenSource)
            .OrderBy(open => open)
            .ToArray();
    }
}
