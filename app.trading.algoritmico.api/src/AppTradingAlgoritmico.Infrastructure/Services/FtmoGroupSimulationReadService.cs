using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B2 (design.md D5) — resolves every member's inputs from the database, projects each
/// member's held runs with the shipped <see cref="FtmoSimulationInputs.ProjectRun"/>, and hands the
/// ALREADY-RESOLVED inputs to the pure <see cref="FtmoGroupComputation.ComputeGroup"/> once per kind.
/// <para>
/// <b>Queries.</b> At most six, none depending on the member count: strategies, the limits row, runs, specs,
/// calibrations (the last two by the distinct run symbols, via <c>Contains</c>), and every needed run's trades
/// in ONE query grouped in memory. All are <c>AsNoTracking</c> and all finish before any computation starts, so
/// the pure compute never touches the <c>DbContext</c>.
/// </para>
/// <para>
/// <b>Refusal precedence (group-wide).</b> <c>InvalidRequest</c> (no query is issued) -&gt;
/// <c>MemberNotFound</c> -&gt; <c>SharedInputsRefused</c> (limits) -&gt; <c>MixedSourceTimeZones</c> -&gt;
/// <c>SharedInputsRefused</c> (<c>TimeZoneDataUnavailable</c>). Everything else is member-level and lives in
/// the per-kind results.
/// </para>
/// </summary>
public sealed class FtmoGroupSimulationReadService(AppDbContext db) : IFtmoGroupSimulationReadService
{
    private static readonly BacktestRunKind[] Kinds = [BacktestRunKind.Deploy, BacktestRunKind.Evaluation];

    private sealed record MemberRun(Guid RunId, BacktestRunKind Kind, string? Symbol);

    private sealed record Member(
        Guid StrategyId,
        string Name,
        int Order,
        IReadOnlyDictionary<BacktestRunKind, MemberRun> Runs,
        FtmoSimulationInputs.SymbolResolution? SymbolRefusedBy,
        FtmoSimulationInputs.SymbolResolution? Display);

    public async Task<FtmoGroupSimulationDto> SimulateAsync(FtmoGroupSimulationParameters parameters, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ct.ThrowIfCancellationRequested();

        var requested = parameters.MemberStrategyIds ?? [];
        var orderedIds = FtmoGroupMerger.OrderMembers(requested).ToList();
        var duplicates = requested.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).Order().ToList();

        // Group-wide 1: a present-but-unusable value. No query is issued.
        var sourceGrid = parameters.TryBuildSourceGrid();
        if (orderedIds.Count == 0 || sourceGrid is null || parameters.InitialCapital <= 0m || parameters.TargetRiskPerTrade <= 0m)
            return GroupWide(FtmoGroupRefusal.InvalidRequest, duplicates: duplicates);

        var strategies = await db.Strategies.AsNoTracking()
            .Where(s => orderedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        var foundIds = strategies.Select(s => s.Id).ToHashSet();
        var names = strategies.ToDictionary(s => s.Id, s => s.Name);
        var knownOrdered = orderedIds.Where(foundIds.Contains).ToList();
        var warnings = NameWarnings(knownOrdered, names);

        // Group-wide 2: an unknown id. The members that DO exist are echoed without resolved facts.
        var unknown = orderedIds.Where(id => !foundIds.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            return GroupWide(
                FtmoGroupRefusal.MemberNotFound, members: BareMembers(knownOrdered, names), duplicates: duplicates,
                warnings: warnings, unknown: unknown);
        }

        // Group-wide 3: the broker limits row.
        var limits = await FtmoSimulationInputs.ResolveLimitsAsync(db, parameters.Broker, ct);
        if (limits.Refusal is not null)
        {
            var configured = limits.Refusal != FtmoSimulationRefusal.LimitsNotConfigured;
            return GroupWide(
                FtmoGroupRefusal.SharedInputsRefused, shared: limits.Refusal,
                daily: configured ? limits.DailyPct : null, max: configured ? limits.MaxPct : null,
                members: BareMembers(knownOrdered, names), duplicates: duplicates, warnings: warnings);
        }

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
                specBySymbol.GetValueOrDefault(s), calibrationBySymbol.GetValueOrDefault(s), parameters.FxLow, parameters.FxHigh),
            StringComparer.OrdinalIgnoreCase);
        var noSymbol = FtmoSimulationInputs.ResolveSymbol(null, null, parameters.FxLow, parameters.FxHigh);

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

        var memberDtos = members.Select(ToMemberDto).ToList();

        // Group-wide 4: the source zone is a property of the group.
        var zoneIds = members
            .Where(m => m.Display is not null)
            .Select(m => m.Display!.Spec!.SourceTimeZoneId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (zoneIds.Count > 1)
        {
            return GroupWide(
                FtmoGroupRefusal.MixedSourceTimeZones, daily: limits.DailyPct, max: limits.MaxPct, members: memberDtos,
                duplicates: duplicates, warnings: warnings);
        }

        var anyZoneUnresolved = members.Any(m =>
            Kinds.Where(m.Runs.ContainsKey).Select(k => Resolve(m.Runs[k].Symbol)).Any(r => r.Refusal is null && r.ZoneRefusal is not null));
        if (anyZoneUnresolved || !FtmoSimulationInputs.TryResolveBerlin(out var berlinZone))
        {
            return GroupWide(
                FtmoGroupRefusal.SharedInputsRefused, shared: FtmoSimulationRefusal.TimeZoneDataUnavailable,
                daily: limits.DailyPct, max: limits.MaxPct, members: memberDtos, duplicates: duplicates, warnings: warnings);
        }

        var sourceZone = members
            .SelectMany(m => Kinds.Where(m.Runs.ContainsKey).Select(k => Resolve(m.Runs[k].Symbol)))
            .Where(r => r.Refusal is null)
            .Select(r => r.SourceZone)
            .FirstOrDefault(z => z is not null);

        // Trades: one query for every run that can still be replayed (its member's symbol resolved).
        var neededRunIds = members
            .Where(m => m.SymbolRefusedBy is null)
            .SelectMany(m => m.Runs.Values.Select(r => r.RunId))
            .ToList();
        var tradesByRun = neededRunIds.Count == 0
            ? new Dictionary<Guid, List<BacktestTrade>>()
            : (await db.BacktestTrades.AsNoTracking()
                    .Where(t => neededRunIds.Contains(t.BacktestRunId))
                    .ToListAsync(ct))
                .GroupBy(t => t.BacktestRunId)
                .ToDictionary(g => g.Key, g => g.ToList());

        // From here on there is no database access.
        var nonUsdMember = members.Any(m => m.Display is not null && !FtmoSimulationInputs.SettlesInAccountCurrency(m.Display.Spec!));
        var echoBand = nonUsdMember ? (parameters.FxLow ?? 1m, parameters.FxHigh ?? 1m) : (1m, 1m);
        var rules = new FtmoChallengeRulesDto(
            FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
            FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null);
        var groupParams = new GroupParams(
            sourceZone, berlinZone!, parameters.InitialCapital, limits.DailyPct, limits.MaxPct, limits.ProfitTargetPct,
            echoBand, rules);

        var kindResults = new List<FtmoGroupKindResultDto>(Kinds.Length);
        foreach (var kind in Kinds)
        {
            ct.ThrowIfCancellationRequested();

            var inputs = members.Select(m => ToKindInput(m, kind, tradesByRun, sourceGrid, parameters.TargetRiskPerTrade, Resolve)).ToList();
            kindResults.Add(ComputeGroup(kind, inputs, groupParams, ct));
        }

        return new FtmoGroupSimulationDto(
            FtmoSimulationStatus.Evaluated, Refusal: null, SharedRefusal: null, limits.DailyPct, limits.MaxPct, memberDtos,
            duplicates, warnings, UnknownStrategyIds: [], kindResults, FtmoGroupSimulationLimits.Disclosures);
    }

    private static GroupMemberKindInput ToKindInput(
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

    private static FtmoGroupMemberDto ToMemberDto(Member member)
    {
        var display = member.Display;
        var applied = display is not null && member.SymbolRefusedBy is null;
        return new FtmoGroupMemberDto(
            member.StrategyId, member.Name, member.Order, display?.Spec?.ProfitCurrency, display?.Spec?.SourceTimeZoneId,
            applied ? display!.FxBand.Low : null, applied ? display!.FxBand.High : null);
    }

    private static List<FtmoGroupMemberDto> BareMembers(IReadOnlyList<Guid> ordered, IReadOnlyDictionary<Guid, string> names)
        => [.. ordered.Select((id, order) => new FtmoGroupMemberDto(id, names[id], order, null, null, null, null))];

    private static List<FtmoGroupNameWarningDto> NameWarnings(IReadOnlyList<Guid> ordered, IReadOnlyDictionary<Guid, string> names)
        => [.. ordered
            .GroupBy(id => names[id], StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new FtmoGroupNameWarningDto(names[g.First()], [.. g]))];

    private static FtmoGroupSimulationDto GroupWide(
        FtmoGroupRefusal refusal,
        FtmoSimulationRefusal? shared = null,
        decimal? daily = null,
        decimal? max = null,
        IReadOnlyList<FtmoGroupMemberDto>? members = null,
        IReadOnlyList<Guid>? duplicates = null,
        IReadOnlyList<FtmoGroupNameWarningDto>? warnings = null,
        IReadOnlyList<Guid>? unknown = null)
        => new(
            FtmoSimulationStatus.Refused, refusal, shared, daily, max, members ?? [], duplicates ?? [], warnings ?? [],
            unknown ?? [], Kinds: [], FtmoGroupSimulationLimits.Disclosures);
}
