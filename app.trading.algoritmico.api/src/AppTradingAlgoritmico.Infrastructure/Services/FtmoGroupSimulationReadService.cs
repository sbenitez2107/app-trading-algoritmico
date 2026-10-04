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
    private static readonly BacktestRunKind[] Kinds = FtmoGroupMemberResolution.Kinds;

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

        var names = await FtmoGroupMemberResolution.LoadNamesAsync(db, orderedIds, ct);

        var foundIds = names.Keys.ToHashSet();
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

        var resolved = await FtmoGroupMemberResolution.LoadMembersAsync(
            db, orderedIds, knownOrdered, names, parameters.FxLow, parameters.FxHigh, ct);
        var members = resolved.Members;
        var resolve = resolved.Resolve;

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
            Kinds.Where(m.Runs.ContainsKey).Select(k => resolve(m.Runs[k].Symbol)).Any(r => r.Refusal is null && r.ZoneRefusal is not null));
        if (anyZoneUnresolved || !FtmoSimulationInputs.TryResolveBerlin(out var berlinZone))
        {
            return GroupWide(
                FtmoGroupRefusal.SharedInputsRefused, shared: FtmoSimulationRefusal.TimeZoneDataUnavailable,
                daily: limits.DailyPct, max: limits.MaxPct, members: memberDtos, duplicates: duplicates, warnings: warnings);
        }

        // Trades: one query for every run that can still be replayed (its member's symbol resolved).
        var tradesByRun = await FtmoGroupMemberResolution.LoadTradesAsync(db, members, ct);

        // From here on there is no database access.
        var groupParams = FtmoGroupMemberResolution.BuildGroupParams(
            members, resolve, limits, berlinZone!, parameters.InitialCapital, parameters.FxLow, parameters.FxHigh);

        var kindResults = new List<FtmoGroupKindResultDto>(Kinds.Length);
        foreach (var kind in Kinds)
        {
            ct.ThrowIfCancellationRequested();

            var inputs = members.Select(m => FtmoGroupMemberResolution.ToKindInput(m, kind, tradesByRun, sourceGrid, parameters.TargetRiskPerTrade, resolve)).ToList();
            kindResults.Add(ComputeGroup(kind, inputs, groupParams, ct));
        }

        return new FtmoGroupSimulationDto(
            FtmoSimulationStatus.Evaluated, Refusal: null, SharedRefusal: null, limits.DailyPct, limits.MaxPct, memberDtos,
            duplicates, warnings, UnknownStrategyIds: [], kindResults, FtmoGroupSimulationLimits.Disclosures);
    }

    private static FtmoGroupMemberDto ToMemberDto(FtmoGroupMemberResolution.Member member)
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
