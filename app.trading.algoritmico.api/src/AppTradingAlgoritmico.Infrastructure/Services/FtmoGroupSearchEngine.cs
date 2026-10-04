using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMemberResolution;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>Why a strategy never enters the funnel. The zero value is <c>Unknown</c>, never a real reason.</summary>
public enum FtmoGroupSearchExclusionReason
{
    Unknown = 0,
    MissingKind,
    SymbolRefused,
    ZoneUnresolved,
    ProjectionRefused,
    ProjectionRowless,
    IdenticalDeployEval,
}

/// <summary>
/// ftmo-group-search D3 — the pure funnel: eligibility, enumeration, EXACT prunes, the proxy on each candidate's own
/// window, and the shortlist. <c>internal static</c>, no I/O, no randomness, no clock. Enumeration is in ascending
/// <c>StrategyId</c> order; the proxy stage may run in parallel but stores results BY CANDIDATE INDEX, so the
/// output never depends on thread scheduling.
/// </summary>
internal static class FtmoGroupSearchEngine
{
    internal sealed record SearchOptions(
        int MinMembers,
        int MaxMembers,
        int MaxPerInstrument = 1,
        bool IncludeIdenticalDeployEval = false,
        bool OnePercentRule = false,
        int ShortlistSize = FtmoGroupSearchLimits.ShortlistSize);

    internal sealed record Exclusion(Guid StrategyId, string Name, FtmoGroupSearchExclusionReason Reason, FtmoSimulationRefusal? Refusal);

    /// <summary><c>Eligible.Count + Exclusions.Count == PoolSize</c> always.</summary>
    internal sealed record EligibilityResult(
        IReadOnlyList<Guid> Eligible, IReadOnlySet<Guid> IdenticalDeployEval, IReadOnlyList<Exclusion> Exclusions, int PoolSize);

    /// <summary>Enumerated = removed by cap + by pair conflict + by the 1% rule + remaining (each counted once, in that order).</summary>
    internal readonly record struct Funnel(
        int Enumerated, int RemovedByCap, int RemovedByPairConflict, int RemovedByOnePercentRule, int Remaining);

    internal sealed record PruneResult(IReadOnlyList<int[]> Survivors, Funnel Funnel);

    /// <param name="Removal">Null when the candidate has a usable proxy; otherwise why it was removed.</param>
    internal readonly record struct ProxyOutcome(FtmoGroupRefusal? Removal, decimal DailyUsed, int Peak);

    internal sealed record ShortlistedCandidate(IReadOnlyList<Guid> MemberIds, decimal DailyUsed, int Peak);

    internal sealed record SearchPlan(
        EligibilityResult Eligibility, Funnel Funnel, int RemovedNoCommonWindow, int RemovedMemberHasNoTrades,
        IReadOnlyList<ShortlistedCandidate> Shortlist);

    // ---- Eligibility ----

    internal static EligibilityResult EvaluateEligibility(
        FtmoProjectionCache cache, Func<string?, FtmoSimulationInputs.SymbolResolution> resolve, bool includeIdentical)
    {
        var eligible = new List<Guid>();
        var identical = new HashSet<Guid>();
        var exclusions = new List<Exclusion>();

        foreach (var m in cache.Members)
        {
            var (reason, refusal) = Classify(cache, m, resolve);
            if (reason == FtmoGroupSearchExclusionReason.Unknown && IsIdentical(cache, m.StrategyId))
            {
                if (!includeIdentical)
                    (reason, refusal) = (FtmoGroupSearchExclusionReason.IdenticalDeployEval, null);
                else
                    identical.Add(m.StrategyId);
            }

            if (reason == FtmoGroupSearchExclusionReason.Unknown)
                eligible.Add(m.StrategyId);
            else
                exclusions.Add(new Exclusion(m.StrategyId, m.Name, reason, refusal));
        }

        return new EligibilityResult(eligible, identical, exclusions, cache.Members.Count);
    }

    private static (FtmoGroupSearchExclusionReason, FtmoSimulationRefusal?) Classify(
        FtmoProjectionCache cache, Member m, Func<string?, FtmoSimulationInputs.SymbolResolution> resolve)
    {
        var inputs = FtmoGroupMemberResolution.Kinds.Select(k => cache.Input(m.StrategyId, k)).ToList();
        if (inputs.Any(i => i.RunId is null))
            return (FtmoGroupSearchExclusionReason.MissingKind, null);
        if (m.SymbolRefusedBy is not null)
            return (FtmoGroupSearchExclusionReason.SymbolRefused, m.SymbolRefusedBy.Refusal);
        if (m.Runs.Values.Select(r => resolve(r.Symbol)).Any(r => r.ZoneRefusal is not null || r.SourceZone is null))
            return (FtmoGroupSearchExclusionReason.ZoneUnresolved, null);
        if (inputs.FirstOrDefault(i => (i.Refusal ?? i.Projection?.Refusal) is not null) is { } refused)
            return (FtmoGroupSearchExclusionReason.ProjectionRefused, refused.Refusal ?? refused.Projection?.Refusal);
        if (inputs.Any(i => i.Projection?.ProjectedLow is not { Count: > 0 }))
            return (FtmoGroupSearchExclusionReason.ProjectionRowless, null);
        return (FtmoGroupSearchExclusionReason.Unknown, null);
    }

    /// <summary>Decided on the projected Low and High series at the request's risk and FX band, never on run ids.</summary>
    private static bool IsIdentical(FtmoProjectionCache cache, Guid id)
    {
        var deploy = cache.Input(id, BacktestRunKind.Deploy).Projection!;
        var eval = cache.Input(id, BacktestRunKind.Evaluation).Projection!;
        return SameSeries(deploy.ProjectedLow!, eval.ProjectedLow!) && SameSeries(deploy.ProjectedHigh!, eval.ProjectedHigh!);
    }

    private static bool SameSeries(IReadOnlyList<ProjectedTrade> a, IReadOnlyList<ProjectedTrade> b)
        => a.Select(t => (t.OpenSource, t.CloseSource, t.Net, t.Outcome)).SequenceEqual(b.Select(t => (t.OpenSource, t.CloseSource, t.Net, t.Outcome)));

    // ---- Enumeration and exact prunes ----

    /// <summary>Index combinations over the ascending pool: sizes <paramref name="kMin"/>..<paramref name="kMax"/>, each lexicographic.</summary>
    internal static IEnumerable<int[]> Enumerate(int poolSize, int kMin, int kMax)
    {
        for (var k = kMin; k <= Math.Min(kMax, poolSize); k++)
        {
            var c = Enumerable.Range(0, k).ToArray();
            while (true)
            {
                yield return (int[])c.Clone();

                var i = k - 1;
                while (i >= 0 && c[i] == poolSize - k + i)
                    i--;
                if (i < 0)
                    break;

                c[i]++;
                for (var j = i + 1; j < k; j++)
                    c[j] = c[j - 1] + 1;
            }
        }
    }

    /// <summary>
    /// The three prunes that remove only what could never be shortlisted: the per-instrument cap (verbatim symbols),
    /// a pair conflict (different source zones, or a pair with no common window - for 1-D intervals the pairwise
    /// overlap is exactly <c>Intersect != null</c>, Helly), and the opt-in Academy 1% rule (<c>k x risk</c> past 1%
    /// of capital). Each removed candidate is counted once, under the first rule it breaks.
    /// </summary>
    internal static PruneResult Prune(
        FtmoProjectionCache cache,
        IReadOnlyList<Guid> eligibleIds,
        Func<string?, FtmoSimulationInputs.SymbolResolution> resolve,
        SearchOptions options,
        decimal initialCapital,
        decimal targetRiskPerTrade)
    {
        ValidateOptions(options);
        var members = cache.Members.ToDictionary(m => m.StrategyId);
        var instruments = eligibleIds.Select(id => cache.Instruments(id)).ToArray();
        var coverage = eligibleIds.Select(id => cache.Coverage(id)).ToArray();
        var zones = eligibleIds.Select(id => members[id].Runs.Values.Select(r => resolve(r.Symbol).SourceZone?.Id).FirstOrDefault()).ToArray();

        int enumerated = 0, byCap = 0, byPair = 0, byOnePercent = 0;
        var survivors = new List<int[]>();
        foreach (var combo in Enumerate(eligibleIds.Count, options.MinMembers, options.MaxMembers))
        {
            enumerated++;
            if (combo.SelectMany(i => instruments[i]).GroupBy(s => s, StringComparer.Ordinal).Any(g => g.Count() > options.MaxPerInstrument))
                byCap++;
            else if (HasPairConflict(combo, coverage, zones))
                byPair++;
            else if (options.OnePercentRule && combo.Length * targetRiskPerTrade > 0.01m * initialCapital)
                byOnePercent++;
            else
                survivors.Add(combo);
        }

        return new PruneResult(survivors, new Funnel(enumerated, byCap, byPair, byOnePercent, survivors.Count));
    }

    private static bool HasPairConflict(int[] combo, (DateTime First, DateTime Last)?[] coverage, string?[] zones)
    {
        for (var a = 0; a < combo.Length; a++)
        {
            for (var b = a + 1; b < combo.Length; b++)
            {
                if (zones[combo[a]] != zones[combo[b]])
                    return true;
                if (coverage[combo[a]] is not { } x || coverage[combo[b]] is not { } y)
                    return true;
                if (DateTime.Compare(x.First > y.First ? x.First : y.First, x.Last < y.Last ? x.Last : y.Last) > 0)
                    return true;
            }
        }

        return false;
    }

    // ---- Proxy ----

    /// <summary>
    /// The proxy of ONE candidate on its OWN window: shipped <c>Intersect</c> then <c>Merge</c> per kind (so the window
    /// is the candidate's, never the pool's), then the worse of both kinds and both FX ends for the daily-used fraction
    /// and the peak. A kind with an empty window, or a member without a row in it, removes the candidate.
    /// </summary>
    internal static ProxyOutcome ComputeProxy(FtmoProjectionCache cache, IReadOnlyList<Guid> ascendingIds, GroupParams p)
    {
        var allowance = p.DailyPct * p.InitialCapital;
        var worstLoss = 0m;
        var peak = 0;

        foreach (var kind in FtmoGroupMemberResolution.Kinds)
        {
            var series = cache.Rebind(ascendingIds, kind)
                .Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!))
                .ToList();

            var window = Intersect(series);
            if (window is null)
                return new ProxyOutcome(FtmoGroupRefusal.NoCommonWindow, 0m, 0);

            var merged = Merge(series, window.Value);
            if (merged.InWindowCountByMember.Any(n => n == 0))
                return new ProxyOutcome(FtmoGroupRefusal.MemberHasNoTradesInWindow, 0m, 0);

            foreach (var end in new[] { merged.Low, merged.High })
                worstLoss = Math.Max(worstLoss, FtmoDailyLossProfile.Compute(end, cache.DayOf, p.InitialCapital, p.DailyPct).WorstDayLoss);

            peak = Math.Max(peak, FtmoPeakConcurrency.Compute(merged));
        }

        return new ProxyOutcome(null, worstLoss / allowance, peak);
    }

    /// <summary>Parallel, but each result is written to its candidate's own slot, so the output is order-stable.</summary>
    internal static ProxyOutcome[] ProxyAll(
        FtmoProjectionCache cache, IReadOnlyList<Guid> eligibleIds, IReadOnlyList<int[]> survivors, GroupParams p,
        int maxDegreeOfParallelism = -1)
    {
        var outcomes = new ProxyOutcome[survivors.Count];
        Parallel.For(
            0, survivors.Count, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            i => outcomes[i] = ComputeProxy(cache, [.. survivors[i].Select(ix => eligibleIds[ix])], p));
        return outcomes;
    }

    // ---- Shortlist ----

    /// <summary>
    /// Per-size quota <c>floor(N / #sizes)</c> filled best-proxy-first, the remainder by global proxy order; returned in
    /// global proxy order (daily-used, then peak, then the sorted id tuple - the combos index an ascending pool).
    /// </summary>
    internal static IReadOnlyList<int> Shortlist(IReadOnlyList<int[]> survivors, IReadOnlyList<ProxyOutcome> proxies, SearchOptions options)
    {
        var ranked = Enumerable.Range(0, survivors.Count)
            .Where(i => proxies[i].Removal is null)
            .OrderBy(i => proxies[i].DailyUsed)
            .ThenBy(i => proxies[i].Peak)
            .ThenBy(i => survivors[i], Comparer<int[]>.Create(CompareCombos))
            .ToList();

        var quota = options.ShortlistSize / (options.MaxMembers - options.MinMembers + 1);
        var chosen = new HashSet<int>();
        for (var k = options.MinMembers; k <= options.MaxMembers; k++)
        {
            foreach (var i in ranked.Where(i => survivors[i].Length == k).Take(quota))
                chosen.Add(i);
        }

        foreach (var i in ranked.Where(i => !chosen.Contains(i)).Take(Math.Max(0, options.ShortlistSize - chosen.Count)))
            chosen.Add(i);

        return [.. ranked.Where(chosen.Contains)];
    }

    private static int CompareCombos(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);
        }

        return a.Length.CompareTo(b.Length);
    }

    // ---- Plan: everything up to (not including) the full computation ----

    internal static SearchPlan Plan(
        FtmoProjectionCache cache,
        Func<string?, FtmoSimulationInputs.SymbolResolution> resolve,
        SearchOptions options,
        GroupParams p,
        decimal targetRiskPerTrade,
        int maxDegreeOfParallelism = -1)
    {
        ValidateOptions(options);
        var eligibility = EvaluateEligibility(cache, resolve, options.IncludeIdenticalDeployEval);
        var pruned = Prune(cache, eligibility.Eligible, resolve, options, p.InitialCapital, targetRiskPerTrade);
        var proxies = ProxyAll(cache, eligibility.Eligible, pruned.Survivors, p, maxDegreeOfParallelism);

        var shortlist = Shortlist(pruned.Survivors, proxies, options)
            .Select(i => new ShortlistedCandidate([.. pruned.Survivors[i].Select(ix => eligibility.Eligible[ix])], proxies[i].DailyUsed, proxies[i].Peak))
            .ToList();

        return new SearchPlan(
            eligibility, pruned.Funnel,
            proxies.Count(x => x.Removal == FtmoGroupRefusal.NoCommonWindow),
            proxies.Count(x => x.Removal == FtmoGroupRefusal.MemberHasNoTradesInWindow),
            shortlist);
    }

    private static void ValidateOptions(SearchOptions o)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(o.MinMembers, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(o.MaxMembers, FtmoGroupSimulationLimits.MaxMembers);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(o.MinMembers, o.MaxMembers);
        ArgumentOutOfRangeException.ThrowIfLessThan(o.MaxPerInstrument, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(o.ShortlistSize, 1);
    }
}
