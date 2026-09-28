using System.Diagnostics;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.1.2-1.1.4 — the benchmark gate (design.md Decision 1; hard rule 7).
/// Times the SHIPPED <see cref="FtmoChallengeRace.RunChain"/> plus <see cref="FtmoBreachEvaluator.Evaluate"/>
/// on each monthly start's series, both FX ends (proxied by evaluating the same suffix twice, since
/// this is a timing proxy, not a correctness fixture), for 2 runs — i.e. what one multi-start request
/// costs. <c>[BenchmarkFact]</c>: skipped unless <c>FTMO_BENCH=1</c>, so this can never pass vacuously.
/// Build in Release and take the median of 3 for a stable reading; run with
/// <c>-c Release -p:BaseOutputPath=bin-scratch/</c>.
/// </summary>
public class FtmoMultiStartBenchmarkTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const decimal Capital = 10_000m;
    private const decimal DailyPct = 0.05m;
    private const decimal MaxPct = 0.10m;
    private static readonly TimeSpan Gate = TimeSpan.FromSeconds(5);

    [BenchmarkFact]
    public void NeverProfile_OneMultiStartRequest_StaysUnderTheFiveSecondGate()
    {
        var elapsed = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Never);

        elapsed.Should().BeLessThanOrEqualTo(
            Gate, "the 'never' profile is the scanner's worst case — it never exits early");
    }

    [BenchmarkFact]
    public void FastProfile_OneMultiStartRequest_StaysUnderTheFiveSecondGate()
    {
        var elapsed = MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile.Fast);

        elapsed.Should().BeLessThanOrEqualTo(Gate);
    }

    /// <summary>Median of 3 runs of the full one-request simulation, per design.md Decision 1.</summary>
    private static TimeSpan MedianOfThree(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var samples = new[]
        {
            MeasureOneRequest(profile),
            MeasureOneRequest(profile),
            MeasureOneRequest(profile),
        };
        Array.Sort(samples);
        return samples[1];
    }

    /// <summary>
    /// One multi-start request's cost: every monthly start's suffix, RunChain, at 2 FX ends, for 2 runs
    /// (design.md Decision 1).
    /// <para>
    /// ftmo-multi-start PR1 apply follow-up (option A): this loop now mirrors the PRODUCTION call shape
    /// the PR4 multi-start service will use — one <see cref="FtmoBreachEvaluator.Evaluate"/> per FX end's
    /// phase-1 series, passed into <see cref="FtmoChallengeRace.RunChain"/>'s precomputed-evaluation
    /// parameter, instead of a second, redundant <c>Evaluate</c> call on the identical series
    /// (<see cref="FtmoChallengeRace.RunPhase"/>'s own phase-1 call would otherwise re-run the evaluator
    /// on exactly this input). The open-day attribution is likewise computed ONCE per suffix (shared by
    /// both simulated FX ends here, since this proxy reuses the same suffix for both — a real FX-band
    /// projection shares open/close instants across ends too, per <see cref="FtmoTradeProjector.Project"/>)
    /// and passed into <c>RunChain</c>'s precomputed-attribution parameter, instead of being rebuilt
    /// per phase per FX end.
    /// </para>
    /// </summary>
    private static TimeSpan MeasureOneRequest(FtmoMultiStartBenchmarkFixture.Profile profile)
    {
        var trades = FtmoMultiStartBenchmarkFixture.Build(profile);
        var starts = FtmoMultiStartBenchmarkFixture.MonthlyStartOpens(trades);

        var sw = Stopwatch.StartNew();
        for (var run = 0; run < 2; run++)
        {
            foreach (var startOpen in starts)
            {
                var suffix = trades
                    .Where(t => t.OpenSource >= startOpen)
                    .OrderBy(t => t.OpenSource)
                    .ThenBy(t => t.RowIndex)
                    .ToArray();

                var attribution = FtmoChallengeRace.AttributeOpenDays(suffix, Jerusalem, Berlin);

                for (var fxEnd = 0; fxEnd < 2; fxEnd++)
                {
                    var phase1Evaluation = FtmoBreachEvaluator.Evaluate(suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct);
                    FtmoChallengeRace.RunChain(
                        suffix, Jerusalem, Berlin, Capital, DailyPct, MaxPct, phase1Evaluation, attribution);
                }
            }
        }

        sw.Stop();
        return sw.Elapsed;
    }
}
