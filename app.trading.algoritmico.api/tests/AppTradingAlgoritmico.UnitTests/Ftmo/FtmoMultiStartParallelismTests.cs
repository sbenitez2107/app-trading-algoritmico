using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2 (orchestrator decision 2026-10-04): the per-start loop of
/// <see cref="FtmoMultiStartReadService.ComputeRun"/> runs in parallel. Every start is a pure function of
/// shared READ-ONLY inputs, results go to a pre-sized array indexed by start, so the output must be
/// identical for any degree of parallelism and across repeated runs.
/// </summary>
public class FtmoMultiStartParallelismTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly FtmoChallengeRulesDto Rules = new(0.10m, 0.05m, 4, TimeLimitDays: null);

    /// <summary>
    /// 600 rows over ~5 years with a deterministic mix of wins and losses (no Random), so starts end in
    /// different outcomes: breaches, targets, funded phases and censored runs.
    /// </summary>
    private static List<ProjectedTrade> MixedSeries()
        => [.. FtmoMultiStartBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Never)
            .Take(600)
            .Select((t, i) => t with { Net = ((i * 37 % 11) - 4) * 55m, Outcome = ResizeOutcome.OnTarget })];

    private static FtmoMultiStartRunDto Run(IReadOnlyList<ProjectedTrade> trades, int? degree, CancellationToken ct = default)
        => FtmoMultiStartReadService.ComputeRun(
            Guid.Empty, BacktestRunKind.Deploy, BacktestSegment.InSample, trades, trades, Jerusalem, Berlin,
            10_000m, 0.05m, 0.10m, profitTargetPct: null, fxBand: (1m, 1m), unscalableCount: 0, Rules, ct, degree);

    [Fact]
    public void ComputeRun_OneThreadAndManyThreads_ProduceIdenticalOutput_StartsInOrder()
    {
        var series = MixedSeries();

        var sequential = Run(series, degree: 1);
        var parallel = Run(series, degree: Environment.ProcessorCount);

        sequential.Starts.Count.Should().BeGreaterThan(40, "an empty comparison would prove nothing");
        sequential.Starts.Select(s => s.Outcome).Distinct().Count().Should().BeGreaterThan(1, "the fixture must mix outcomes");
        parallel.Should().BeEquivalentTo(sequential, o => o.ComparingRecordsByMembers().WithStrictOrdering());
        parallel.Starts.Select(s => s.Index).Should().Equal(Enumerable.Range(0, parallel.Starts.Count));
    }

    [Fact]
    public void ComputeRun_RepeatedParallelRuns_AreIdentical()
    {
        var series = MixedSeries();
        var first = Run(series, degree: null);

        for (var i = 0; i < 4; i++)
            Run(series, degree: null).Should().BeEquivalentTo(first, o => o.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void ComputeRun_ACancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => Run(MixedSeries(), degree: null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ComputeRun_ADegreeBelowOne_Throws()
    {
        var act = () => Run(MixedSeries(), degree: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Case (b) of the B2 apply report re-checked against the runway fix at HEAD: phase 2 reaches its target on the
    /// LAST trade, so the funded phase has no trades. It must report FundedNoBreachAtEndOfData with zero runway,
    /// never throw.
    /// </summary>
    [Fact]
    public void ComputeRun_PhaseTwoTargetReachedOnTheLastTrade_ReportsAnEmptyFundedPhaseWithoutThrowing()
    {
        static ProjectedTrade T(int row, int day, decimal net)
            => new(row, new DateTime(2026, 1, day, 8, 0, 0), new DateTime(2026, 1, day, 9, 0, 0), net, ResizeOutcome.OnTarget, 1m);
        var trades = new List<ProjectedTrade>
        {
            T(0, 12, 300m), T(1, 13, 300m), T(2, 14, 300m), T(3, 15, 300m),
            T(4, 16, 150m), T(5, 17, 150m), T(6, 18, 150m), T(7, 19, 150m),
        };

        var run = Run(trades, degree: 1);

        var row = run.Starts.Single();
        row.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        row.Outcome.Should().Be(FtmoChainOutcome.FundedNoBreachAtEndOfData);
        row.RunwayCalendarDays.Should().Be(0);
    }
}
