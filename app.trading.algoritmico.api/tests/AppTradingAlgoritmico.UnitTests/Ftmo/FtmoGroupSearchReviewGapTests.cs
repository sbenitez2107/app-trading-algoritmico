using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchRanking;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d Part 0 — the test gaps the slice-1c review found (RELIABILITY-001..005). Capital 10,000; daily
/// allowance 5% (500); max allowance 10% (1,000). Closed-form fixtures, no <see cref="Random"/>.
/// </summary>
public class FtmoGroupSearchReviewGapTests
{
    private static ProjectedTrade OnDay(int row, int day, decimal? net, int month = 1) => Trade(row, At(month, day, 9), At(month, day, 10), net);

    private static FtmoChallengePhaseDto Phase(FtmoPhaseOutcome outcome, DateTime? start, DateTime? close) =>
        new(outcome, start, null, null, close, null, null, null, null, null);

    // ---- RELIABILITY-001: the funded phase is the deepest, and its T2 boundary is exact ----

    [Fact]
    public void StartMaxUsed_WhenTheFundedPhaseIsTheDeepest_ReportsTheFundedDrawdown_WithAnExactT2Boundary()
    {
        // T1 = 6 Jan 10:00, T2 = 12 Jan 10:00.
        var p1 = OnDay(0, 5, -200m);                                          // P1: 0.2
        var p2 = OnDay(1, 8, -300m);                                          // P2
        var endsAtT2 = Trade(2, At(1, 11, 9), At(1, 12, 10), -100m);          // closes AT T2: P2 only
        var zeroAtT2 = Trade(6, At(1, 12, 10), At(1, 12, 10), -100m);         // opens and closes AT T2: only `Close > T2` keeps it out of funded
        var opensAtT2 = Trade(3, At(1, 12, 10), At(1, 13, 10), -250m);        // opens AT T2: funded (Open >= T2 is inclusive)
        var f1 = OnDay(4, 14, -200m);
        var f2 = OnDay(5, 15, -250m);
        ProjectedTrade[] rows = [p1, p2, endsAtT2, zeroAtT2, opensAtT2, f1, f2];
        var merged = new MergedSeries(rows, rows, [], []);

        var start = new FtmoMultiStartRowDto(
            0, At(1, 1), default, default, default, false, null, null,
            Phase(FtmoPhaseOutcome.TargetReachedFirst, At(1, 1), At(1, 6, 10)),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, At(1, 6, 10), At(1, 12, 10)),
            new FtmoFundedPhaseDto(FtmoFundedOutcome.NoBreachByEndOfData, At(1, 12, 10), null, null, null, null, null, null, null),
            default, false);

        // P1 min 9,800 (0.2); P2 -300 -100 -100 = 9,500 (0.5); funded -250 -200 -250 = 9,300 (0.7), deeper than both.
        FtmoLimitHeadroom.StartMaxUsed(merged, start, Params()).Should().Be(0.7m);
    }

    // ---- RELIABILITY-002: BuildEntry pins an exact headroom, per kind, on the worse kind ----

    /// <summary>
    /// Two members, both spanning 2 Jan 09:00 to 20 Jan 12:00 so nothing is trimmed. Deploy loses 200 a day (daily
    /// 0.4, max 0.4, headroom 0.6); Evaluation loses 400 on the 2nd (daily 0.8) and 600 in total (max 0.6, headroom
    /// 0.2). The Evaluation series also differs in timing, so the pair is not an identical Deploy/Evaluation member.
    /// </summary>
    private static FtmoProjectionCache TwoKindPool(bool deployIsTheWorse)
    {
        var deploy = (A: new[] { Trade(0, At(1, 2, 9), At(1, 2, 10), -100m), Trade(1, At(1, 20, 11), At(1, 20, 12), -100m) },
                      B: new[] { Trade(0, At(1, 2, 9), At(1, 2, 11), -100m), Trade(1, At(1, 20, 10), At(1, 20, 12), -100m) });
        var eval = (A: new[] { Trade(0, At(1, 2, 9), At(1, 2, 10), -300m), Trade(1, At(1, 20, 11), At(1, 20, 12), -100m) },
                    B: new[] { Trade(0, At(1, 2, 9), At(1, 2, 12), -100m), Trade(1, At(1, 20, 10), At(1, 20, 12), -100m) });

        return deployIsTheWorse
            ? Cache(new Strat(1, "A", eval.A, deploy.A), new Strat(2, "B", eval.B, deploy.B))
            : Cache(new Strat(1, "A", deploy.A, eval.A), new Strat(2, "B", deploy.B, eval.B));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildEntry_PinsTheHeadroomOfTheWorseKind_WhicheverKindThatIs(bool deployIsTheWorse)
    {
        var cache = TwoKindPool(deployIsTheWorse);
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 2), Params(), 50m);
        var outcome = Simulate(cache, plan.Shortlist, Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);

        var entry = BuildEntry(cache, outcome.Results.Single(), Params());

        entry.Headroom.Should().Be(0.2m, "the worse kind decides: 1 - max(0.8 daily, 0.6 max); the other kind alone would give 0.6");
        entry.Peak.Should().Be(2);
    }

    // ---- RELIABILITY-003: determinism over FULL entries, not only ids ----

    private static IReadOnlyList<RankEntry> Pipeline()
    {
        var cache = Cache([.. Enumerable.Range(1, 5).Select(n => Distinct(
            n, $"SYM{n}",
            [.. Enumerable.Range(0, 12).Select(i => Trade(i, At(1, 1, 9).AddDays(i * 3), At(1, 1, 10).AddDays(i * 3), ((i + n) % 3 == 0) ? -60m * n : 20m))]))]);
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 3), Params(), 50m);
        var outcome = Simulate(cache, plan.Shortlist, Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);
        return [.. outcome.Results.Select(r => BuildEntry(cache, r, Params()))];
    }

    [Fact]
    public void Rank_OverTwoIndependentPipelines_IsIdenticalInEveryEntryField_NotOnlyTheIds()
    {
        var first = Pipeline();
        var second = Pipeline();

        var reference = Rank(first);
        var again = Rank(Enumerable.Reverse(second));

        reference.Should().HaveCount(20);
        reference.Select(r => r.Entry.Headroom).Distinct().Count().Should().BeGreaterThan(1, "an all-equal field would prove nothing");
        again.Should().BeEquivalentTo(reference, o => o.WithStrictOrdering().ComparingRecordsByMembers());
    }

    // ---- RELIABILITY-004: StartMaxUsed against the real evaluator ----

    private static (IReadOnlyList<FtmoMultiStartRowDto> Starts, MergedSeries Merged) Evaluate(params ProjectedTrade[] rows)
    {
        var cache = Cache(new Strat(1, "A", rows, rows));
        var inputs = cache.Rebind([Id(1)], BacktestRunKind.Deploy);
        var result = FtmoGroupComputation.ComputeGroup(BacktestRunKind.Deploy, inputs, Params(), CancellationToken.None);
        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);

        var series = inputs.Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!)).ToList();
        return (result.Run!.Starts, Merge(series, Intersect(series)!.Value));
    }

    private static bool BreachedMax(FtmoMultiStartRowDto s) =>
        new[] { s.Phase1.BreachLimit, s.Phase2.BreachLimit, s.Funded.BreachLimit }
            .Any(l => l is FtmoFirstBreachingLimit.Max or FtmoFirstBreachingLimit.BothSameClose);

    private static bool FundedBreachedMax(FtmoMultiStartRowDto s) =>
        s.Funded.BreachLimit is FtmoFirstBreachingLimit.Max or FtmoFirstBreachingLimit.BothSameClose;

    private static bool BreachedDaily(FtmoMultiStartRowDto s) =>
        new[] { s.Phase1.BreachLimit, s.Phase2.BreachLimit, s.Funded.BreachLimit }.Any(l => l is FtmoFirstBreachingLimit.Daily);

    private static void AssertAgreesWithTheEvaluator(IReadOnlyList<FtmoMultiStartRowDto> starts, MergedSeries merged)
    {
        foreach (var s in starts)
        {
            var used = FtmoLimitHeadroom.StartMaxUsed(merged, s, Params());
            if (BreachedMax(s))
                used.Should().BeGreaterThan(1m, $"start {s.Index} breached the max limit in the evaluator");
            else
                used.Should().BeLessThanOrEqualTo(1m, $"start {s.Index} did not breach the max limit in the evaluator");
        }
    }

    private static ProjectedTrade[] Gentle(int firstRow) =>
        [.. new[] { 1, 2, 4, 5, 6 }.Select((m, i) => OnDay(firstRow + i, 5, -10m, m))];

    [Fact]
    public void StartMaxUsed_AgreesWithTheEvaluator_OnStartsThatBreachedTheMaxLimitInPhase1_AndOnesThatDidNot()
    {
        // Three -400 days in March (each under the 500 daily limit) cross the 1,000 max limit: Jan..Mar starts breach,
        // the April..June starts see only the -10 rows.
        ProjectedTrade[] rows = [.. Gentle(0), OnDay(5, 10, -400m, 3), OnDay(6, 11, -400m, 3), OnDay(7, 12, -400m, 3)];

        var (starts, merged) = Evaluate(rows);

        starts.Count(BreachedMax).Should().BeGreaterThan(0);
        starts.Count(s => !BreachedMax(s)).Should().BeGreaterThan(0);
        AssertAgreesWithTheEvaluator(starts, merged);
    }

    [Fact]
    public void StartMaxUsed_AgreesWithTheEvaluator_OnAStartWhoseFundedPhaseBreachedTheMaxLimit()
    {
        // 5-8 Jan: +300 a day (P1 target). 12-15 Jan: +150 a day (P2 target). 20-22 Jan: -400 a day (funded max breach).
        var rows = new[]
        {
            OnDay(0, 5, 300m), OnDay(1, 6, 300m), OnDay(2, 7, 300m), OnDay(3, 8, 300m),
            OnDay(4, 12, 150m), OnDay(5, 13, 150m), OnDay(6, 14, 150m), OnDay(7, 15, 150m),
            OnDay(8, 20, -400m), OnDay(9, 21, -400m), OnDay(10, 22, -400m),
            OnDay(11, 5, -10m, 3),
        };

        var (starts, merged) = Evaluate(rows);

        starts.Any(FundedBreachedMax).Should().BeTrue("a start's funded phase must breach the max limit for this fixture to prove anything");
        starts.Count(s => !BreachedMax(s)).Should().BeGreaterThan(0);
        AssertAgreesWithTheEvaluator(starts, merged);
    }

    [Fact]
    public void StartMaxUsed_AgreesWithTheEvaluator_OnAStartThatBreachedOnlyTheDailyLimit()
    {
        // A single -600 day breaches the 500 daily limit while the drawdown stays under the 1,000 max.
        ProjectedTrade[] rows = [.. Gentle(0), OnDay(5, 10, -600m, 5)];

        var (starts, merged) = Evaluate(rows);

        starts.Any(s => BreachedDaily(s) && !BreachedMax(s)).Should().BeTrue();
        AssertAgreesWithTheEvaluator(starts, merged);
    }

    // ---- RELIABILITY-005: the proxy peak that ranks equals the full simulation's worse-kind peak ----

    [Fact]
    public void ARankedCandidatesProxyPeak_EqualsTheWorseKindDiagnosticsPeakOfItsFullSimulation()
    {
        // Deploy hours: A 9, B 9, C 13. Evaluation hours: A 9, B 13, C 13. So AB peaks at 2 in Deploy only, BC in
        // Evaluation only, and the proxy has to take the worse of the two.
        ProjectedTrade[] Rows(int hour) =>
            [.. Enumerable.Range(0, 10).Select(i => Trade(i, At(1, 1, hour).AddDays(i * 3), At(1, 1, hour + 1).AddDays(i * 3), 5m))];
        Strat Member(int n, int deployHour, int evalHour) =>
            new(n, $"SYM{n}", Rows(deployHour), [.. Rows(evalHour).Select(t => t with { Net = t.Net + 1m })]);

        var cache = Cache(Member(1, 9, 9), Member(2, 9, 13), Member(3, 13, 13));
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 3), Params(), 50m);
        var outcome = Simulate(cache, plan.Shortlist, Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);

        var ranked = Rank(outcome.Results.Select(r => BuildEntry(cache, r, Params())));

        ranked.Should().HaveCount(4);
        var differing = 0;
        foreach (var r in ranked)
        {
            var peaks = r.Entry.Kinds.Select(k => k.Diagnostics!.Peak.PeakConcurrentOpen).ToList();
            r.Entry.Peak.Should().Be(peaks.Max(), "key 6 ranks on the proxy peak, which must be the worse kind's reported peak");
            differing += peaks.Distinct().Count() > 1 ? 1 : 0;
        }

        differing.Should().BeGreaterThan(0, "a fixture where both kinds peak alike would not separate the worse kind from either one");
    }
}
