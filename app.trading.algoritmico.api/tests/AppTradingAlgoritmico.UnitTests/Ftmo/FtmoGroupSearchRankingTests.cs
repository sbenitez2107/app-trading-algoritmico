using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchRanking;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1c.2 (design D5) — the exact lexicographic order. Every key test pits a WINNER that is better
/// on that key alone and WORSE on every later key against a base candidate, so only that key can decide the order.
/// Shares are fractions of 100 starts; no epsilon anywhere.
/// </summary>
public class FtmoGroupSearchRankingTests
{
    private sealed record Spec(
        int Breached = 10, decimal Headroom = 0.5m, int? Funded = 50, int? Median = 100,
        int[]? Members = null, int Peak = 3, int Undecided = 0, bool Evaluated = true, FtmoChallengeRaceRefusal? Race = null)
    {
        internal int[] Ids => Members ?? [1, 2, 3];
    }

    private static FtmoGroupKindResultDto Kind(Spec s, BacktestRunKind kind = BacktestRunKind.Deploy)
    {
        if (!s.Evaluated)
            return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Refused, FtmoGroupRefusal.NoCommonWindow, [], null, [], Run: null);

        FtmoOutcomeCountDto Count(FtmoChainOutcome o, int n) => new(o, false, n, n / 100m);
        FtmoOrderStatisticsDto Days(int? m) => new(m is null ? 0 : 1, m, m, m, m, m);
        var summary = new FtmoMultiStartSummaryDto(
            100,
            [
                Count(FtmoChainOutcome.Phase1UndecidedAtEndOfData, s.Undecided),
                Count(FtmoChainOutcome.Phase1Breached, s.Breached),
                .. s.Funded is { } f ? new[] { Count(FtmoChainOutcome.FundedNoBreachAtEndOfData, f) } : [],
            ],
            0, Days(null), Days(null), Days(s.Median), Days(null), Days(null), Days(null));
        var run = new FtmoMultiStartRunDto(
            Guid.Empty, kind, BacktestSegment.Unknown, FtmoSimulationStatus.Evaluated, null, s.Race, null, FtmoStartGrain.Monthly,
            new FtmoChallengeRulesDto(10m, 5m, 4, null), [], summary, [], false, null, null, 0, [], string.Empty);
        return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Evaluated, null, [], null, [], run);
    }

    private static RankEntry Entry(Spec s, Spec? other = null) =>
        new([.. s.Ids.Select(Id)], s.Peak, [Kind(s), Kind(other ?? s, BacktestRunKind.Evaluation)], s.Headroom);

    private static string Describe(RankEntry e) => string.Join(",", e.MemberIds.Select(i => i.ToString()[..8])) + $"/{e.Headroom}/{e.Peak}";

    private static int Cmp(Spec a, Spec b) => Comparer.Compare(Entry(a), Entry(b));

    private static void AssertWinnerFirst(Spec winner, Spec loser)
    {
        Cmp(winner, loser).Should().BeLessThan(0);
        Cmp(loser, winner).Should().BeGreaterThan(0);
        Rank([Entry(loser), Entry(winner)]).Select(r => Describe(r.Entry)).Should().Equal(Describe(Entry(winner)), Describe(Entry(loser)));
    }

    private static readonly Spec Base = new();

    // Every later key is made WORSE on the winner, so only the key under test can decide.
    private static readonly Spec Worse = new(Breached: 99, Headroom: 0.1m, Funded: 10, Median: 500, Members: [1, 2, 3, 4], Peak: 9);

    // ---- key 1: both kinds evaluated ----

    [Fact]
    public void Key1_AnEvaluatedCandidate_RanksBeforeARefusedOne_EvenWithWorseMetrics()
    {
        var refused = new Spec(Breached: 0, Headroom: 1m, Funded: 100, Median: 1, Evaluated: false);
        AssertWinnerFirst(Worse with { Members = [7, 8] }, refused with { Members = [1, 2] });
    }

    [Fact]
    public void Key1_ARaceRefusal_ZeroValuedEnumMember_RanksAfterAnEvaluatedCandidate()
    {
        var raced = new Spec(Breached: 0, Race: FtmoChallengeRaceRefusal.ProfitTargetMismatch, Members: [1, 2]);
        AssertWinnerFirst(Worse with { Members = [7, 8] }, raced);
    }

    [Fact]
    public void Key1_ARefusedKindOnEitherSide_Demotes_AndRefusedCandidatesFollowIdOrderWithTheirRefusal()
    {
        var clean = Entry(Worse with { Members = [7, 8] });
        var oneRefused = new RankEntry([Id(1), Id(2)], 1, [Kind(Base), Kind(new Spec(Evaluated: false), BacktestRunKind.Evaluation)], 0.9m);
        var bothRefused = new RankEntry([Id(3), Id(4)], 1, [Kind(new Spec(Evaluated: false)), Kind(new Spec(Evaluated: false), BacktestRunKind.Evaluation)], 0.9m);

        var ranked = Rank([bothRefused, oneRefused, clean]);

        ranked.Select(r => r.Entry.MemberIds[0]).Should().Equal(Id(7), Id(1), Id(3));
        ranked[1].Entry.Kinds.Should().Contain(k => k.Refusal == FtmoGroupRefusal.NoCommonWindow);
    }

    // ---- key 2: breach share, lowest first, worse kind ----

    [Fact]
    public void Key2_LowerBreachShare_Wins_WhateverTheLaterKeysSay() =>
        AssertWinnerFirst(Worse with { Breached = 9 }, Base);

    [Fact]
    public void Key2_UsesTheWorseKind_AndAnUndecidedOutcomeIsNotABreach()
    {
        // X: breaches 5 and 20 -> worse 20. Y: 12 and 12 -> worse 12. Y wins although X has the lower best kind.
        Cmp(new Spec(Breached: 12), new Spec(Breached: 20, Members: [1, 2, 4])).Should().BeLessThan(0);
        Comparer.Compare(Entry(new Spec(Breached: 12)), Entry(new Spec(Breached: 5, Members: [1, 2, 4]), new Spec(Breached: 20, Members: [1, 2, 4])))
            .Should().BeLessThan(0);

        // Phase1UndecidedAtEndOfData is the zero-valued enum member: 30 of them must not count as breaches.
        AssertWinnerFirst(Worse with { Breached = 1, Undecided = 30 }, new Spec(Breached: 2));
    }

    // ---- key 3: headroom, highest first ----

    [Fact]
    public void Key3_HigherHeadroom_Wins() =>
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0.6m }, Base);

    [Fact]
    public void Key3_AZeroHeadroom_IsStillOrderedAgainstNegative_NoTruthiness() =>
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0m }, Base with { Headroom = -0.2m });

    // ---- key 4: funded no-breach share, highest first, missing last ----

    [Fact]
    public void Key4_HigherFundedNoBreachShare_Wins() =>
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0.5m, Funded = 60 }, Base);

    [Fact]
    public void Key4_UsesTheWorseKind_TheLowerShare()
    {
        var x = Entry(new Spec(Funded: 90), new Spec(Funded: 20));
        var y = Entry(new Spec(Funded: 40, Members: [1, 2, 4]));

        Comparer.Compare(y, x).Should().BeLessThan(0);
    }

    [Fact]
    public void Key4_AZeroShare_BeatsAMissingOne()
    {
        // Funded 0 means zero of 100 starts ended funded without a breach; null means the outcome row is absent.
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0.5m, Funded = 0 }, Base with { Funded = null });
    }

    // ---- key 5: days to both targets, lowest first, missing last ----

    [Fact]
    public void Key5_FewerMedianDaysToBothTargets_Wins() =>
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0.5m, Funded = 50, Median = 90 }, Base);

    [Fact]
    public void Key5_AMissingMedian_RanksLast_AndTheWorseKindIsTheLargerMedian()
    {
        AssertWinnerFirst(Worse with { Breached = 10, Headroom = 0.5m, Funded = 50, Median = 900 }, Base with { Median = null });

        var x = Entry(new Spec(Median: 50), new Spec(Median: 300));
        var y = Entry(new Spec(Median: 200, Members: [1, 2, 4]));
        Comparer.Compare(y, x).Should().BeLessThan(0);
    }

    // ---- key 6: member count, then peak, then ids ----

    [Fact]
    public void Key6_FewerMembers_WinsBeforePeakAndIds() =>
        AssertWinnerFirst(new Spec(Members: [8, 9, 10], Peak: 9), new Spec(Members: [1, 2, 3, 4], Peak: 1));

    [Fact]
    public void Key6_LowerPeak_Wins_AtEqualSize() =>
        AssertWinnerFirst(new Spec(Members: [7, 8, 9], Peak: 2), new Spec(Members: [1, 2, 3], Peak: 3));

    [Fact]
    public void Key6_TheSortedGuidSequence_BreaksTheLastTie_AndTheComparerIsTotal()
    {
        AssertWinnerFirst(new Spec(Members: [1, 2, 3]), new Spec(Members: [1, 2, 4]));

        // Member order inside an entry never matters: the sequence is sorted first.
        Cmp(new Spec(Members: [3, 1, 2]), new Spec(Members: [1, 2, 3])).Should().Be(0);
        Cmp(Base, Base).Should().Be(0);
    }

    // ---- ties, determinism, ceiling ----

    [Fact]
    public void ThreeMembers_RankBeforeFourMembers_WhenEqualOnKeys1To5() =>
        AssertWinnerFirst(new Spec(Members: [5, 6, 7]), new Spec(Members: [1, 2, 3, 4]));

    [Fact]
    public void Rank_IsIdenticalForAnyInputOrder()
    {
        var specs = new[]
        {
            Base, Worse, Base with { Members = [1, 2, 4] }, Base with { Breached = 3 }, Base with { Headroom = 0.9m },
            Base with { Funded = null }, Base with { Median = 40 }, new Spec(Evaluated: false, Members: [2, 3]),
            Base with { Members = [2, 3, 4], Peak = 1 },
        };
        var entries = specs.Select(s => Entry(s)).ToList();
        var reference = Rank(entries).Select(r => string.Join(",", r.Entry.MemberIds.Order())).ToList();

        for (var shift = 1; shift < entries.Count; shift++)
        {
            var rotated = entries.Skip(shift).Concat(entries.Take(shift)).ToList();
            Rank(rotated).Select(r => string.Join(",", r.Entry.MemberIds.Order())).Should().Equal(reference);
        }

        Rank(Enumerable.Reverse(entries)).Select(r => string.Join(",", r.Entry.MemberIds.Order())).Should().Equal(reference);
    }

    [Fact]
    public void Rank_ARefusedKindCandidate_RanksAfterACleanWorseOne() =>
        AssertWinnerFirst(Worse with { Members = [9, 10] }, new Spec(Breached: 0, Headroom: 1m, Evaluated: false, Members: [1, 2]));

    [Fact]
    public void Ceiling_FlagsRowsWithinTheShareCeiling_AndNeverReordersOrDropsRows()
    {
        var specs = new[] { new Spec(Breached: 4, Members: [1, 2]), new Spec(Breached: 5, Members: [3, 4]), new Spec(Breached: 6, Members: [5, 6]) };

        var ranked = Rank(specs.Select(s => Entry(s)));

        ranked.Select(r => r.WithinCeiling).Should().Equal(true, true, false);
        ranked.Should().HaveCount(3);
        Rank(specs.Select(s => Entry(s)), ceiling: 0.04m).Select(r => r.WithinCeiling).Should().Equal(true, false, false);
        Rank(specs.Select(s => Entry(s)), ceiling: 0.04m).Select(r => r.Entry.MemberIds[0]).Should().Equal(Id(1), Id(3), Id(5));
        Rank([Entry(new Spec(Evaluated: false))]).Single().WithinCeiling.Should().BeFalse("a refused candidate has no share to be within");
    }

    // ---- wiring: Simulate -> BuildEntry -> Rank ----

    [Fact]
    public void BuildEntry_ComputesHeadroomOnTheCandidateWindow_AndRankKeepsEveryShortlistedCandidate()
    {
        var cache = Cache([.. Enumerable.Range(1, 4).Select(n => Distinct(
            n, $"SYM{n}",
            [.. Enumerable.Range(0, 12).Select(i => Trade(i, At(1, 1, 9).AddDays(i * 3), At(1, 1, 10).AddDays(i * 3), ((i + n) % 3 == 0) ? -60m : 20m))]))]);
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 2), Params(), 50m);
        var outcome = Simulate(cache, plan.Shortlist, Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);

        var entries = outcome.Results.Select(r => BuildEntry(cache, r, Params())).ToList();
        var ranked = Rank(entries);

        ranked.Should().HaveCount(plan.Shortlist.Count);
        entries.Should().OnlyContain(e => e.Headroom <= 1m);
        entries.Zip(outcome.Results).Should().OnlyContain(
            z => z.First.Headroom <= 1m - z.Second.Candidate.DailyUsed,
            "headroom never exceeds what the proxy's daily-used fraction leaves");
        ranked.Select(r => Describe(r.Entry)).Should().Equal(Rank(Enumerable.Reverse(entries)).Select(r => Describe(r.Entry)));
    }
}
