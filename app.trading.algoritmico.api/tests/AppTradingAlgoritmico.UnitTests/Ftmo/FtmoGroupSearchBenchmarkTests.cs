using System.Diagnostics;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Xunit.Abstractions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d.2 (design D5, hard rule 6) — the search benchmark. <c>[BenchmarkFact]</c>: skipped unless
/// <c>FTMO_BENCH=1</c>, so it can never pass vacuously. Release, median of 3, on the closed-form P = 24 pool
/// (<see cref="FtmoGroupSearchBenchmarkFixture"/>):
/// <c>FTMO_BENCH=1 dotnet test ... -c Release -p:BaseOutputPath=bin-scratch/ --filter FtmoGroupSearchBenchmark</c>.
/// <para>
/// Gate: the PROXY STAGE (eligibility, enumeration with the exact prunes, the parallel proxy over all 12,926
/// candidates, the shortlist) at P = 24 stays within 60 s. Also measured: the full group computation of ONE candidate
/// (both kinds, the shipped <c>ComputeGroup</c>) at k = 2, 3, 4, and the proxy stage at larger pools, which sets
/// <see cref="FtmoGroupSearchLimits.MaxPoolSize"/>.
/// </para>
/// <para><b>Measured table (1d.2.3), Release, 12 logical cores, closed-form pool, two runs, seconds.</b>
/// <code>
///  proxy stage, P=24, 12,926 candidates, MEDIAN of 3 (the gate, 60 s):   run 1  56.081    run 2  39.204
///  proxy stage, one sample per pool size (informational):
///    P=16 (2,500)      run 1   7.160    run 2  11.057
///    P=24 (12,926)     run 1  37.604    run 2  60.868
///    P=32 (41,416)     run 1 183.505    run 2 203.126
///    P=40 (102,050)    run 1 485.375    run 2 355.796
///  full computation of ONE candidate, both kinds, median of 3:
///    k=2               run 1   1.733    run 2   1.487
///    k=3               run 1   2.914    run 2   1.934
///    k=4               run 1   2.376    run 2   3.476
/// </code>
/// The machine is noisy (the same P=24 pass measured 37.6, 39.2, 56.1 and 60.9 s in four samples), so the gate is a
/// MEDIAN and its margin is thin: about 4.3 ms per candidate at the worst median, and one single P=24 sample (60.9 s)
/// was already over the gate. Eligibility and the prunes
/// are negligible (under 0.2 s even at P=40); the time is the parallel proxy.
/// </para>
/// <para>
/// <b>Constants derived (design D5):</b> <c>MaxPoolSize = 24</c> (P=32 and P=40 are 3x to 8x over the gate; P=25 would
/// project to 67 s at the worst median). <c>ShortlistSize = 150</c> and <c>DefaultMaxFullSimulations = 150</c>:
/// 150 x 3.5 s (the slowest per-candidate median seen, k=4 in run 2) = 525 s. <c>DefaultMaxWallClock = 15 min</c> (900 s)
/// leaves 1.7x headroom for a slower machine. <see cref="FtmoGroupSearchLimitsTests"/> pins these relations.
/// </para>
/// </summary>
public class FtmoGroupSearchBenchmarkTests(ITestOutputHelper output)
{
    private static readonly TimeSpan ProxyGate = TimeSpan.FromSeconds(60);
    private static readonly SearchOptions Options = new(2, 4);

    private readonly record struct StageTimes(TimeSpan Prune, TimeSpan Proxy, TimeSpan Total, int Candidates);

    private static StageTimes MeasureProxyStage(FtmoProjectionCache cache)
    {
        var p = Params();
        var sw = Stopwatch.StartNew();
        var eligibility = EvaluateEligibility(cache, _ => Resolution(), includeIdentical: false);
        var pruned = Prune(cache, eligibility.Eligible, _ => Resolution(), Options, p.InitialCapital, 50m);
        var prune = sw.Elapsed;

        var proxies = ProxyAll(cache, eligibility.Eligible, pruned.Survivors, p);
        var proxy = sw.Elapsed - prune;
        var shortlist = Shortlist(pruned.Survivors, proxies, Options);
        sw.Stop();

        // A stage that removed everything would be fast and meaningless.
        proxies.Should().OnlyContain(x => x.Removal == null);
        shortlist.Should().HaveCount(Options.ShortlistSize);
        return new StageTimes(prune, proxy, sw.Elapsed, pruned.Survivors.Count);
    }

    private static StageTimes MedianOfThree(FtmoProjectionCache cache)
    {
        var samples = new[] { MeasureProxyStage(cache), MeasureProxyStage(cache), MeasureProxyStage(cache) };
        return samples.OrderBy(s => s.Total).ElementAt(1);
    }

    [BenchmarkFact]
    public void ProxyStage_AtP24_OverTwelveThousandNineHundredTwentySixCandidates_StaysUnderTheSixtySecondGate()
    {
        var cache = FtmoGroupSearchBenchmarkFixture.BuildCache();

        var median = MedianOfThree(cache);
        output.WriteLine(
            $"MEDIAN proxy stage P=24 candidates={median.Candidates} total={median.Total.TotalSeconds:F3}s "
            + $"(eligibility+prune={median.Prune.TotalSeconds:F3}s, proxy={median.Proxy.TotalSeconds:F3}s)");

        median.Candidates.Should().Be(12_926);
        median.Total.Should().BeLessThanOrEqualTo(ProxyGate, "the proxy stage over a P=24 pool must stay within the spec's 60 s gate");
    }

    /// <summary>Measurement only: one sample per pool size, to see where the C(P, 2..4) pass crosses the gate.</summary>
    [BenchmarkFact]
    public void Sweep_PrintsTheProxyStageTimePerPoolSize()
    {
        foreach (var poolSize in new[] { 16, 24, 32, 40 })
        {
            var cache = FtmoGroupSearchBenchmarkFixture.BuildCache(poolSize);
            var t = MeasureProxyStage(cache);
            output.WriteLine(
                $"SWEEP P={poolSize} candidates={t.Candidates} total={t.Total.TotalSeconds:F3}s "
                + $"(prune={t.Prune.TotalSeconds:F3}s, proxy={t.Proxy.TotalSeconds:F3}s, {t.Proxy.TotalMilliseconds * 1000 / t.Candidates:F1} us/candidate)");

            t.Total.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    /// <summary>The full group computation of one candidate: <c>Simulate</c> over a one-candidate shortlist, both kinds.</summary>
    [BenchmarkFact]
    public void FullSimulation_PerCandidate_AtK2K3K4_IsMeasuredAndReported()
    {
        var cache = FtmoGroupSearchBenchmarkFixture.BuildCache();
        foreach (var k in new[] { 2, 3, 4 })
        {
            var candidate = new ShortlistedCandidate([.. cache.Members.Take(k).Select(m => m.StrategyId)], 0m, 0);
            var samples = new List<TimeSpan>();
            for (var i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                var outcome = Simulate(cache, [candidate], Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);
                sw.Stop();

                outcome.Results.Single().Kinds.Should().OnlyContain(x => x.Status == FtmoSimulationStatus.Evaluated);
                samples.Add(sw.Elapsed);
            }

            var median = samples.Order().ElementAt(1);
            output.WriteLine($"FULLSIM k={k} per candidate (both kinds) median={median.TotalSeconds:F3}s samples={string.Join("/", samples.Select(s => s.TotalSeconds.ToString("F3")))}");

            median.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }
}
