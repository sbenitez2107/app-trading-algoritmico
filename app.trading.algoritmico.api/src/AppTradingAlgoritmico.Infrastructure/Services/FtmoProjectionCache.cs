using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMemberResolution;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D2 — the per-job projection cache. Every strategy is projected ONCE at the job's single risk,
/// grid and FX band (the projection depends on those and the trades, never on the group), and each candidate
/// rebinds the cached inputs with <c>MemberOrder = i</c>. Also holds one shared instant (close or open) to bookkeeping-day
/// dictionary (read-only after construction, so safe under the parallel proxy), and each member's instrument set
/// and coverage. Instance state only: the cache dies with the job.
/// </summary>
internal sealed class FtmoProjectionCache
{
    private readonly Dictionary<(Guid, BacktestRunKind), GroupMemberKindInput> _inputs;
    private readonly Dictionary<DateTime, DateOnly> _days;
    private readonly Dictionary<Guid, HashSet<string>> _instruments;
    private readonly Dictionary<Guid, (DateTime First, DateTime Last)?> _coverage;

    private FtmoProjectionCache(
        IReadOnlyList<Member> members, Dictionary<(Guid, BacktestRunKind), GroupMemberKindInput> inputs,
        TimeZoneInfo sourceZone, TimeZoneInfo berlin)
    {
        _inputs = inputs;
        Members = [.. members.OrderBy(m => m.StrategyId)];
        _instruments = Members.ToDictionary(
            m => m.StrategyId,
            m => m.Runs.Values.Select(r => r.Symbol).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToHashSet(StringComparer.Ordinal));

        _days = [];
        _coverage = [];
        foreach (var m in Members)
        {
            var rows = FtmoGroupMemberResolution.Kinds
                .Select(k => _inputs.GetValueOrDefault((m.StrategyId, k))?.Projection?.ProjectedLow)
                .Where(r => r is { Count: > 0 })
                .SelectMany(r => r!)
                .ToList();
            _coverage[m.StrategyId] = rows.Count == 0 ? null : (rows.Min(t => t.OpenSource), rows.Max(t => t.CloseSource));

            // Closes feed the daily-loss bookkeeping; opens feed the race surrogate's start months and trading days.
            foreach (var instant in rows.SelectMany(t => new[] { t.CloseSource, t.OpenSource }))
            {
                if (!_days.ContainsKey(instant))
                    _days[instant] = FtmoDayClock.Attribute(instant, sourceZone, berlin).BookkeepingDay;
            }
        }
    }

    /// <summary>The pool in ascending-<c>StrategyId</c> order.</summary>
    internal IReadOnlyList<Member> Members { get; }

    internal static FtmoProjectionCache Create(
        IReadOnlyList<Member> members, IReadOnlyDictionary<(Guid, BacktestRunKind), GroupMemberKindInput> inputs,
        TimeZoneInfo sourceZone, TimeZoneInfo berlin)
        => new(members, new Dictionary<(Guid, BacktestRunKind), GroupMemberKindInput>(inputs), sourceZone, berlin);

    /// <summary>Projects every member of both kinds through the shipped stages (<c>ToKindInput</c>) and caches them.</summary>
    internal static FtmoProjectionCache Build(
        IReadOnlyList<Member> members,
        IReadOnlyDictionary<Guid, List<BacktestTrade>> tradesByRun,
        LotGrid sourceGrid,
        decimal targetRiskPerTrade,
        Func<string?, FtmoSimulationInputs.SymbolResolution> resolve,
        GroupParams poolParams)
    {
        var inputs = new Dictionary<(Guid, BacktestRunKind), GroupMemberKindInput>();
        foreach (var m in members)
        {
            foreach (var kind in FtmoGroupMemberResolution.Kinds)
                inputs[(m.StrategyId, kind)] = ToKindInput(m, kind, tradesByRun, sourceGrid, targetRiskPerTrade, resolve);
        }

        return Create(members, inputs, poolParams.SourceZone ?? TimeZoneInfo.Utc, poolParams.BerlinZone);
    }

    internal GroupMemberKindInput Input(Guid strategyId, BacktestRunKind kind) => _inputs[(strategyId, kind)];

    /// <summary>The candidate's inputs for one kind; the member's index in <paramref name="ascendingIds"/> is its order.</summary>
    internal IReadOnlyList<GroupMemberKindInput> Rebind(IReadOnlyList<Guid> ascendingIds, BacktestRunKind kind)
        => [.. ascendingIds.Select((id, i) => Input(id, kind) with { MemberOrder = i })];

    internal DateOnly DayOf(DateTime closeInstant) => _days[closeInstant];

    internal IReadOnlySet<string> Instruments(Guid strategyId) => _instruments[strategyId];

    /// <summary>First open and last close over both kinds' rows; null when the member has none.</summary>
    internal (DateTime First, DateTime Last)? Coverage(Guid strategyId) => _coverage[strategyId];
}
