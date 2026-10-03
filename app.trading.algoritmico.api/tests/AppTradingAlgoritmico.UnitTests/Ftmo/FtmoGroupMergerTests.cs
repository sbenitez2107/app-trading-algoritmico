using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B1 (design.md D2, tasks hard rule 3) — the pure group merger. Every member's
/// per-run <c>RowIndex</c> runs 0..n-1, so two members COLLIDE on the index. The shipped engine keys
/// <c>AttributeOpenDays</c> and the evaluator's "self" skip by <c>RowIndex</c>, so a merge that does not
/// renumber globally silently drops one member's trading days and its concurrency. These tests drive the
/// merged series through the UNEDITED shipped race/evaluator to prove both effects.
/// </summary>
public class FtmoGroupMergerTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Outside any DST-mismatch window for Jerusalem/Berlin.
    private static DateTime At(int day, int hour, int minute = 0) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net = 10m, decimal lots = 1m)
    {
        return new ProjectedTrade(
            rowIndex, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, net is null ? 0m : lots);
    }

    /// <summary>High mirrors Low's instants and rows; only the nets differ (the FX high end).</summary>
    private static MemberSeries Member(int order, params ProjectedTrade[] low) =>
        new(order, low, low.Select(t => t with { Net = t.Net * 2m }).ToList());

    /// <summary>What a merge WITHOUT global renumbering would hand to the engine: a plain concatenation by Open.</summary>
    private static List<ProjectedTrade> ConcatKeepingOriginalIndices(params MemberSeries[] members) =>
        members.SelectMany(m => m.Low).OrderBy(t => t.OpenSource).ToList();

    // ---- B1.2.1: globally unique, gapless RowIndex ----

    [Fact]
    public void Merge_TwoMembersWithDuplicateRowIndices_YieldsUniqueGaplessMergedIndices()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)), Trade(2, At(13, 8), At(13, 9)));
        var b = Member(1, Trade(0, At(12, 9, 30), At(12, 9, 45)), Trade(1, At(13, 10), At(13, 11)), Trade(2, At(14, 8), At(14, 9)));
        var window = new GroupWindow(At(12, 8), At(14, 9));

        var merged = Merge([a, b], window);

        merged.Low.Should().HaveCount(6);
        merged.Low.Select(t => t.RowIndex).Should().Equal(0, 1, 2, 3, 4, 5);
        merged.High.Select(t => t.RowIndex).Should().Equal(0, 1, 2, 3, 4, 5);
        merged.Low.Select(t => t.RowIndex).Should().OnlyHaveUniqueItems();
    }

    // ---- B1.2.2: row map, counts, ordering ----

    [Fact]
    public void Merge_RowMap_RoundTripsEveryMergedRowToItsMemberAndOriginalRow()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 9), net: 11m), Trade(1, At(12, 10), At(12, 11), net: 12m), Trade(2, At(13, 8), At(13, 9), net: 13m));
        var b = Member(1, Trade(0, At(12, 9, 30), At(12, 9, 45), net: 21m), Trade(1, At(13, 10), At(13, 11), net: 22m), Trade(2, At(14, 8), At(14, 9), net: 23m));
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 8), At(14, 9)));

        merged.RowMap.Should().HaveCount(merged.Low.Count);
        for (var r = 0; r < merged.Low.Count; r++)
        {
            var origin = merged.RowMap[r];
            var source = members[origin.MemberOrder].Low.Single(t => t.RowIndex == origin.OriginalRowIndex);
            merged.Low[r].Should().Be(source with { RowIndex = r });
            var sourceHigh = members[origin.MemberOrder].High.Single(t => t.RowIndex == origin.OriginalRowIndex);
            merged.High[r].Should().Be(sourceHigh with { RowIndex = r });
        }
    }

    [Fact]
    public void Merge_PerMemberCounts_MatchTheRowsKeptForEachMember()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)));
        var b = Member(1, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)), Trade(2, At(12, 12), At(12, 13)));
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 8), At(12, 13)));

        // Every row fits; B's row 2 closes exactly at the window end and is kept.
        merged.InWindowCountByMember.Should().Equal(2, 3);
        merged.RowMap.Count(o => o.MemberOrder == 0).Should().Be(2);
        merged.RowMap.Count(o => o.MemberOrder == 1).Should().Be(3);
    }

    [Fact]
    public void Merge_SameOpenTie_BreaksByMemberThenOriginalRow()
    {
        var a = Member(0, Trade(3, At(12, 8), At(12, 9)), Trade(4, At(12, 8), At(12, 10)));
        var b = Member(1, Trade(0, At(12, 8), At(12, 9)));
        var members = new[] { b, a }; // input order must not matter

        var merged = Merge(members, new GroupWindow(At(12, 8), At(12, 10)));

        merged.RowMap.Should().Equal(new RowOrigin(0, 3), new RowOrigin(0, 4), new RowOrigin(1, 0));
    }

    [Fact]
    public void Merge_OverlappingMembers_InterleaveByOpen()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 12)), Trade(1, At(12, 12), At(12, 14)));
        var b = Member(1, Trade(0, At(12, 9), At(12, 13)), Trade(1, At(12, 11), At(12, 15)));
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 8), At(12, 15)));

        merged.RowMap.Select(o => (o.MemberOrder, o.OriginalRowIndex))
            .Should().Equal((0, 0), (1, 0), (1, 1), (0, 1));
        merged.Low.Select(t => t.OpenSource).Should().BeInAscendingOrder();
    }

    // ---- B1.2.3: low/high identical renumbering ----

    [Fact]
    public void Merge_LowAndHigh_CarryTheSameRowIndexOpenAndCloseAtEveryPosition()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)));
        var b = Member(1, Trade(0, At(12, 9), At(12, 10)), Trade(1, At(12, 10), At(12, 12)));
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 8), At(12, 12)));

        merged.Low.Should().HaveSameCount(merged.High);
        for (var i = 0; i < merged.Low.Count; i++)
        {
            merged.High[i].RowIndex.Should().Be(merged.Low[i].RowIndex);
            merged.High[i].OpenSource.Should().Be(merged.Low[i].OpenSource);
            merged.High[i].CloseSource.Should().Be(merged.Low[i].CloseSource);
            merged.High[i].Net.Should().Be(merged.Low[i].Net * 2m, "the high end keeps its own net, only the indices are shared");
        }
    }

    [Fact]
    public void Merge_LowHighDisagreeOnRowIndex_Throws()
    {
        var low = new[] { Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)) };
        var high = new[] { low[0], low[1] with { RowIndex = 7 } };
        var member = new MemberSeries(0, low, high);

        var act = () => Merge([member], new GroupWindow(At(12, 8), At(12, 11)));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_LowHighDisagreeOnOpenSource_Throws()
    {
        var low = new[] { Trade(0, At(12, 8), At(12, 9)) };
        var high = new[] { low[0] with { OpenSource = At(12, 7) } };

        var act = () => Merge([new MemberSeries(0, low, high)], new GroupWindow(At(12, 7), At(12, 9)));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_LowHighDisagreeOnCloseSource_Throws()
    {
        var low = new[] { Trade(0, At(12, 8), At(12, 9)) };
        var high = new[] { low[0] with { CloseSource = At(12, 10) } };

        var act = () => Merge([new MemberSeries(0, low, high)], new GroupWindow(At(12, 8), At(12, 10)));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_LowHighDifferInLength_Throws()
    {
        var low = new[] { Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)) };
        var high = new[] { low[0] };

        var act = () => Merge([new MemberSeries(0, low, high)], new GroupWindow(At(12, 8), At(12, 11)));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_MismatchOnARowOutsideTheWindow_StillThrows()
    {
        // The pairing is a wiring invariant over the whole member, not only over the kept rows.
        var low = new[] { Trade(0, At(12, 8), At(12, 9)), Trade(1, At(20, 8), At(20, 9)) };
        var high = new[] { low[0], low[1] with { RowIndex = 9 } };

        var act = () => Merge([new MemberSeries(0, low, high)], new GroupWindow(At(12, 8), At(12, 9)));

        act.Should().Throw<InvalidOperationException>();
    }

    // ---- B1.2.4: trading days (both members counted) ----

    private static (MemberSeries A, MemberSeries B) TradingDaysFixture() => (
        Member(0, Trade(5, At(12, 10), At(12, 11))),   // member A, Berlin day D1
        Member(1, Trade(5, At(13, 10), At(13, 11))));  // member B, SAME RowIndex 5, Berlin day D2

    [Fact]
    public void Merge_TradingDays_BothMembersAreAttributedThroughTheShippedRace()
    {
        var (a, b) = TradingDaysFixture();
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 10), At(13, 11)));
        var attribution = FtmoChallengeRace.AttributeOpenDays(merged.Low, Jerusalem, Berlin);
        var cache = new FtmoChallengeRace.CachedOpenDays(merged.Low, Jerusalem, Berlin, attribution);
        var days = attribution.Values.OrderBy(d => d).ToList();

        attribution.Should().HaveCount(2, "each member's trade owns its own merged RowIndex");
        days.Should().Equal(new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 13));
        cache.CountOpenDays(days[0], days[1], At(14, 0)).Should().Be(2, "both members' trading days are counted");
    }

    [Fact]
    public void Concatenation_WithoutRenumbering_SilentlyCountsOneTradingDay()
    {
        // Falsification: what the engine would see if the merge kept the per-run RowIndex.
        var (a, b) = TradingDaysFixture();
        var colliding = ConcatKeepingOriginalIndices(a, b);

        var attribution = FtmoChallengeRace.AttributeOpenDays(colliding, Jerusalem, Berlin);
        var cache = new FtmoChallengeRace.CachedOpenDays(colliding, Jerusalem, Berlin, attribution);

        attribution.Should().HaveCount(1, "the two members' RowIndex 5 collapse into a single key");
        var firstDay = new DateOnly(2026, 1, 12);
        cache.CountOpenDays(firstDay, new DateOnly(2026, 1, 13), At(14, 0)).Should().Be(1, "one trading day is silently lost");
    }

    // ---- B1.2.5: concurrency across members ----

    private static (MemberSeries A, MemberSeries B) ConcurrencyFixture() => (
        // A loses 600 on a 10,000 account (> 5% daily): breaching close at 12:00.
        Member(0, Trade(5, At(12, 10), At(12, 12), net: -300m)),
        // B is open from 11:00 to 13:00: it spans A's breaching close. Same RowIndex 5 as A. Its own close
        // recovers the balance (high end +600), so A's close is the only breach and it is NOT a clean one.
        Member(1, Trade(5, At(12, 11), At(12, 13), net: 300m)));

    private static FtmoBreachEvaluator.FtmoBreachEvaluation Evaluate(IReadOnlyList<ProjectedTrade> trades) =>
        FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initialCapital: 10_000m, dailyPct: 0.05m, maxPct: 0.30m);

    [Fact]
    public void Merge_Concurrency_TheEvaluatorSeesTheOtherMembersOpenPosition()
    {
        var (a, b) = ConcurrencyFixture();
        var members = new[] { a, b };

        var merged = Merge(members, new GroupWindow(At(12, 10), At(12, 13)));
        var result = Evaluate(merged.High); // high end: A's net is -600 (2 x -300)

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Daily.Causes.Should().Contain(BreachContingencyCause.ConcurrentOpenPosition);
    }

    [Fact]
    public void Merge_Concurrency_EqualsTheSameTradesWithDistinctIndices()
    {
        var (a, b) = ConcurrencyFixture();
        var members = new[] { a, b };
        var merged = Merge(members, new GroupWindow(At(12, 10), At(12, 13)));
        var distinct = new[]
        {
            a.High[0] with { RowIndex = 0 },
            b.High[0] with { RowIndex = 1 },
        };

        var viaMerge = Evaluate(merged.High);
        var viaDistinct = Evaluate(distinct);

        viaMerge.Daily.Verdict.Should().Be(viaDistinct.Daily.Verdict);
        viaMerge.Daily.Causes.Should().Equal(viaDistinct.Daily.Causes);
        viaMerge.Max.Verdict.Should().Be(viaDistinct.Max.Verdict);
    }

    [Fact]
    public void Concatenation_WithoutRenumbering_SilentlyLosesTheConcurrentOpenPosition()
    {
        // Falsification: with the colliding RowIndex 5 the evaluator's "self" skip hides the sibling,
        // and the breach is reported as a clean Breached instead of BreachContingent.
        var (a, b) = ConcurrencyFixture();
        var colliding = ConcatKeepingOriginalIndices(
            new MemberSeries(0, a.High, a.High), new MemberSeries(1, b.High, b.High));

        var result = Evaluate(colliding);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
        result.Daily.Causes.Should().BeEmpty("the concurrency is silently missed");
    }

    // ---- B1.2.6: Intersect / trim ----

    [Fact]
    public void Intersect_Window_IsMaxOfFirstOpensToMinOfLastCloses_UnscalableRowsIncluded()
    {
        var a = Member(0, Trade(0, At(10, 8), At(10, 9)), Trade(1, At(20, 8), At(21, 9)));
        // B's earliest row (Jan 12) is Unscalable; it still defines B's first open.
        var b = Member(1, Trade(0, At(12, 8), At(12, 9), net: null), Trade(1, At(15, 8), At(18, 9)));

        var window = Intersect([a, b]);

        window.Should().NotBeNull();
        window!.Value.Start.Should().Be(At(12, 8), "max of first opens (A: Jan 10, B: Jan 12)");
        window.Value.End.Should().Be(At(18, 9), "min of last closes (A: Jan 21, B: Jan 18)");
    }

    [Fact]
    public void Intersect_NonOverlappingRanges_ReturnsNull()
    {
        var a = Member(0, Trade(0, At(10, 8), At(11, 9)));
        var b = Member(1, Trade(0, At(15, 8), At(16, 9)));

        Intersect([a, b]).Should().BeNull();
    }

    [Fact]
    public void Intersect_RangesTouchingAtOneInstant_IsAZeroWidthWindowNotEmpty()
    {
        var a = Member(0, Trade(0, At(10, 8), At(12, 9)));
        var b = Member(1, Trade(0, At(12, 9), At(14, 9)));

        var window = Intersect([a, b]);

        window.Should().Be(new GroupWindow(At(12, 9), At(12, 9)));
    }

    [Fact]
    public void Intersect_NoMembersOrAMemberWithoutRows_ReturnsNull()
    {
        Intersect([]).Should().BeNull();
        Intersect([Member(0, Trade(0, At(10, 8), At(11, 9))), Member(1)]).Should().BeNull();
    }

    [Fact]
    public void Merge_Trim_KeepsARowIffOpenAtOrAfterStartAndCloseAtOrBeforeEnd()
    {
        var start = At(12, 8);
        var end = At(12, 18);
        var rows = new[]
        {
            Trade(0, At(12, 7, 59), At(12, 9)),    // opened before start: dropped
            Trade(1, start, At(12, 9)),            // Open == Start: kept
            Trade(2, At(12, 10), end),             // Close == End: kept
            Trade(3, At(12, 17), At(12, 18, 1)),   // straddles the end: dropped
            Trade(4, At(12, 19), At(12, 20)),      // after the window: dropped
            Trade(5, At(12, 11), At(12, 12), net: null), // Unscalable inside: kept
        };

        var merged = Merge([Member(0, rows)], new GroupWindow(start, end));

        merged.RowMap.Select(o => o.OriginalRowIndex).Should().BeEquivalentTo([1, 2, 5]);
        merged.InWindowCountByMember.Should().Equal(3);
    }

    [Fact]
    public void Merge_Trim_DropsStraddlingAndEarlyRowsOfEveryMember()
    {
        var a = Member(0, Trade(0, At(10, 8), At(10, 9)), Trade(1, At(12, 9), At(12, 10)), Trade(2, At(12, 11), At(14, 9)));
        var b = Member(1, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 12)), Trade(2, At(13, 8), At(13, 9)));
        var members = new[] { a, b };
        var window = Intersect(members)!.Value; // [12th 08:00, min(14th 09:00, 13th 09:00) = 13th 09:00]

        var merged = Merge(members, window);

        window.Should().Be(new GroupWindow(At(12, 8), At(13, 9)));
        merged.RowMap.Should().BeEquivalentTo(new[]
        {
            new RowOrigin(0, 1),
            new RowOrigin(1, 0),
            new RowOrigin(1, 1),
            new RowOrigin(1, 2),
        });
        merged.InWindowCountByMember.Should().Equal(1, 3);
    }

    [Fact]
    public void Merge_SurvivingTrades_KeepTheirSizingAndNetUntouched()
    {
        var low = new[] { Trade(0, At(12, 8), At(12, 9), net: 37.5m, lots: 0.7m), Trade(1, At(12, 10), At(12, 11), net: null) };
        var member = Member(0, low);

        var merged = Merge([member], new GroupWindow(At(12, 8), At(12, 11)));

        merged.Low.Should().Equal(low[0] with { RowIndex = 0 }, low[1] with { RowIndex = 1 });
        merged.Low[0].FtmoLots.Should().Be(0.7m);
        merged.Low[0].Net.Should().Be(37.5m);
        merged.Low[1].Net.Should().BeNull();
        merged.Low[1].Outcome.Should().Be(ResizeOutcome.Unscalable);
    }

    // ---- member order: ascending StrategyId, independent of request order ----

    [Fact]
    public void OrderMembers_IsAscendingStrategyIdDeduplicatedAndIndependentOfRequestOrder()
    {
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var mid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var high = Guid.Parse("ffffffff-0000-0000-0000-000000000000");

        var forward = OrderMembers([low, mid, high]);
        var shuffled = OrderMembers([high, low, mid, low]);

        forward.Should().Equal(low, mid, high);
        shuffled.Should().Equal(forward);
    }

    [Fact]
    public void Merge_InputListOrder_DoesNotChangeTheOutput()
    {
        var a = Member(0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 10), At(12, 11)));
        var b = Member(1, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 9), At(12, 12)));
        var window = new GroupWindow(At(12, 8), At(12, 12));

        var forward = Merge([a, b], window);
        var reversed = Merge([b, a], window);

        reversed.Low.Should().Equal(forward.Low);
        reversed.High.Should().Equal(forward.High);
        reversed.RowMap.Should().Equal(forward.RowMap);
        reversed.InWindowCountByMember.Should().Equal(forward.InWindowCountByMember);
    }

    [Fact]
    public void Merge_Intersect_InputListOrder_DoesNotChangeTheWindow()
    {
        var a = Member(0, Trade(0, At(10, 8), At(20, 9)));
        var b = Member(1, Trade(0, At(12, 8), At(18, 9)));

        Intersect([b, a]).Should().Be(Intersect([a, b]));
    }
}
