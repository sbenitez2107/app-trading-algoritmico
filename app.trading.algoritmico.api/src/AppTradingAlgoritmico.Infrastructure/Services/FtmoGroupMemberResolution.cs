using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D1 — the member-resolution stages of <see cref="FtmoGroupSimulationReadService"/>, moved out
/// VERBATIM so a search loader reuses the exact same inputs (the only way "metrics equal a direct run" can hold).
/// <para>
/// The work is split into STAGES, not one call, so the group service keeps its query order and every group-wide
/// refusal in its shipped precedence: names, then (after the unknown-id and limits refusals) members, then
/// (after the zone refusals) trades. A single <c>ResolveAsync</c> would issue the runs, specs and trades queries
/// after a refusal and change the query count.
/// </para>
/// </summary>
internal static class FtmoGroupMemberResolution
{
    internal static readonly BacktestRunKind[] Kinds = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

    internal sealed record MemberRun(Guid RunId, BacktestRunKind Kind, string? Symbol);

    internal sealed record Member(
        Guid StrategyId,
        string Name,
        int Order,
        IReadOnlyDictionary<BacktestRunKind, MemberRun> Runs,
        FtmoSimulationInputs.SymbolResolution? SymbolRefusedBy,
        FtmoSimulationInputs.SymbolResolution? Display);

    internal sealed record ResolvedMembers(
        List<Member> Members,
        Func<string?, FtmoSimulationInputs.SymbolResolution> Resolve,
        List<string> Symbols);

    /// <summary>Query 1: the strategies' names, keyed by id. An id that is not in the result does not exist.</summary>
    internal static async Task<Dictionary<Guid, string>> LoadNamesAsync(
        AppDbContext db, IReadOnlyList<Guid> orderedIds, CancellationToken ct)
    {
        var strategies = await db.Strategies.AsNoTracking()
            .Where(s => orderedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        return strategies.ToDictionary(s => s.Id, s => s.Name);
    }

    /// <summary>Queries 3-5: runs, specs and calibrations (the last two by the distinct run symbols).</summary>
    internal static async Task<ResolvedMembers> LoadMembersAsync(
        AppDbContext db,
        IReadOnlyList<Guid> orderedIds,
        IReadOnlyList<Guid> knownOrdered,
        IReadOnlyDictionary<Guid, string> names,
        decimal? fxLow,
        decimal? fxHigh,
        CancellationToken ct)
    {
        var runs = await db.BacktestRuns.AsNoTracking()
            .Where(r => orderedIds.Contains(r.StrategyId))
            .Select(r => new { r.Id, r.StrategyId, r.Kind, r.Symbol })
            .ToListAsync(ct);

        var symbols = runs
            .Select(r => r.Symbol)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var specs = await db.FtmoInstrumentSpecs.AsNoTracking()
            .Where(s => symbols.Contains(s.SqxSymbol))
            .ToListAsync(ct);
        var calibrations = await db.SymbolCalibrations.AsNoTracking()
            .Where(c => symbols.Contains(c.Symbol))
            .ToListAsync(ct);

        var specBySymbol = specs
            .GroupBy(s => s.SqxSymbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var calibrationBySymbol = calibrations
            .GroupBy(c => c.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Pure, per distinct symbol. A run with no symbol resolves like a symbol with no spec.
        var resolutions = symbols.ToDictionary(
            s => s,
            s => FtmoSimulationInputs.ResolveSymbol(
                specBySymbol.GetValueOrDefault(s), calibrationBySymbol.GetValueOrDefault(s), fxLow, fxHigh),
            StringComparer.OrdinalIgnoreCase);
        var noSymbol = FtmoSimulationInputs.ResolveSymbol(null, null, fxLow, fxHigh);

        FtmoSimulationInputs.SymbolResolution Resolve(string? symbol)
            => !string.IsNullOrWhiteSpace(symbol) && resolutions.TryGetValue(symbol, out var r) ? r : noSymbol;

        var members = knownOrdered
            .Select((id, order) =>
            {
                var held = runs
                    .Where(r => r.StrategyId == id)
                    .GroupBy(r => r.Kind)
                    .ToDictionary(g => g.Key, g => new MemberRun(g.First().Id, g.Key, g.First().Symbol));
                var heldResolutions = Kinds.Where(held.ContainsKey).Select(k => Resolve(held[k].Symbol)).ToList();

                return new Member(
                    id, names[id], order, held,
                    SymbolRefusedBy: heldResolutions.FirstOrDefault(r => r.Refusal is not null),
                    Display: heldResolutions.FirstOrDefault(r => r.Spec is not null && r.FtmoGrid is not null));
            })
            .ToList();

        return new ResolvedMembers(members, Resolve, symbols);
    }

    /// <summary>Query 6: every needed run's trades in ONE query, for every run that can still be replayed.</summary>
    internal static async Task<Dictionary<Guid, List<BacktestTrade>>> LoadTradesAsync(
        AppDbContext db, IReadOnlyList<Member> members, CancellationToken ct)
    {
        var neededRunIds = members
            .Where(m => m.SymbolRefusedBy is null)
            .SelectMany(m => m.Runs.Values.Select(r => r.RunId))
            .ToList();
        return neededRunIds.Count == 0
            ? new Dictionary<Guid, List<BacktestTrade>>()
            : (await db.BacktestTrades.AsNoTracking()
                    .Where(t => neededRunIds.Contains(t.BacktestRunId))
                    .ToListAsync(ct))
                .GroupBy(t => t.BacktestRunId)
                .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// The first resolved member zone is the source zone; the echo band is the requested band only when THIS group
    /// holds a non-USD member. Pure: no database access.
    /// </summary>
    internal static GroupParams BuildGroupParams(
        IReadOnlyList<Member> members,
        Func<string?, FtmoSimulationInputs.SymbolResolution> resolve,
        FtmoSimulationInputs.LimitsResolution limits,
        TimeZoneInfo berlinZone,
        decimal initialCapital,
        decimal? fxLow,
        decimal? fxHigh)
    {
        var sourceZone = members
            .SelectMany(m => Kinds.Where(m.Runs.ContainsKey).Select(k => resolve(m.Runs[k].Symbol)))
            .Where(r => r.Refusal is null)
            .Select(r => r.SourceZone)
            .FirstOrDefault(z => z is not null);

        var nonUsdMember = members.Any(m => m.Display is not null && !FtmoSimulationInputs.SettlesInAccountCurrency(m.Display.Spec!));
        var echoBand = nonUsdMember ? (fxLow ?? 1m, fxHigh ?? 1m) : (1m, 1m);
        var rules = new FtmoChallengeRulesDto(
            FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
            FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null);
        return new GroupParams(
            sourceZone, berlinZone, initialCapital, limits.DailyPct, limits.MaxPct, limits.ProfitTargetPct,
            echoBand, rules);
    }

    internal static GroupMemberKindInput ToKindInput(
        Member member,
        BacktestRunKind kind,
        IReadOnlyDictionary<Guid, List<BacktestTrade>> tradesByRun,
        LotGrid sourceGrid,
        decimal targetRiskPerTrade,
        Func<string?, FtmoSimulationInputs.SymbolResolution> resolve)
    {
        if (!member.Runs.TryGetValue(kind, out var run))
            return new GroupMemberKindInput(member.StrategyId, member.Name, member.Order, null, null, null);

        // A symbol-level failure belongs to the member's symbol, so it refuses every kind the member has.
        if (member.SymbolRefusedBy is not null)
        {
            return new GroupMemberKindInput(
                member.StrategyId, member.Name, member.Order, run.RunId, member.SymbolRefusedBy.Refusal, null);
        }

        var symbol = resolve(run.Symbol);
        var trades = tradesByRun.GetValueOrDefault(run.RunId) ?? [];
        var projection = FtmoSimulationInputs.ProjectRun(
            trades, sourceGrid, symbol.FtmoGrid!, targetRiskPerTrade, symbol.PointValue, symbol.Spec!.ContractSize, symbol.FxBand);

        return new GroupMemberKindInput(member.StrategyId, member.Name, member.Order, run.RunId, null, projection);
    }
}
