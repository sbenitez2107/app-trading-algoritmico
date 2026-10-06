using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b.3.1-1b.3.2 — enumeration order and the three EXACT prunes (instrument cap, pair conflict,
/// optional Academy 1% rule), with funnel counts that always reconcile.
/// </summary>
public class FtmoGroupSearchPrunesTests
{
    // One trade from January <day> to <day + days>; two strategies overlap when those spans overlap.
    private static Strat Span(int n, string symbol, int day, int days = 5) =>
        Distinct(n, symbol, Trade(0, At(1, day, 9), At(1, day + days, 9), 10m));

    private static FtmoGroupSearchEngine.PruneResult Prune(
        Strat[] pool, FtmoGroupSearchEngine.SearchOptions options, Func<string?, FtmoSimulationInputs.SymbolResolution>? resolve = null)
    {
        var cache = Cache(pool);
        var ids = cache.Members.Select(m => m.StrategyId).ToList();
        return FtmoGroupSearchEngine.Prune(cache, ids, resolve ?? (_ => Resolution()), options, 10_000m, 50m);
    }

    private static string Key(int[] combo) => string.Join(",", combo);

    private static void ShouldReconcile(FtmoGroupSearchEngine.PruneResult r)
    {
        var f = r.Funnel;
        (f.RemovedByCap + f.RemovedByPairConflict + f.RemovedByOnePercentRule + f.Remaining).Should().Be(f.Enumerated);
        f.Remaining.Should().Be(r.Survivors.Count);
    }

    // ---- 1b.3.1: enumeration ----

    [Fact]
    public void Enumerate_YieldsEachSizeInLexicographicOrder_SmallestSizeFirst()
    {
        FtmoGroupSearchEngine.Enumerate(4, 2, 3).Select(Key).Should()
            .Equal("0,1", "0,2", "0,3", "1,2", "1,3", "2,3", "0,1,2", "0,1,3", "0,2,3", "1,2,3");
    }

    [Fact]
    public void Enumerate_TheSameInputTwice_GivesAnIdenticalSequence()
    {
        var first = FtmoGroupSearchEngine.Enumerate(9, 2, 4).Select(Key).ToList();
        var second = FtmoGroupSearchEngine.Enumerate(9, 2, 4).Select(Key).ToList();

        second.Should().Equal(first);
        first.Should().HaveCount(36 + 84 + 126);
    }

    [Fact]
    public void Enumerate_ASizeLargerThanThePool_YieldsNothingForThatSize()
    {
        FtmoGroupSearchEngine.Enumerate(2, 2, 4).Should().HaveCount(1);
        FtmoGroupSearchEngine.Enumerate(1, 2, 4).Should().BeEmpty();
    }

    // ---- 1b.3.2: instrument cap ----

    // User decision 2026-10-04: the default cap is 2, so two instruments with two strategies each yield triples and quads.
    [Fact]
    public void Prune_DefaultCap_IsTwo_AndTwoInstrumentsWithTwoStrategiesEachYieldTriplesAndQuads()
    {
        var r = Prune([Span(1, "XAUUSD", 2), Span(2, "XAUUSD", 2), Span(3, "NQ", 2), Span(4, "NQ", 2)], new(2, 4));

        new FtmoGroupSearchEngine.SearchOptions(2, 4).MaxPerInstrument.Should().Be(FtmoGroupSearchLimits.DefaultMaxPerInstrument).And.Be(2);
        r.Survivors.Select(c => c.Length).Distinct().Order().Should().Equal(2, 3, 4);
        r.Survivors.Select(Key).Should().Contain(["0,1,2", "0,1,2,3"]);
        ShouldReconcile(r);
    }

    [Fact]
    public void Prune_AnExplicitCapOfOne_StillYieldsOnlyPairsOnThatPool()
    {
        var r = Prune([Span(1, "XAUUSD", 2), Span(2, "XAUUSD", 2), Span(3, "NQ", 2), Span(4, "NQ", 2)], new(2, 4, MaxPerInstrument: 1));

        r.Survivors.Should().OnlyContain(c => c.Length == 2);
        r.Survivors.Select(Key).Should().Equal("0,2", "0,3", "1,2", "1,3");
        ShouldReconcile(r);
    }

    [Fact]
    public void Prune_ACapOfOne_ForbidsTwoStrategiesOfOneSymbol_AndCountsTheRemoval()
    {
        var r = Prune([Span(1, "EURUSD", 2), Span(2, "EURUSD", 2), Span(3, "GBPUSD", 2)], new(2, 2, MaxPerInstrument: 1));

        r.Survivors.Select(Key).Should().Equal("0,2", "1,2");
        r.Funnel.Should().Be(new FtmoGroupSearchEngine.Funnel(3, 1, 0, 0, 2));
        ShouldReconcile(r);
    }

    [Fact]
    public void Prune_CapOfTwo_AdmitsTheSameSymbolPair()
    {
        var r = Prune([Span(1, "EURUSD", 2), Span(2, "EURUSD", 2), Span(3, "GBPUSD", 2)], new(2, 2, MaxPerInstrument: 2));

        r.Survivors.Select(Key).Should().Contain("0,1");
        r.Funnel.RemovedByCap.Should().Be(0);
        r.Funnel.Remaining.Should().Be(3);
    }

    [Fact]
    public void Prune_TheCapIsVerbatim_SoDifferentCasesAreDifferentInstruments()
    {
        var r = Prune([Span(1, "EURUSD", 2), Span(2, "eurusd", 2)], new(2, 2));

        r.Survivors.Should().HaveCount(1);
        r.Funnel.RemovedByCap.Should().Be(0);
    }

    // ---- 1b.3.2: pair conflict ----

    [Fact]
    public void Prune_ADisjointPair_IsRemovedAsAPairConflict()
    {
        var r = Prune([Span(1, "EURUSD", 2, 3), Span(2, "GBPUSD", 20, 3), Span(3, "USDJPY", 3, 3)], new(2, 2));

        r.Survivors.Select(Key).Should().Equal("0,2");
        r.Funnel.Should().Be(new FtmoGroupSearchEngine.Funnel(3, 0, 2, 0, 1));
        ShouldReconcile(r);
    }

    [Fact]
    public void Prune_DifferentSourceZones_AreAPairConflict()
    {
        FtmoSimulationInputs.SymbolResolution Resolve(string? s) => Resolution() with { SourceZone = s == "GBPUSD" ? Berlin : Jerusalem };

        var r = Prune([Span(1, "EURUSD", 2), Span(2, "GBPUSD", 2), Span(3, "USDJPY", 2)], new(2, 2), Resolve);

        r.Funnel.RemovedByPairConflict.Should().Be(2);
        r.Survivors.Select(Key).Should().Equal("0,2");
    }

    [Fact]
    public void Prune_PairwiseOverlap_IsExactlyAnIntersectOverTheWholeCandidate_Helly()
    {
        // Pairwise-overlapping 1-D intervals always share a point, so the pair prune must equal Intersect != null.
        var cache = Cache(Span(1, "A", 1, 10), Span(2, "B", 5, 10), Span(3, "C", 9, 10), Span(4, "D", 14, 5), Span(5, "E", 25, 3));
        var ids = cache.Members.Select(m => m.StrategyId).ToList();

        var survivors = FtmoGroupSearchEngine.Prune(cache, ids, _ => Resolution(), new(3, 3, MaxPerInstrument: 3), 10_000m, 50m)
            .Survivors.Select(Key).ToHashSet();

        foreach (var combo in FtmoGroupSearchEngine.Enumerate(5, 3, 3))
        {
            var series = combo.Select((idx, i) =>
            {
                var rows = cache.Input(ids[idx], BacktestRunKind.Deploy).Projection!.ProjectedLow!;
                return new MemberSeries(i, rows, rows);
            }).ToList();

            survivors.Contains(Key(combo)).Should().Be(Intersect(series) is not null, "combo {0}", Key(combo));
        }
    }

    // ---- 1b.3.2: Academy 1% rule ----

    [Fact]
    public void Prune_OnePercentRule_RemovesKThreeAndUp_AndKeepsKTwo_AtRisk50Capital10000()
    {
        var pool = new[] { Span(1, "A", 2), Span(2, "B", 2), Span(3, "C", 2), Span(4, "D", 2) };

        var r = Prune(pool, new(2, 4, OnePercentRule: true));

        r.Survivors.Should().OnlyContain(c => c.Length == 2).And.HaveCount(6);
        r.Funnel.Should().Be(new FtmoGroupSearchEngine.Funnel(11, 0, 0, 5, 6));
        ShouldReconcile(r);
    }

    [Fact]
    public void Prune_OnePercentRuleOff_RemovesNothingByThatRule()
    {
        var r = Prune([Span(1, "A", 2), Span(2, "B", 2), Span(3, "C", 2), Span(4, "D", 2)], new(2, 4));

        r.Funnel.RemovedByOnePercentRule.Should().Be(0);
        r.Funnel.Remaining.Should().Be(11);
    }

    [Fact]
    public void Prune_ACandidateBreakingTwoRules_IsCountedOnceUnderTheFirstRule()
    {
        // The two share a symbol (cap) AND are disjoint (pair): counted under the cap only.
        var r = Prune([Span(1, "EURUSD", 2, 2), Span(2, "EURUSD", 20, 2)], new(2, 2, MaxPerInstrument: 1));

        r.Funnel.Should().Be(new FtmoGroupSearchEngine.Funnel(1, 1, 0, 0, 0));
    }
}
