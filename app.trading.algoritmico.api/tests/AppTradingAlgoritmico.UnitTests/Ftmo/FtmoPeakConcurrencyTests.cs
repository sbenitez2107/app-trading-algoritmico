using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1a.3 (design D3) — <see cref="FtmoPeakConcurrency"/> mirrors the private peak sweep of
/// <c>FtmoGroupDiagnostics</c>. The first group pins the sweep's rules by hand-computed cases; the second proves,
/// on fixtures, that it equals the peak <c>ComputeGroup</c> reports for the same members (hard rule 4b).
/// </summary>
public class FtmoPeakConcurrencyTests
{
    private static DateTime At(int day, int hour, int minute = 0) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(int row, DateTime open, DateTime close, decimal? net = 10m) =>
        new(row, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, net is null ? 0m : 1m);

    // ---- 1a.3.1: hand-computed cases ----

    [Fact]
    public void Compute_ThreeOverlappingRows_ReportsThreeAtTheirCommonMoment()
    {
        // A 10:00-12:00, B 11:00-13:00, C 11:30-11:45: all three are open at 11:30.
        var rows = new[]
        {
            Trade(0, At(12, 10), At(12, 12)),
            Trade(1, At(12, 11), At(12, 13)),
            Trade(2, At(12, 11, 30), At(12, 11, 45)),
        };

        FtmoPeakConcurrency.Compute(rows).Should().Be(3);
    }

    [Fact]
    public void Compute_ACloseAndAnOpenAtTheSameInstant_AreNotConcurrent()
    {
        var rows = new[] { Trade(0, At(12, 10), At(12, 12)), Trade(1, At(12, 12), At(12, 14)) };

        FtmoPeakConcurrency.Compute(rows).Should().Be(1);
    }

    [Fact]
    public void Compute_RowsListedOpenFirstAtTheBoundary_StillCloseBeforeTheyOpen()
    {
        // The later row is listed FIRST, so an implementation that trusts list order would overlap them.
        var rows = new[] { Trade(0, At(12, 12), At(12, 14)), Trade(1, At(12, 10), At(12, 12)) };

        FtmoPeakConcurrency.Compute(rows).Should().Be(1);
    }

    [Fact]
    public void Compute_ZeroDurationAndUnscalableRows_AreIgnored()
    {
        var rows = new[]
        {
            Trade(0, At(12, 10), At(12, 12)),
            Trade(1, At(12, 11), At(12, 11)),
            Trade(2, At(12, 11), At(12, 13), net: null),
        };

        FtmoPeakConcurrency.Compute(rows).Should().Be(1);
    }

    [Fact]
    public void Compute_NoRows_IsZero()
    {
        FtmoPeakConcurrency.Compute([]).Should().Be(0);
    }

    [Fact]
    public void Compute_AMergedSeries_UsesTheLowEnd()
    {
        var a = new MemberSeries(0, [Trade(0, At(12, 10), At(12, 12))], [Trade(0, At(12, 10), At(12, 12))]);
        var b = new MemberSeries(1, [Trade(0, At(12, 11), At(12, 13))], [Trade(0, At(12, 11), At(12, 13))]);
        var merged = Merge([a, b], new GroupWindow(At(12, 0), At(12, 23)));

        FtmoPeakConcurrency.Compute(merged).Should().Be(2);
    }

    // ---- 1a.3.2: parity with the peak ComputeGroup reports ----

    private static readonly Guid[] Ids =
    [
        Guid.Parse("00000000-0000-0000-0000-00000000000a"),
        Guid.Parse("00000000-0000-0000-0000-00000000000b"),
        Guid.Parse("00000000-0000-0000-0000-00000000000c"),
    ];

    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static GroupMemberKindInput Member(int order, params ProjectedTrade[] trades) =>
        new(Ids[order], $"member-{order}", order, Guid.NewGuid(), null,
            new FtmoSimulationInputs.RunProjection(
                null, BacktestSegment.InSample, [.. trades], [.. trades], 0, 0,
                trades.Count(t => t.Outcome == ResizeOutcome.Unscalable)));

    private static (int Proxy, int Reported) Both(params GroupMemberKindInput[] members)
    {
        var p = new GroupParams(
            Jerusalem, Berlin, 10_000m, 0.05m, 0.10m, null, (1m, 1m), new FtmoChallengeRulesDto(0.10m, 0.05m, 4, TimeLimitDays: null));
        var result = ComputeGroup(BacktestRunKind.Deploy, members, p, CancellationToken.None);
        result.Status.Should().Be(FtmoSimulationStatus.Evaluated, "the fixture must reach the replay");

        var series = members.Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!)).ToList();
        var merged = Merge(series, Intersect(series)!.Value);
        return (FtmoPeakConcurrency.Compute(merged), result.Diagnostics!.Peak.PeakConcurrentOpen);
    }

    [Fact]
    public void Parity_OverlappingMembers_EqualsTheReportedPeak()
    {
        var (proxy, reported) = Both(
            Member(0, Trade(9, At(2, 8), At(2, 9)), Trade(0, At(10, 8), At(10, 12)), Trade(1, At(12, 8), At(12, 9)), Trade(2, At(20, 8), At(20, 9))),
            Member(1, Trade(9, At(2, 9), At(2, 10)), Trade(0, At(10, 9), At(10, 13)), Trade(1, At(20, 8), At(20, 9))),
            Member(2, Trade(9, At(2, 10), At(2, 11)), Trade(0, At(10, 10), At(10, 11)), Trade(1, At(20, 8, 30), At(20, 9, 30))));

        reported.Should().Be(3, "the fixture must exercise a real multi-member peak");
        proxy.Should().Be(reported);
    }

    [Fact]
    public void Parity_BoundaryTouchZeroDurationAndUnscalableRows_EqualsTheReportedPeak()
    {
        var (proxy, reported) = Both(
            Member(0, Trade(9, At(2, 8), At(2, 9)), Trade(0, At(10, 8), At(10, 10)), Trade(1, At(11, 8), At(11, 8)), Trade(2, At(12, 8), At(12, 9), net: null)),
            Member(1, Trade(9, At(2, 9), At(2, 10)), Trade(0, At(10, 10), At(10, 12)), Trade(1, At(11, 8), At(11, 9)), Trade(2, At(12, 8), At(12, 9))));

        reported.Should().Be(1, "touching rows and an ignored zero-duration row never overlap");
        proxy.Should().Be(reported);
    }

    [Fact]
    public void Parity_RowsOutsideTheCommonWindow_AreTrimmedExactlyAsTheGroupDoes()
    {
        // Member 0 starts at day 5, member 1 at day 10: A's early rows (a 2-deep overlap) fall outside the window.
        var (proxy, reported) = Both(
            Member(0, Trade(0, At(5, 8), At(5, 12)), Trade(1, At(6, 8), At(6, 12)), Trade(2, At(10, 8), At(10, 10)), Trade(3, At(15, 8), At(15, 9))),
            Member(1, Trade(0, At(10, 9), At(10, 11)), Trade(1, At(14, 8), At(14, 9)), Trade(2, At(15, 8), At(15, 9))));

        proxy.Should().Be(reported);
    }
}
