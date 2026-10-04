using System.Diagnostics;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Xunit.Abstractions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.4 (design.md D7, hard rule 5) — the group benchmark gate. Times the merge and the
/// group computation (<see cref="FtmoGroupComputation.ComputeGroup"/>: window, trim, global renumbering, then
/// the SHIPPED <c>ComputeRun</c>) for BOTH run kinds, i.e. the whole CPU cost of one group request, on k members
/// of ~1,000 trades each. <c>[BenchmarkFact]</c>: skipped unless <c>FTMO_BENCH=1</c>, so it can never pass
/// vacuously. Run in Release, median of 3, 5 s gate:
/// <c>FTMO_BENCH=1 dotnet test ... -c Release -p:BaseOutputPath=bin-scratch/ --filter FtmoGroupBenchmark</c>.
/// <para>
/// The gate asserts at <see cref="FtmoGroupSimulationLimits.MaxMembers"/>, so raising that constant forces a new
/// measurement.
/// </para>
/// <para>
/// <b>Measured table (B2.4.3), 'never' profile median of 3, BOTH kinds, Release, 12 logical cores, seconds.</b>
/// BEFORE per-start parallelism (sequential <c>ComputeRun</c>), two runs: k=1 2.768/3.131, k=2 4.499/6.176,
/// k=3 11.164/13.472, k=4 11.387/17.583, k=6 21.126/34.323, k=8 54.498/55.207, k=10 82.696/100.076 (cap would
/// have been none: k=2 failed run 2).
/// AFTER per-start parallelism (<c>Parallel.For</c>, degree = processor count), two runs:
/// <code>
///  k        1      2      3      4      6      8       10
///  run 1    0.438  1.665  2.018  3.785  7.081  10.682  16.635
///  run 2    0.463  1.454  2.154  3.468  6.831  9.575   16.055
/// </code>
/// The largest k of {2,4,6,8,10} within 5 s in both runs is 4, hence <c>MaxMembers = 4</c> (k = 1 and 3 are
/// informational). The 'fast' profile is not gated (about 2.5x the 'never' cost).
/// </para>
/// </summary>
public class FtmoGroupBenchmarkTests(ITestOutputHelper output)
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly TimeSpan Gate = TimeSpan.FromSeconds(5);
    private static readonly int[] SweepMemberCounts = [2, 4, 6, 8, 10];

    private static readonly GroupParams Params = new(
        Jerusalem, Berlin, InitialCapital: 10_000m, DailyPct: 0.05m, MaxPct: 0.10m, ProfitTargetPct: null,
        EchoFxBand: (1m, 1m), new FtmoChallengeRulesDto(0.10m, 0.05m, 4, TimeLimitDays: null));

    [BenchmarkFact]
    public void NeverProfile_AtMaxMembers_BothKinds_StaysUnderTheFiveSecondGate()
    {
        var elapsed = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Never, FtmoGroupSimulationLimits.MaxMembers);
        output.WriteLine($"MEDIAN never k={FtmoGroupSimulationLimits.MaxMembers} = {elapsed.TotalSeconds:F3}s");

        elapsed.Should().BeLessThanOrEqualTo(
            Gate, "the 'never' profile is the worst case: it never exits early, so every start scans its whole suffix");
    }

    /// <summary>
    /// The 'fast' profile is exercised and REPORTED at the cap, but it is not gated: the spec gates the 'never'
    /// profile only, and 'fast' reaches the targets and runs the funded phase too, so it costs more per start.
    /// </summary>
    [BenchmarkFact]
    public void FastProfile_AtMaxMembers_BothKinds_IsMeasuredAndReported()
    {
        var elapsed = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Fast, FtmoGroupSimulationLimits.MaxMembers);
        output.WriteLine($"MEDIAN fast k={FtmoGroupSimulationLimits.MaxMembers} = {elapsed.TotalSeconds:F3}s (not gated)");

        elapsed.Should().BeGreaterThan(TimeSpan.Zero);
    }

    /// <summary>Measurement only (B2.4.3): prints the 'never' median per k. It asserts nothing about the gate.</summary>
    [BenchmarkFact]
    public void Sweep_PrintsTheNeverMedianPerMemberCount()
    {
        foreach (var k in SweepMemberCounts)
        {
            var never = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Never, k);
            output.WriteLine($"SWEEP k={k} never={never.TotalSeconds:F3}s");

            never.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    /// <summary>Informational only: k = 1 and k = 3 sit outside the cap rule's {2,4,6,8,10} set but show the curve between its points.</summary>
    [BenchmarkFact]
    public void Sweep_Informational_OneAndThreeMembers()
    {
        foreach (var k in new[] { 1, 3 })
        {
            var never = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Never, k);
            output.WriteLine($"INFO k={k} never={never.TotalSeconds:F3}s");

            never.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    private static TimeSpan MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile profile, int memberCount)
    {
        var samples = new[]
        {
            MeasureOneRequest(profile, memberCount),
            MeasureOneRequest(profile, memberCount),
            MeasureOneRequest(profile, memberCount),
        };
        Array.Sort(samples);
        return samples[1];
    }

    /// <summary>One group request's CPU: <c>ComputeGroup</c> for Deploy then Evaluation over the same k members.</summary>
    private static TimeSpan MeasureOneRequest(FtmoMultiStartBenchmarkFixture.Profile profile, int memberCount)
    {
        var members = FtmoGroupBenchmarkFixture.Build(profile, memberCount);
        var deploy = ToInputs(members);
        var evaluation = ToInputs(members);

        var sw = Stopwatch.StartNew();
        var deployResult = ComputeGroup(BacktestRunKind.Deploy, deploy, Params, CancellationToken.None);
        var evaluationResult = ComputeGroup(BacktestRunKind.Evaluation, evaluation, Params, CancellationToken.None);
        sw.Stop();

        // A refused kind would make the timing meaningless (it would skip the replay).
        deployResult.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        evaluationResult.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        return sw.Elapsed;
    }

    private static List<GroupMemberKindInput> ToInputs(IReadOnlyList<FtmoGroupMerger.MemberSeries> members)
        => [.. members.Select(m => new GroupMemberKindInput(
            Guid.NewGuid(), $"member-{m.MemberOrder}", m.MemberOrder, Guid.NewGuid(), Refusal: null,
            new FtmoSimulationInputs.RunProjection(
                null, BacktestSegment.InSample, [.. m.Low], [.. m.High], 0, 0, 0)))];
}
