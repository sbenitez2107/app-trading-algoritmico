using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchRanking;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search D5/D9 — the shortlist is SIMULATED interleaved by size (k=2 #1, k=3 #1, k=4 #1, k=2 #2, ...) so a
/// budget stop leaves every size represented. The order only decides what is simulated first; the final ranking is
/// computed independently of it.
/// </summary>
public class FtmoGroupSearchInterleaveTests
{
    /// <summary>A candidate of <paramref name="size"/> members, tagged by <paramref name="rank"/> (its position within its size).</summary>
    private static ShortlistedCandidate Candidate(int size, int rank)
        => new([.. Enumerable.Range(0, size).Select(i => Id(rank * 10 + i + 1))], 0m, 0);

    private static List<ShortlistedCandidate> BestFirst(int size, int count)
        => [.. Enumerable.Range(1, count).Select(rank => Candidate(size, rank))];

    private static string Tag(ShortlistedCandidate c) => $"k{c.MemberIds.Count}#{Convert.ToInt32(c.MemberIds[0].ToString()[..8], 16) / 10}";

    private static string[] Tags(IEnumerable<ShortlistedCandidate> list) => [.. list.Select(Tag)];

    [Fact]
    public void InterleaveBySize_AlternatesTheSizesBestFirst()
    {
        var global = new[] { Candidate(2, 1), Candidate(2, 2), Candidate(3, 1), Candidate(4, 1), Candidate(3, 2), Candidate(4, 2) };

        Tags(InterleaveBySize(global)).Should().Equal("k2#1", "k3#1", "k4#1", "k2#2", "k3#2", "k4#2");
    }

    [Fact]
    public void InterleaveBySize_UnevenSizes_ContinuesWithTheSizesThatRemain()
    {
        List<ShortlistedCandidate> global = [.. BestFirst(2, 5), .. BestFirst(3, 5), .. BestFirst(4, 3)];

        Tags(InterleaveBySize(global)).Should().Equal(
            "k2#1", "k3#1", "k4#1", "k2#2", "k3#2", "k4#2", "k2#3", "k3#3", "k4#3", "k2#4", "k3#4", "k2#5", "k3#5");
    }

    [Fact]
    public void InterleaveBySize_AMissingSize_IsSkippedWithoutGaps()
    {
        List<ShortlistedCandidate> global = [.. BestFirst(2, 2), .. BestFirst(4, 2)];

        Tags(InterleaveBySize(global)).Should().Equal("k2#1", "k4#1", "k2#2", "k4#2");
    }

    [Fact]
    public void InterleaveBySize_IsAPermutation_StableAndDeterministic()
    {
        List<ShortlistedCandidate> global = [.. BestFirst(3, 4), .. BestFirst(2, 4), .. BestFirst(4, 2)];

        var first = InterleaveBySize(global);

        first.Should().HaveCount(global.Count).And.BeEquivalentTo(global);
        Tags(InterleaveBySize(global)).Should().Equal(Tags(first));
        InterleaveBySize([]).Should().BeEmpty();
    }

    // ---- through the real plan, simulation and ranking ----

    private static ProjectedTrade[] Rows(int seed) =>
        [.. Enumerable.Range(0, 30).Select(i =>
            Trade(i, At(1, 1, 9).AddDays(i * 3), At(1, 1, 10).AddDays(i * 3), ((i + seed) % 4 == 0) ? -40m : 15m))];

    private static FtmoProjectionCache Pool(int size) =>
        Cache([.. Enumerable.Range(1, size).Select(n => Distinct(n, $"SYM{n}", Rows(n)))]);

    [Fact]
    public void Plan_ListsTheShortlistInterleavedBySize()
    {
        var plan = Plan(Pool(5), _ => Resolution(), new SearchOptions(2, 4), Params(), 50m);

        plan.Shortlist.Select(c => c.MemberIds.Count).Take(6).Should().Equal(2, 3, 4, 2, 3, 4);
        plan.Shortlist.Should().HaveCount(25, "C(5,2) + C(5,3) + C(5,4) all fit the default shortlist");
    }

    [Fact]
    public void Simulate_ABudgetStopOfThree_SimulatesOneGroupPerSize()
    {
        var cache = Pool(5);
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 4), Params(), 50m);

        var outcome = Simulate(cache, plan.Shortlist, Params(), new SimulationBudget(3), null, CancellationToken.None);

        outcome.Stop.Should().Be(SearchStopReason.MaxFullSimulations);
        outcome.Results.Select(r => r.Candidate.MemberIds.Count).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Ranking_IsUnchangedByTheSimulationOrder()
    {
        var cache = Pool(5);
        var plan = Plan(cache, _ => Resolution(), new SearchOptions(2, 4), Params(), 50m);
        var interleaved = plan.Shortlist;
        var grouped = interleaved.OrderBy(c => c.MemberIds.Count).ToList();   // the old global-by-size order, size blocks
        grouped.Select(c => c.MemberIds.Count).Should().NotEqual(interleaved.Select(c => c.MemberIds.Count), "the two orders must differ");

        string[] RankOf(IReadOnlyList<ShortlistedCandidate> order)
        {
            var outcome = Simulate(cache, order, Params(), new SimulationBudget(int.MaxValue), null, CancellationToken.None);
            return [.. Rank(outcome.Results.Select(r => BuildEntry(cache, r, Params())))
                .Select(r => string.Join(",", r.Entry.MemberIds.Select(g => g.ToString()[..8])) + $"/{r.Entry.Headroom}/{r.Entry.Peak}")];
        }

        RankOf(interleaved).Should().Equal(RankOf(grouped));
    }
}
