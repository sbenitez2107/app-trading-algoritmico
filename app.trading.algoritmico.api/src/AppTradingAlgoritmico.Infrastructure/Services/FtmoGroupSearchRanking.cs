using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D5 — the deterministic, total ranking of the simulated candidates. <c>internal static</c>, pure.
/// Exact decimals, no epsilon, no randomness. The keys, in order:
/// (1) both kinds evaluated with no race refusal and a summary; (2) breach share, lowest first, worse kind;
/// (3) headroom, highest first; (4) funded no-breach share, highest first, worse kind, missing last; (5) median days
/// to both targets, lowest first, worse kind, missing last; (6) member count, then peak concurrency, then the sorted
/// <c>StrategyId</c> sequence, all ascending. Candidates that fail key 1 are ordered by the id sequence alone.
/// </summary>
internal static class FtmoGroupSearchRanking
{
    /// <summary>The default elimination ceiling on the breach share: a highlight, never a ranking key.</summary>
    internal const decimal DefaultEliminationCeiling = 0.05m;

    /// <param name="Peak">The worse peak concurrency over both kinds.</param>
    /// <param name="Headroom">The rank scalar of <see cref="FtmoLimitHeadroom.Rank"/>; unused when key 1 fails.</param>
    internal sealed record RankEntry(IReadOnlyList<Guid> MemberIds, int Peak, IReadOnlyList<FtmoGroupKindResultDto> Kinds, decimal Headroom);

    /// <param name="WithinCeiling">The breach share (worse kind) is at or under the ceiling. Never reorders or drops a row.</param>
    internal sealed record RankedEntry(RankEntry Entry, bool WithinCeiling);

    private readonly record struct Keys(
        bool Evaluated, decimal BreachShare, decimal? FundedShare, int? MedianDays, Guid[] SortedIds);

    internal static readonly IComparer<RankEntry> Comparer = System.Collections.Generic.Comparer<RankEntry>.Create(Compare);

    internal static IReadOnlyList<RankedEntry> Rank(IEnumerable<RankEntry> entries, decimal ceiling = DefaultEliminationCeiling)
        => [.. entries.OrderBy(e => e, Comparer).Select(e => new RankedEntry(e, KeysOf(e) is { Evaluated: true } k && k.BreachShare <= ceiling))];

    /// <summary>
    /// Builds the entry of one simulated candidate: its headroom is recomputed per kind on the candidate's own merged
    /// window (the same shipped <c>Intersect</c> + <c>Merge</c> as the computation), over the kind's own start rows.
    /// </summary>
    internal static RankEntry BuildEntry(FtmoProjectionCache cache, FtmoGroupSearchEngine.CandidateResult result, GroupParams p)
    {
        var headrooms = new List<FtmoLimitHeadroom.KindHeadroom>();
        foreach (var kind in result.Kinds.Where(k => k.Run is not null))
        {
            var series = cache.Rebind(result.Candidate.MemberIds, kind.Kind)
                .Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!))
                .ToList();
            if (Intersect(series) is not { } window)
                continue;

            headrooms.Add(FtmoLimitHeadroom.Compute(Merge(series, window), kind.Run!.Starts, cache.DayOf, p));
        }

        var headroom = headrooms.Count == 0 ? 0m : FtmoLimitHeadroom.Rank(headrooms);
        return new RankEntry(result.Candidate.MemberIds, result.Candidate.Peak, result.Kinds, headroom);
    }

    private static int Compare(RankEntry? x, RankEntry? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var a = KeysOf(x);
        var b = KeysOf(y);

        var c = b.Evaluated.CompareTo(a.Evaluated);
        if (c != 0)
            return c;

        if (!a.Evaluated)
            return CompareIds(a.SortedIds, b.SortedIds);

        c = a.BreachShare.CompareTo(b.BreachShare);
        if (c != 0)
            return c;

        c = y.Headroom.CompareTo(x.Headroom);
        if (c != 0)
            return c;

        c = CompareMissingLast(a.FundedShare, b.FundedShare, descending: true);
        if (c != 0)
            return c;

        c = CompareMissingLast(a.MedianDays, b.MedianDays);
        if (c != 0)
            return c;

        c = a.SortedIds.Length.CompareTo(b.SortedIds.Length);
        if (c != 0)
            return c;

        c = x.Peak.CompareTo(y.Peak);
        return c != 0 ? c : CompareIds(a.SortedIds, b.SortedIds);
    }

    /// <summary>A missing value is last whichever way the present values are ordered.</summary>
    private static int CompareMissingLast<T>(T? a, T? b, bool descending = false) where T : struct, IComparable<T>
    {
        if (a is null || b is null)
            return (a is null).CompareTo(b is null);

        return descending ? b.Value.CompareTo(a.Value) : a.Value.CompareTo(b.Value);
    }

    private static int CompareIds(Guid[] a, Guid[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0)
                return c;
        }

        return a.Length.CompareTo(b.Length);
    }

    private static Keys KeysOf(RankEntry e)
    {
        var ids = e.MemberIds.Order().ToArray();
        var evaluated = e.Kinds.Count > 0 && e.Kinds.All(k =>
            k.Status == FtmoSimulationStatus.Evaluated && k.Run is { RaceRefusal: null, Summary: not null });
        if (!evaluated)
            return new Keys(false, 0m, null, null, ids);

        var summaries = e.Kinds.Select(k => k.Run!.Summary!).ToList();
        var breach = summaries.Max(s => s.StartCount == 0 ? 0m : (decimal)Count(s, Breaches) / s.StartCount);
        var funded = summaries.Select(s => s.StartCount == 0 || !s.Outcomes.Any(o => o.Outcome == FtmoChainOutcome.FundedNoBreachAtEndOfData)
            ? (decimal?)null
            : (decimal)Count(s, FtmoChainOutcome.FundedNoBreachAtEndOfData) / s.StartCount).ToList();
        var medians = summaries.Select(s => s.DaysToBothTargets.Median).ToList();

        return new Keys(
            true, breach,
            funded.Any(f => f is null) ? null : funded.Min(),
            medians.Any(m => m is null) ? null : medians.Max(),
            ids);
    }

    private static readonly FtmoChainOutcome[] Breaches =
        [FtmoChainOutcome.Phase1Breached, FtmoChainOutcome.Phase2Breached, FtmoChainOutcome.FundedBreached];

    private static int Count(FtmoMultiStartSummaryDto s, params FtmoChainOutcome[] outcomes)
        => s.Outcomes.Where(o => outcomes.Contains(o.Outcome)).Sum(o => o.Count);
}
