using AppTradingAlgoritmico.Infrastructure.Services;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.4 (design.md D7) — test-only support. k members, each the shipped deterministic
/// 1,000-trade series (<see cref="FtmoMultiStartBenchmarkFixture"/>) shifted by <c>m x spacing / k</c> hours,
/// where spacing is that series' own row spacing (~87.7 h). Holds of 1-72 h against offsets of ~11 h at k = 8
/// force real cross-member overlap, and every member keeps <c>RowIndex 0..999</c>, so the merger's
/// duplicate-index renumbering is on the measured path. Closed-form: no <see cref="Random"/>. The low and
/// high ends are identical (a USD-settling, degenerate band), like the shipped benchmark.
/// </summary>
public static class FtmoGroupBenchmarkFixture
{
    private static readonly DateTime SeriesStart = new(2016, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime SeriesEnd = new(2025, 12, 31, 23, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The base series' row spacing in hours (identical to the shipped fixture's).</summary>
    public static double SpacingHours => (SeriesEnd - SeriesStart).TotalHours / (FtmoMultiStartBenchmarkFixture.TradeCount - 1);

    internal static IReadOnlyList<FtmoGroupMerger.MemberSeries> Build(FtmoMultiStartBenchmarkFixture.Profile profile, int memberCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(memberCount, 1);

        var baseline = FtmoMultiStartBenchmarkFixture.Build(profile);
        var members = new List<FtmoGroupMerger.MemberSeries>(memberCount);
        for (var m = 0; m < memberCount; m++)
        {
            var shift = TimeSpan.FromHours(m * SpacingHours / memberCount);
            var shifted = baseline
                .Select(t => t with { OpenSource = t.OpenSource + shift, CloseSource = t.CloseSource + shift })
                .ToList();
            members.Add(new FtmoGroupMerger.MemberSeries(m, shifted, shifted));
        }

        return members;
    }
}
