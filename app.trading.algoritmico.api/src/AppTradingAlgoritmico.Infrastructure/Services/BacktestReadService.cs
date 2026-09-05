using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Read model for imported backtest data. Takes the full <see cref="AppDbContext"/> rather than
/// the importer's narrow <c>IBacktestDbContext</c> because showing WHOSE run a row is requires
/// joining <c>Strategies</c> — and the point of the narrow interface is to keep that join out of
/// the WRITE path, not to forbid it everywhere. Splitting the two is what lets design.md D2 stay
/// literally true instead of being widened for a display concern.
/// </summary>
public sealed class BacktestReadService(AppDbContext db) : IBacktestReadService
{
    public async Task<PagedResult<BacktestRunDto>> GetRunsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.BacktestRuns.AsNoTracking();
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                db.Strategies.AsNoTracking(),
                r => r.StrategyId,
                s => s.Id,
                (r, s) => new BacktestRunDto(
                    r.Id,
                    r.SourceFileName,
                    r.Symbol,
                    s.Id,
                    s.Name,
                    r.Kind,
                    r.Trades.Count,
                    r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<BacktestRunDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<BacktestTradeDto>> GetTradesByRunAsync(
        Guid runId, BacktestSegment? segment, int page, int pageSize, CancellationToken ct)
    {
        var query = db.BacktestTrades.AsNoTracking().Where(t => t.BacktestRunId == runId);
        if (segment is not null)
            query = query.Where(t => t.Segment == segment);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.RowIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new BacktestTradeDto(
                t.Id, t.RowIndex, t.Ticket, t.Symbol, t.Type, t.OpenTime, t.OpenPrice, t.Size,
                t.CloseTime, t.ClosePrice, t.Profit, t.Balance, t.SampleTypeRaw, t.Segment,
                t.SegmentIndex, t.CloseType, t.RealizedRisk, t.StopLoss, t.Comment))
            .ToListAsync(ct);

        return new PagedResult<BacktestTradeDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<SymbolCalibrationDto>> GetCalibrationsAsync(CancellationToken ct)
        => await db.SymbolCalibrations
            .AsNoTracking()
            .OrderBy(c => c.Symbol)
            .Select(c => new SymbolCalibrationDto(
                c.Symbol, c.PointValue, c.SampleCount, c.MinObserved, c.MaxObserved, c.Status, c.CalibratedAt))
            .ToListAsync(ct);

    public async Task<StrategyBacktestsDto> GetByStrategyAsync(Guid strategyId, CancellationToken ct)
    {
        var runs = await db.BacktestRuns
            .AsNoTracking()
            .Where(r => r.StrategyId == strategyId)
            .Select(r => new BacktestRunSummaryDto(
                r.Id, r.SourceFileName, r.Symbol, r.Kind, r.Trades.Count, r.CreatedAt))
            .ToListAsync(ct);

        var export = await db.StrategyWalkForwardExports
            .AsNoTracking()
            .Where(e => e.StrategyId == strategyId)
            .Select(e => new WalkForwardExportSummaryDto(
                e.Id, e.SourceFileName, e.OosFromDate, e.Windows.Count,
                e.DeployParameters, e.EvaluationParameters, e.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return new StrategyBacktestsDto(
            strategyId,
            runs.FirstOrDefault(r => r.Kind == BacktestRunKind.Deploy),
            runs.FirstOrDefault(r => r.Kind == BacktestRunKind.Evaluation),
            export);
    }

    // =====================================================================
    // Group risk analysis (design.md D8/D8a/D8b) — the first production
    // caller of slice 2a's TryNormalize/Resize, and the only door through
    // which a backtest-derived VaR or correlation reaches the API.
    // =====================================================================

    /// <summary>
    /// Correlation and VaR for ONE caller-named group of strategies, computed over the ONE sample
    /// the caller asked for.
    /// <para>
    /// The pipeline is: refuse the request if it cannot name a sample → derive every run's segment
    /// in ONE projection over the whole group → select each member's run among its at-most-two rows
    /// → estimate its own risk per trade → resize onto the operator's target and lot grid → bridge
    /// to a dated net series → analytics. Every stage can only produce a series or a NAMED refusal;
    /// none of them can produce a zero.
    /// </para>
    /// <para>
    /// <b>A refused member refuses the analysis.</b> Dropping it and computing over the rest would
    /// answer a different question from the one asked, quietly — the same failure class as
    /// publishing a withheld figure as <c>0</c>. The per-member rows are returned either way, so a
    /// refusal always says which member and why.
    /// </para>
    /// </summary>
    public async Task<GroupRiskAnalysisDto> GetGroupRiskAnalysisAsync(
        GroupRiskAnalysisRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // --- Request-side refusals. Two rules about Unknown, kept distinct. ---

        // (1) No segment at all. Without one every figure would be silently in-sample, which is the
        //     number most likely to be optimistic. The field is nullable precisely so this state is
        //     expressible at all: on a non-nullable enum an omitted property binds to Unknown.
        if (request.Segment is null)
            return Refused(GroupRiskAnalysisStatus.SegmentNotSpecified, "No BacktestSegment was specified.");

        // (2) Unknown was asked for. Different reasoning, same outcome: Unknown is the label the
        //     parser assigns when it CANNOT classify a sample type, so a figure carrying it would
        //     assert something the data does not support.
        if (request.Segment == BacktestSegment.Unknown)
        {
            return Refused(
                GroupRiskAnalysisStatus.UnknownSegmentNotSelectable,
                "BacktestSegment.Unknown is not a selectable sample: it marks a label the importer "
                + "could not classify, so a figure computed over it would state a provenance the data "
                + "does not establish.");
        }

        var segment = request.Segment.Value;

        // (3) No capital to divide by. `InitialCapital` is the denominator of every percentage on
        //     the payload; on a non-nullable decimal an omitted query parameter binds to 0, so
        //     without this rule the caller who forgot is answered with real currency figures and
        //     incomputable percentages beside them.
        if (request.InitialCapital <= 0m)
        {
            return Refused(
                GroupRiskAnalysisStatus.InvalidInitialCapital,
                $"InitialCapital {request.InitialCapital} is not a capital base: it is the "
                + "denominator of every percentage this analysis publishes, and an omitted query "
                + "parameter binds to 0, so a non-positive value cannot be distinguished from an "
                + "unstated one.");
        }

        var strategyIds = request.StrategyIds?.Distinct().ToList() ?? [];
        if (strategyIds.Count == 0)
            return Refused(GroupRiskAnalysisStatus.NoStrategiesRequested, "No strategies were named.");

        var grid = request.TryBuildGrid();
        if (grid is null)
        {
            return Refused(
                GroupRiskAnalysisStatus.InvalidLotGrid,
                $"The requested lot grid (decimals {request.SizeDecimals}, step {request.Step}, "
                + $"min {request.MinLot}, max {request.MaxLots}) is not a valid grid.");
        }

        var labels = await db.Strategies
            .AsNoTracking()
            .Where(s => strategyIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var missing = strategyIds.Where(id => !labels.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return Refused(
                GroupRiskAnalysisStatus.StrategyNotFound,
                "No such strategy: " + string.Join(", ", missing));
        }

        // --- ONE server-side projection for the WHOLE group (the ReadinessRows precedent). ---
        var segmentRows = await RunSegmentSelection
            .SegmentRows(db.BacktestRuns.AsNoTracking(), db.BacktestTrades.AsNoTracking(), strategyIds)
            .ToListAsync(ct);

        var weights = await MemberWeightsAsync(request.PortfolioId, strategyIds, ct);

        // --- Select each member's run. Request order is preserved so the payload is deterministic. ---
        var selections = new List<(Guid StrategyId, string Label, RunSelectionResult Selection)>();
        var members = new List<GroupRiskMemberDto>();
        foreach (var strategyId in strategyIds)
        {
            var rows = segmentRows.Where(r => r.StrategyId == strategyId).ToList();
            var selection = RunSegmentSelection.Select(rows, segment, request.RunKind);
            selections.Add((strategyId, labels[strategyId], selection));
            members.Add(DescribeSelection(strategyId, labels[strategyId], selection));
        }

        if (members.Any(m => m.Status != GroupRiskMemberStatus.Resolved))
            return RefusedMembers(members);

        // --- Trades for every selected run, in ONE query. ---
        var selectedRunIds = selections.Select(s => s.Selection.Run!.RunId).ToList();
        var tradesByRun = (await db.BacktestTrades
                .AsNoTracking()
                .Where(t => selectedRunIds.Contains(t.BacktestRunId))
                .OrderBy(t => t.RowIndex)
                .ToListAsync(ct))
            .GroupBy(t => t.BacktestRunId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BacktestTrade>)[.. g]);

        // --- TryNormalize -> Resize -> Bridge, per member. ---
        var series = new List<BacktestNetSeries>(selections.Count);
        members.Clear();
        foreach (var (strategyId, label, selection) in selections)
        {
            var run = selection.Run!;
            var trades = tradesByRun.TryGetValue(run.RunId, out var held) ? held : [];

            if (!TradeRiskNormalizer.TryNormalize(trades, grid, out var profile))
            {
                members.Add(new GroupRiskMemberDto(
                    strategyId, label, GroupRiskMemberStatus.RiskNotEstimable,
                    run.Segment, run.Kind, run.RunId,
                    $"Run {run.RunId} produced no risk-per-trade estimate from its own SL closes, so "
                    + "there is nothing to resize onto the requested target."));
                continue;
            }

            var resized = TradeResizer.Resize(profile!, request.TargetRiskPerTrade, grid);
            var weight = weights.TryGetValue(strategyId, out var w) ? w : 1m;

            var built = BacktestNetSeries.Bridge.Build(
                trades, resized, strategyId, label, request.FundingService, run.Segment!.Value, weight);

            if (built.Status != BacktestNetSeriesStatus.Built)
            {
                members.Add(new GroupRiskMemberDto(
                    strategyId, label, GroupRiskMemberStatus.NonUnitWeight,
                    run.Segment, run.Kind, run.RunId,
                    $"{label} carries an allocation weight of {built.OfferedWeight}. The series is "
                    + $"already sized at {request.TargetRiskPerTrade} per trade, so applying a second "
                    + "factor would double-size it."));
                continue;
            }

            series.Add(built.Series!);
            members.Add(new GroupRiskMemberDto(
                strategyId, label, GroupRiskMemberStatus.Resolved,
                run.Segment, run.Kind, run.RunId, Detail: null));
        }

        if (members.Any(m => m.Status != GroupRiskMemberStatus.Resolved))
            return RefusedMembers(members);

        // --- One sample label, or no figure. ---
        var disagreement = DescribeSegmentDisagreement([.. series.Select(s => (s.Label, s.Segment))]);
        if (disagreement is not null)
        {
            return new GroupRiskAnalysisDto(
                GroupRiskAnalysisStatus.HeterogeneousGroup, Segment: null, members,
                Risk: null, Correlation: null, Refusal: disagreement);
        }

        var risk = PortfolioAnalyticsCalculator.ComputeVaR(request.InitialCapital, series);
        var correlation = PortfolioAnalyticsCalculator.ComputeCorrelation(series);

        return new GroupRiskAnalysisDto(
            GroupRiskAnalysisStatus.Completed,
            segment,
            members,
            risk with { VarTarget = await VarTargetAsync(request.FundingService, risk, ct) },
            correlation,
            Refusal: null);
    }

    /// <summary>
    /// The group-level refusal in words when the members' selected runs do not share one segment,
    /// or null when they do (design.md D8b).
    /// <para>
    /// <b>Stated as a named refusal even though this read path cannot currently reach it.</b>
    /// Selection matches the requested segment EXACTLY, so every selected run carries that one
    /// segment and the group is homogeneous by construction. The check stays because the calculator
    /// throws on a mixed group as a backstop, and a throw is not something an operator can act on:
    /// if any future door composes a group differently, this is the sentence it must produce
    /// instead. Keeping it public and pure is what makes the claim testable rather than asserted.
    /// </para>
    /// </summary>
    public static string? DescribeSegmentDisagreement(IReadOnlyList<(string Label, BacktestSegment Segment)> members)
    {
        var disagreeing = RunSegmentSelection.DisagreeingSegments(members);
        if (disagreeing.Count == 0) return null;

        return "A correlation or VaR figure implies ONE sample label, but the members disagree: "
            + string.Join(", ", disagreeing.Select(m => $"{m.Label} is {m.Segment}"))
            + ". No figure is computed with a mixed label and no majority segment is assumed.";
    }

    /// <summary>
    /// Turns a run selection into the member row that reports it, naming what the refusal is about.
    /// </summary>
    private static GroupRiskMemberDto DescribeSelection(Guid strategyId, string label, RunSelectionResult selection)
        => selection.Status switch
        {
            GroupRiskMemberStatus.Resolved => new GroupRiskMemberDto(
                strategyId, label, GroupRiskMemberStatus.Resolved,
                selection.Run!.Segment, selection.Run.Kind, selection.Run.RunId, Detail: null),

            GroupRiskMemberStatus.RunSegmentsDisagree => new GroupRiskMemberDto(
                strategyId, label, selection.Status, null, null, null,
                "Trades of run " + string.Join(", ", selection.DisagreeingRunIds)
                + " carry more than one BacktestSegment. The importer rejects any file holding more "
                + "than one sample type, so those rows were edited outside it."),

            GroupRiskMemberStatus.AmbiguousRunSelection => new GroupRiskMemberDto(
                strategyId, label, selection.Status, null, null, null,
                $"{label} has runs in BOTH slots carrying the requested segment ("
                + string.Join(" and ", selection.CandidateKinds)
                + "). They are two parameter sets over the SAME sample, so picking either would make "
                + "the published figure depend on an arbitrary choice. Name a run kind to disambiguate."),

            _ => new GroupRiskMemberDto(
                strategyId, label, GroupRiskMemberStatus.NoEvidenceForSegment, null, null, null,
                $"{label} has no run carrying the requested segment. There is no series for it — "
                + "not an empty one."),
        };

    /// <summary>
    /// Each member's allocation weight, or an empty map when the request names no portfolio. A bare
    /// group carries no allocation decision, so every member's weight is <c>1</c>.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> MemberWeightsAsync(
        Guid? portfolioId, IReadOnlyCollection<Guid> strategyIds, CancellationToken ct)
        => portfolioId is null
            ? []
            : await db.PortfolioStrategies
                .AsNoTracking()
                .Where(ps => ps.PortfolioId == portfolioId.Value && strategyIds.Contains(ps.StrategyId))
                .ToDictionaryAsync(ps => ps.StrategyId, ps => ps.Weight, ct);

    /// <summary>
    /// The shipped band readout for the group's funding service, or null.
    /// <para>
    /// This slice NEVER derives a band position of its own. The KB's target-VaR determination walks
    /// up to six months of historical VaR until the max/min ratio reaches 2:1, over the last 45 days
    /// of OPEN positions — neither is implemented here, and neither is reachable from a percentile
    /// of realized closes. What is published is the operator's OWN configured band next to
    /// <c>MonthlyVar95 / InitialCapital</c>, with the denominator stated.
    /// </para>
    /// </summary>
    private async Task<VarTargetReadoutDto?> VarTargetAsync(
        string? fundingService, BacktestPortfolioRiskDto risk, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fundingService)) return null;

        var limits = await db.BrokerRiskLimits
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Broker == fundingService, ct);

        if (limits is null || limits.Kind != GuardrailKind.VarTarget) return null;

        var impliedMultiplier = limits.TargetVarPct is decimal target
            && risk.MonthlyVar95Percent is decimal measured && measured > 0
            ? target / measured
            : (decimal?)null;

        return new VarTargetReadoutDto(
            TargetVarPct: limits.TargetVarPct,
            VarFloorPct: limits.VarFloorPct,
            HorizonDays: AnalyticsSeries.MonthlyVarHorizonDays,
            InsufficientHistory: risk.MonthlyVar95Withheld == VarWithholdReason.InsufficientHistory,
            ObservationDays: risk.ObservationDays,
            OverlappingWindows: risk.MonthlyVarOverlappingWindows,
            IndependentWindows: risk.MonthlyVarIndependentWindows,
            MonthlyVar95: risk.MonthlyVar95,
            MonthlyVar95Percent: risk.MonthlyVar95Percent,
            ImpliedMultiplier: impliedMultiplier);
    }

    private static GroupRiskAnalysisDto Refused(GroupRiskAnalysisStatus status, string refusal)
        => new(status, Segment: null, Members: [], Risk: null, Correlation: null, Refusal: refusal);

    /// <summary>
    /// The group takes the status of the FIRST refused member in request order — deterministic, and
    /// it keeps every member's own row so nothing about the other refusals is lost.
    /// </summary>
    private static GroupRiskAnalysisDto RefusedMembers(IReadOnlyList<GroupRiskMemberDto> members)
    {
        var first = members.First(m => m.Status != GroupRiskMemberStatus.Resolved);
        var status = first.Status switch
        {
            GroupRiskMemberStatus.RunSegmentsDisagree => GroupRiskAnalysisStatus.RunSegmentsDisagree,
            GroupRiskMemberStatus.AmbiguousRunSelection => GroupRiskAnalysisStatus.AmbiguousRunSelection,
            GroupRiskMemberStatus.RiskNotEstimable => GroupRiskAnalysisStatus.RiskNotEstimable,
            GroupRiskMemberStatus.NonUnitWeight => GroupRiskAnalysisStatus.NonUnitWeight,
            _ => GroupRiskAnalysisStatus.NoEvidenceForSegment,
        };

        return new GroupRiskAnalysisDto(status, Segment: null, members, Risk: null, Correlation: null, first.Detail);
    }
}
