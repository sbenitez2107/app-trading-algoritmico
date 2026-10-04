using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B2 (design.md D4) — the pure per-kind group computation. It takes ALREADY-RESOLVED
/// member inputs and parameters (no <c>DbContext</c>, no I/O) and composes the shipped pieces without editing
/// any of them: member refusals, then <see cref="FtmoGroupMerger.Intersect"/>, then
/// <see cref="FtmoGroupMerger.Merge"/> (trim, sort, global renumbering), then the empty-member check, then the
/// shipped <see cref="FtmoMultiStartReadService.ComputeRun"/> over the merged series. This is the seam a later
/// combination generator would call.
/// </summary>
internal static class FtmoGroupComputation
{
    /// <summary>
    /// One member's input for one kind. <see cref="RunId"/> null means the member has no held run of the kind.
    /// <see cref="Refusal"/> is a symbol-level reason the caller resolved for the member (spec, calibration, FX);
    /// a run-level reason arrives on <see cref="Projection"/>'s own <c>Refusal</c>. <see cref="MemberOrder"/> is
    /// the member's index in the ascending-<c>StrategyId</c> order, unique in <c>[0, members.Count)</c>.
    /// </summary>
    internal sealed record GroupMemberKindInput(
        Guid StrategyId,
        string Name,
        int MemberOrder,
        Guid? RunId,
        FtmoSimulationRefusal? Refusal,
        FtmoSimulationInputs.RunProjection? Projection);

    /// <summary>
    /// Group-wide parameters. <see cref="SourceZone"/> is null only when no member could resolve one, which
    /// means no member can reach the replay.
    /// </summary>
    internal sealed record GroupParams(
        TimeZoneInfo? SourceZone,
        TimeZoneInfo BerlinZone,
        decimal InitialCapital,
        decimal DailyPct,
        decimal MaxPct,
        decimal? ProfitTargetPct,
        (decimal Low, decimal High) EchoFxBand,
        FtmoChallengeRulesDto Rules);

    internal static FtmoGroupKindResultDto ComputeGroup(
        BacktestRunKind kind, IReadOnlyList<GroupMemberKindInput> members, GroupParams p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(p);
        ct.ThrowIfCancellationRequested();

        var ordered = members.OrderBy(m => m.MemberOrder).ToList();

        // Stage 1: every member is checked and EVERY failing one is listed with its own reason.
        var failures = new List<FtmoGroupMemberRefusalDto>();
        foreach (var m in ordered)
        {
            if (m.RunId is null)
            {
                failures.Add(new FtmoGroupMemberRefusalDto(m.StrategyId, m.Name, FtmoGroupRefusal.MemberMissingKind, null));
                continue;
            }

            var reason = m.Refusal ?? m.Projection?.Refusal;
            if (reason is not null)
            {
                failures.Add(new FtmoGroupMemberRefusalDto(m.StrategyId, m.Name, FtmoGroupRefusal.MemberRunRefused, reason));
                continue;
            }

            if (m.Projection?.ProjectedLow is null || m.Projection.ProjectedHigh is null)
            {
                throw new InvalidOperationException(
                    $"Member {m.StrategyId} has a run but neither a refusal nor a projection (wiring defect).");
            }
        }

        if (failures.Count > 0)
        {
            var causes = failures.Select(f => f.Reason).Distinct().ToList();
            var refusal = causes.Count == 1 ? causes[0] : FtmoGroupRefusal.MemberRunRefused;
            return Refused(kind, refusal, failures, window: null, coverage: []);
        }

        var series = ordered
            .Select(m => new MemberSeries(m.MemberOrder, m.Projection!.ProjectedLow!, m.Projection.ProjectedHigh!))
            .ToList();

        // Stage 2: the common window. Empty blames no member and echoes every member's coverage.
        var window = Intersect(series);
        if (window is null)
            return Refused(kind, FtmoGroupRefusal.NoCommonWindow, [], window: null, Coverage(ordered, inWindow: null));

        // Stage 3: trim, sort, renumber globally. Sizing was fixed per member BEFORE this trim.
        var merged = Merge(series, window.Value);
        var windowDto = new FtmoGroupWindowDto(window.Value.Start, window.Value.End);
        var coverage = Coverage(ordered, merged.InWindowCountByMember);

        var empty = ordered
            .Where(m => merged.InWindowCountByMember[m.MemberOrder] == 0)
            .Select(m => new FtmoGroupMemberRefusalDto(m.StrategyId, m.Name, FtmoGroupRefusal.MemberHasNoTradesInWindow, null))
            .ToList();
        if (empty.Count > 0)
            return Refused(kind, FtmoGroupRefusal.MemberHasNoTradesInWindow, empty, windowDto, coverage);

        // Stage 4: the shipped replay, unedited. The merged series is never empty here (every member has rows).
        var sourceZone = p.SourceZone
            ?? throw new InvalidOperationException("No member resolved a source zone, yet every member passed (wiring defect).");

        var segments = ordered.Select(m => m.Projection!.Segment).Distinct().ToList();
        var segment = segments.Count == 1 ? segments[0] : BacktestSegment.Unknown;
        var unscalable = merged.Low.Count(t => t.Outcome == ResizeOutcome.Unscalable);

        var run = FtmoMultiStartReadService.ComputeRun(
            Guid.Empty, kind, segment, merged.Low, merged.High, sourceZone, p.BerlinZone, p.InitialCapital,
            p.DailyPct, p.MaxPct, p.ProfitTargetPct, p.EchoFxBand, unscalable, p.Rules, ct);

        return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Evaluated, null, [], windowDto, coverage, run);
    }

    private static FtmoGroupKindResultDto Refused(
        BacktestRunKind kind,
        FtmoGroupRefusal refusal,
        IReadOnlyList<FtmoGroupMemberRefusalDto> memberRefusals,
        FtmoGroupWindowDto? window,
        IReadOnlyList<FtmoGroupMemberCoverageDto> coverage)
        => new(kind, FtmoSimulationStatus.Refused, refusal, memberRefusals, window, coverage, Run: null);

    /// <summary>First open and last close over ALL of a member's rows (Unscalable included); in-window count when known.</summary>
    private static List<FtmoGroupMemberCoverageDto> Coverage(
        IReadOnlyList<GroupMemberKindInput> ordered, IReadOnlyList<int>? inWindow)
        => [.. ordered.Select(m =>
        {
            var rows = m.Projection!.ProjectedLow!;
            return new FtmoGroupMemberCoverageDto(
                m.StrategyId, m.Name,
                rows.Count == 0 ? null : rows.Min(t => t.OpenSource),
                rows.Count == 0 ? null : rows.Max(t => t.CloseSource),
                inWindow?[m.MemberOrder] ?? 0);
        })];
}
