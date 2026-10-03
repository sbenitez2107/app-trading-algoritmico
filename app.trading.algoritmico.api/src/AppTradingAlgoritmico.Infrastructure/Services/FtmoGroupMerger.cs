using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B1 (design.md D2) — merges several members' projected series into ONE
/// globally renumbered series per FX end, so the shipped engine (<c>FtmoMultiStartReadService.ComputeRun</c>
/// and everything under it) can run a group unchanged. <c>internal static</c>, pure: no I/O.
/// <para>
/// <b>Why the renumbering is mandatory.</b> Every member's per-run <c>RowIndex</c> runs 0..n-1, so two
/// members collide on it. The engine keys by <c>RowIndex</c>: <c>FtmoChallengeRace.AttributeOpenDays</c>
/// (one dictionary entry per row, shared by both FX ends) and the evaluator's "self" skip in
/// <c>HasConcurrentOpenPosition</c>. A merge that kept the per-run indices would silently drop a member's
/// trading days and hide cross-member concurrency, with no error.
/// </para>
/// </summary>
internal static class FtmoGroupMerger
{
    /// <summary>
    /// One member's projected rows at both FX ends. <see cref="MemberOrder"/> is the member's index in
    /// <see cref="OrderMembers"/> and must be unique within one merge, in <c>[0, members.Count)</c>.
    /// <c>Low[i]</c> and <c>High[i]</c> come from the same source trade (<c>ProjectRun</c>), so they are paired by position.
    /// </summary>
    internal sealed record MemberSeries(int MemberOrder, IReadOnlyList<ProjectedTrade> Low, IReadOnlyList<ProjectedTrade> High);

    internal readonly record struct GroupWindow(DateTime Start, DateTime End);

    /// <summary>Where a merged row came from: its member and the row index it had in its own run.</summary>
    internal readonly record struct RowOrigin(int MemberOrder, int OriginalRowIndex);

    /// <param name="Low">Merged low-FX series; <c>RowIndex == position</c>.</param>
    /// <param name="High">Merged high-FX series; renumbered identically to <paramref name="Low"/>.</param>
    /// <param name="RowMap">Index == merged <c>RowIndex</c>.</param>
    /// <param name="InWindowCountByMember">Index == <see cref="MemberSeries.MemberOrder"/>.</param>
    internal sealed record MergedSeries(
        IReadOnlyList<ProjectedTrade> Low,
        IReadOnlyList<ProjectedTrade> High,
        IReadOnlyList<RowOrigin> RowMap,
        IReadOnlyList<int> InWindowCountByMember);

    /// <summary>
    /// The group is a SET: members are deduplicated and sorted by <c>StrategyId</c> ascending, so the
    /// position in the result is the <see cref="MemberSeries.MemberOrder"/> and the output never depends
    /// on the order the caller listed them in.
    /// </summary>
    internal static IReadOnlyList<Guid> OrderMembers(IEnumerable<Guid> strategyIds)
    {
        ArgumentNullException.ThrowIfNull(strategyIds);
        return strategyIds.Distinct().Order().ToList();
    }

    /// <summary>
    /// <c>[max over members of first Open, min over members of last Close]</c>, over ALL of each member's
    /// projected rows (Unscalable included, so a range is the member's real data span). <c>null</c> means
    /// empty: no members, a member with no rows, or <c>max first Open &gt; min last Close</c>. A window whose
    /// two ends coincide is zero-width, not empty.
    /// </summary>
    internal static GroupWindow? Intersect(IReadOnlyList<MemberSeries> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (members.Count == 0 || members.Any(m => m.Low.Count == 0))
            return null;

        var start = members.Max(m => m.Low.Min(t => t.OpenSource));
        var end = members.Min(m => m.Low.Max(t => t.CloseSource));

        return start > end ? null : new GroupWindow(start, end);
    }

    /// <summary>
    /// Trims every member to <paramref name="window"/> (a row survives iff <c>Open &gt;= Start</c> and
    /// <c>Close &lt;= End</c>; Unscalable rows included), orders the survivors by
    /// <c>(OpenSource, MemberOrder, OriginalRowIndex, position)</c> and renumbers them <c>0..N-1</c> on BOTH
    /// FX ends. Surviving trades are not re-sized: the trim runs AFTER projection, because Â is a property of
    /// the member's whole run. Throws <see cref="InvalidOperationException"/> when a member's low and high
    /// series disagree on length, <c>RowIndex</c>, <c>OpenSource</c> or <c>CloseSource</c> at any position.
    /// </summary>
    internal static MergedSeries Merge(IReadOnlyList<MemberSeries> members, GroupWindow window)
    {
        ArgumentNullException.ThrowIfNull(members);

        var seen = new HashSet<int>();
        foreach (var member in members)
        {
            if (member.MemberOrder < 0 || member.MemberOrder >= members.Count || !seen.Add(member.MemberOrder))
            {
                throw new ArgumentException(
                    $"MemberOrder {member.MemberOrder} must be unique and within [0, {members.Count}).", nameof(members));
            }

            AssertPaired(member);
        }

        var kept = new List<(MemberSeries Member, int Position)>();
        var counts = new int[members.Count];
        foreach (var member in members)
        {
            for (var i = 0; i < member.Low.Count; i++)
            {
                var trade = member.Low[i];
                if (trade.OpenSource < window.Start || trade.CloseSource > window.End)
                    continue;

                kept.Add((member, i));
                counts[member.MemberOrder]++;
            }
        }

        var ordered = kept
            .OrderBy(k => k.Member.Low[k.Position].OpenSource)
            .ThenBy(k => k.Member.MemberOrder)
            .ThenBy(k => k.Member.Low[k.Position].RowIndex)
            .ThenBy(k => k.Position)
            .ToList();

        var low = new List<ProjectedTrade>(ordered.Count);
        var high = new List<ProjectedTrade>(ordered.Count);
        var rowMap = new List<RowOrigin>(ordered.Count);
        for (var r = 0; r < ordered.Count; r++)
        {
            var (member, position) = ordered[r];
            low.Add(member.Low[position] with { RowIndex = r });
            high.Add(member.High[position] with { RowIndex = r });
            rowMap.Add(new RowOrigin(member.MemberOrder, member.Low[position].RowIndex));
        }

        return new MergedSeries(low, high, rowMap, counts);
    }

    private static void AssertPaired(MemberSeries member)
    {
        if (member.Low.Count != member.High.Count)
        {
            throw new InvalidOperationException(
                $"Member {member.MemberOrder}: low has {member.Low.Count} rows but high has {member.High.Count}.");
        }

        for (var i = 0; i < member.Low.Count; i++)
        {
            var lo = member.Low[i];
            var hi = member.High[i];
            if (lo.RowIndex != hi.RowIndex || lo.OpenSource != hi.OpenSource || lo.CloseSource != hi.CloseSource)
            {
                throw new InvalidOperationException(
                    $"Member {member.MemberOrder}: low and high disagree at position {i} on RowIndex/OpenSource/CloseSource.");
            }
        }
    }
}
