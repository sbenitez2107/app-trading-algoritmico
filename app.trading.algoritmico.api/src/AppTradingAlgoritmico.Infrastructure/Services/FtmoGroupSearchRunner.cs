using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMemberResolution;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D1/D2/D7 — the real runner: it loads the account's pool through the shared
/// <see cref="FtmoGroupMemberResolution"/> stages (six <c>AsNoTracking</c> queries, none per strategy), builds the
/// per-job <see cref="FtmoProjectionCache"/> ONCE, runs <c>Plan</c>, <c>Simulate</c> and <c>Rank</c>, and maps the outcome
/// to the wire DTOs. The job's DI scope (and its <c>DbContext</c>) exists only inside <see cref="LoadAsync"/>: it is
/// disposed before any CPU-heavy stage, and the cache is a local that dies with the job. Stages reported: Loading,
/// Eligibility (projection and eligibility), Proxy (enumeration, prunes, proxy, shortlist), Simulating, Ranking.
/// </summary>
internal sealed class FtmoGroupSearchRunner(IServiceScopeFactory scopes, TimeProvider clock) : IFtmoGroupSearchRunner
{
    /// <summary>Test seam: receives the cache the moment it is built, so a test can prove it is released.</summary>
    internal Action<FtmoProjectionCache>? OnCacheBuilt { get; init; }

    internal int MaxPoolSize { get; init; } = FtmoGroupSearchLimits.MaxPoolSize;

    private sealed record Job(
        Guid AccountId, string Broker, decimal Capital, decimal Risk, decimal? FxLow, decimal? FxHigh, LotGrid Grid,
        SearchOptions Options, int MaxFullSimulations, TimeSpan MaxWallClock, decimal Ceiling);

    private sealed record Loaded(
        ResolvedMembers Resolved, IReadOnlyDictionary<Guid, List<BacktestTrade>> Trades,
        FtmoSimulationInputs.LimitsResolution Limits, TimeZoneInfo Berlin);

    private static readonly FtmoGroupSearchFunnelDto EmptyFunnel = new(0, 0, 0, 0, 0, 0, 0);

    public async Task<FtmoGroupSearchRunResult> RunAsync(
        FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto> report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(report);

        var job = Validate(request);
        void Report(FtmoGroupSearchStage stage, int processed, int total, FtmoGroupSearchFunnelDto funnel, int done = 0)
            => report(new FtmoGroupSearchProgressDto(stage, processed, total, 0, done, job.MaxFullSimulations, funnel));

        Report(FtmoGroupSearchStage.Loading, 0, 0, EmptyFunnel);
        if (await LoadAsync(job, ct) is not { } loaded)
            return new FtmoGroupSearchRunResult(false, FtmoGroupSearchStopReason.None, 0, 0, EmptyFunnel, [], []);

        // From here on there is no database access and no scope. The CPU stages run for minutes, so they get a
        // dedicated thread (LongRunning) instead of holding a request-handling pool thread for the whole job.
        return await Task.Factory.StartNew(
            () => Compute(job, loaded, Report, ct), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private FtmoGroupSearchRunResult Compute(
        Job job, Loaded loaded, Action<FtmoGroupSearchStage, int, int, FtmoGroupSearchFunnelDto, int> report, CancellationToken ct)
    {
        void Report(FtmoGroupSearchStage stage, int processed, int total, FtmoGroupSearchFunnelDto funnel, int done = 0)
            => report(stage, processed, total, funnel, done);

        Report(FtmoGroupSearchStage.Eligibility, 0, 0, EmptyFunnel);
        var resolved = loaded.Resolved;
        var groupParams = BuildGroupParams(
            resolved.Members, resolved.Resolve, loaded.Limits, loaded.Berlin, job.Capital, job.FxLow, job.FxHigh);
        var cache = FtmoProjectionCache.Build(resolved.Members, loaded.Trades, job.Grid, job.Risk, resolved.Resolve, groupParams);
        OnCacheBuilt?.Invoke(cache);

        ct.ThrowIfCancellationRequested();
        Report(FtmoGroupSearchStage.Proxy, 0, 0, EmptyFunnel);
        SearchPlan plan;
        try
        {
            plan = Plan(cache, resolved.Resolve, job.Options, groupParams, job.Risk, ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new FtmoGroupSearchRunResult(true, FtmoGroupSearchStopReason.None, 0, 0, EmptyFunnel, [], []);
        }

        var funnel = ToFunnel(plan);

        ct.ThrowIfCancellationRequested();
        Report(FtmoGroupSearchStage.Simulating, 0, plan.Shortlist.Count, funnel);
        var simulationStart = clock.GetTimestamp();
        var outcome = Simulate(
            cache, plan.Shortlist, groupParams,
            new SimulationBudget(job.MaxFullSimulations, () => clock.GetElapsedTime(simulationStart) > job.MaxWallClock),
            p => Report(FtmoGroupSearchStage.Simulating, p.Done, p.Total, funnel, p.Done), ct);

        Report(FtmoGroupSearchStage.Ranking, outcome.Results.Count, outcome.Results.Count, funnel, outcome.Results.Count);
        var ranked = FtmoGroupSearchRanking.Rank(
            outcome.Results.Select(r => FtmoGroupSearchRanking.BuildEntry(cache, r, groupParams)), job.Ceiling);

        return new FtmoGroupSearchRunResult(
            outcome.Cancelled, ToStopReason(outcome.Stop), outcome.NotComputed, outcome.Results.Count, funnel,
            [.. plan.Eligibility.Exclusions.Select(ToIneligible)],
            [.. ranked.Select((r, i) => ToRow(i + 1, r, cache, plan.Eligibility.IdenticalDeployEval, groupParams))]);
    }

    /// <summary>
    /// The only place a scope exists. Six queries at most: the account's strategies, the limits row, then runs, specs and
    /// calibrations (<see cref="LoadMembersAsync"/>) and every trade in one query (<see cref="LoadTradesAsync"/>). Null
    /// when the account has no strategies; a pool above <see cref="MaxPoolSize"/> is refused after five, before the trades.
    /// </summary>
    private async Task<Loaded?> LoadAsync(Job job, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pool = await db.Strategies.AsNoTracking()
            .Where(s => s.TradingAccountId == job.AccountId)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);
        if (pool.Count == 0)
            return null;

        var ids = pool.Select(s => s.Id).Order().ToList();
        var names = pool.ToDictionary(s => s.Id, s => s.Name);

        var limits = await FtmoSimulationInputs.ResolveLimitsAsync(db, job.Broker, ct);
        if (limits.Refusal is not null)
            throw new FtmoGroupSearchRefusedException($"The loss limits for broker '{job.Broker}' cannot be used ({limits.Refusal}).");
        if (!FtmoSimulationInputs.TryResolveBerlin(out var berlin))
            throw new FtmoGroupSearchRefusedException("The Berlin time zone data is unavailable on this host.");

        var resolved = await LoadMembersAsync(db, ids, ids, names, job.FxLow, job.FxHigh, ct);

        // The pool bound is checked BEFORE the trades are loaded and the cache is built. The count is the members that
        // pass every trade-independent eligibility check (both kinds held, symbol usable, zone resolved): an upper
        // bound of the eligible count, which only the projection (needing the trades) can lower further.
        var searchable = resolved.Members.Count(m => CanBeEligible(m, resolved.Resolve));
        if (searchable > MaxPoolSize)
        {
            throw new FtmoGroupSearchRefusedException(
                $"The pool has {searchable} eligible strategies; the maximum is {MaxPoolSize}. Narrow the pool and search again.");
        }

        var trades = await LoadTradesAsync(db, resolved.Members, ct);
        return new Loaded(resolved, trades, limits, berlin!);
    }

    /// <summary>The trade-independent part of the engine's eligibility classification, in its order.</summary>
    private static bool CanBeEligible(Member m, Func<string?, FtmoSimulationInputs.SymbolResolution> resolve)
        => FtmoGroupMemberResolution.Kinds.All(m.Runs.ContainsKey)
           && m.SymbolRefusedBy is null
           && m.Runs.Values.Select(r => resolve(r.Symbol)).All(r => r.ZoneRefusal is null && r.SourceZone is not null);

    private static Job Validate(FtmoGroupSearchRequest r)
    {
        if (r.TradingAccountId is not { } account || r.MinMembers is not { } min || r.MaxMembers is not { } max
            || r.InitialCapital is not { } capital || r.TargetRiskPerTrade is not { } risk
            || r.SizeDecimals is not { } decimals || r.Step is not { } step || r.MinLot is not { } minLot
            || r.MaxLots is not { } maxLots || string.IsNullOrWhiteSpace(r.Broker))
        {
            throw new FtmoGroupSearchRefusedException("The search request is missing required fields.");
        }

        var grid = new FtmoGroupSimulationParameters([], r.Broker, capital, risk, r.FxLow, r.FxHigh, decimals, step, minLot, maxLots).TryBuildSourceGrid();
        if (capital <= 0m || risk <= 0m || grid is null)
            throw new FtmoGroupSearchRefusedException("The capital, the risk per trade and the lot grid must be usable values.");

        return new Job(
            account, r.Broker, capital, risk, r.FxLow, r.FxHigh, grid,
            new SearchOptions(
                min, max, r.MaxPerInstrument ?? FtmoGroupSearchLimits.DefaultMaxPerInstrument,
                r.IncludeIdenticalDeployEval ?? false, r.OnePercentRule ?? false),
            r.MaxFullSimulations ?? FtmoGroupSearchLimits.DefaultMaxFullSimulations,
            r.MaxWallClockSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : FtmoGroupSearchLimits.DefaultMaxWallClock,
            r.EliminationCeiling ?? FtmoGroupSearchRanking.DefaultEliminationCeiling);
    }

    private static FtmoGroupSearchFunnelDto ToFunnel(SearchPlan plan) => new(
        plan.Funnel.Enumerated, plan.Funnel.RemovedByCap, plan.Funnel.RemovedByPairConflict, plan.Funnel.RemovedByOnePercentRule,
        plan.RemovedNoCommonWindow, plan.RemovedMemberHasNoTrades, plan.Shortlist.Count);

    private static FtmoGroupSearchStopReason ToStopReason(SearchStopReason stop) => stop switch
    {
        SearchStopReason.None => FtmoGroupSearchStopReason.None,
        SearchStopReason.MaxFullSimulations => FtmoGroupSearchStopReason.MaxFullSimulations,
        SearchStopReason.WallClock => FtmoGroupSearchStopReason.WallClock,
        _ => FtmoGroupSearchStopReason.Unknown,
    };

    private static FtmoGroupSearchIneligibleDto ToIneligible(Exclusion e) => new(e.StrategyId, e.Name, e.Reason switch
    {
        FtmoGroupSearchExclusionReason.MissingKind => FtmoGroupSearchIneligibleReason.MissingKind,
        FtmoGroupSearchExclusionReason.SymbolRefused => FtmoGroupSearchIneligibleReason.SymbolRefused,
        FtmoGroupSearchExclusionReason.ZoneUnresolved => FtmoGroupSearchIneligibleReason.ZoneUnresolved,
        FtmoGroupSearchExclusionReason.ProjectionRefused => FtmoGroupSearchIneligibleReason.ProjectionRefused,
        FtmoGroupSearchExclusionReason.ProjectionRowless => FtmoGroupSearchIneligibleReason.ProjectionRowless,
        FtmoGroupSearchExclusionReason.IdenticalDeployEval => FtmoGroupSearchIneligibleReason.IdenticalDeployEval,
        _ => FtmoGroupSearchIneligibleReason.Unknown,
    }, e.Refusal);

    private static FtmoGroupSearchRowDto ToRow(
        int rank, FtmoGroupSearchRanking.RankedEntry ranked, FtmoProjectionCache cache,
        IReadOnlySet<Guid> identical, GroupParams p)
    {
        var entry = ranked.Entry;
        var names = cache.Members.ToDictionary(m => m.StrategyId, m => m.Name);
        return new FtmoGroupSearchRowDto(
            rank, entry.MemberIds, [.. entry.MemberIds.Select(id => names[id])], entry.Peak,
            identical.Overlaps(entry.MemberIds), ranked.WithinCeiling, entry.Headroom, KindHeadrooms(cache, entry, p), entry.Kinds,
            [.. entry.MemberIds.SelectMany(id => cache.Instruments(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    /// <summary>Each evaluated kind's own limit usage on the candidate's merged window, never blended.</summary>
    private static List<FtmoGroupSearchKindHeadroomDto> KindHeadrooms(
        FtmoProjectionCache cache, FtmoGroupSearchRanking.RankEntry entry, GroupParams p)
    {
        var result = new List<FtmoGroupSearchKindHeadroomDto>();
        foreach (var kind in entry.Kinds.Where(k => k.Run is not null))
        {
            var series = cache.Rebind(entry.MemberIds, kind.Kind)
                .Select(m => new FtmoGroupMerger.MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!))
                .ToList();
            if (FtmoGroupMerger.Intersect(series) is not { } window)
                continue;

            var h = FtmoLimitHeadroom.Compute(FtmoGroupMerger.Merge(series, window), kind.Run!.Starts, cache.DayOf, p);
            result.Add(new FtmoGroupSearchKindHeadroomDto(kind.Kind, h.DailyUsed, h.WorstMaxUsed, h.MedianMaxUsed, h.Headroom));
        }

        return result;
    }
}
