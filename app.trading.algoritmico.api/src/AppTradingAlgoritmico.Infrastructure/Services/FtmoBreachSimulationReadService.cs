using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// PR P4 — composes the three PR P1/P2 pure units (<see cref="FtmoDayClock"/>,
/// <see cref="FtmoTradeProjector"/>, <see cref="FtmoBreachEvaluator"/>) plus PR P3's
/// <see cref="FtmoInstrumentSpec"/> row into one result per held run (design.md Data Flow).
/// <para>
/// The guard chain runs in the ORDER design.md specifies: product -&gt; limits -&gt; drawdown model
/// -&gt; instrument spec -&gt; point-value-null check -&gt; FX band -&gt; normalizer -&gt; segments -&gt;
/// timezone -&gt; request validity. The first eight guards are properties of the STRATEGY/request,
/// not of one run, so they are evaluated ONCE; a failure there refuses every held run with the same
/// reason. The normalizer and run-segments guards are properties of one run's own trades, so they
/// are evaluated PER RUN — one run's <see cref="FtmoSimulationRefusal.RiskNotEstimable"/> does not
/// refuse a sibling run (spec.md "Two runs give two results").
/// </para>
/// <para>
/// <b>The null-PointValue check is load-bearing</b> (design.md Decision 6): <c>CalibrationStatus.Calibrated</c>
/// is enum value 0, the CLR default, so this checks <c>PointValue is null</c> explicitly rather than
/// trusting a non-default <c>Status</c> alone. This is exactly the NQ (<c>USATECHIDXUSD_M1_UTC02</c>)
/// case: measured <c>Inconsistent</c> with a null point value.
/// </para>
/// <para>
/// Narrow projections only: this service issues its OWN queries and never routes through
/// <c>BacktestReadService.cs:232-246</c>'s existing <c>TryNormalize</c>/<c>TradeResizer</c> pairing
/// (a known pre-existing lot-count defect for unequal point values, out of scope here) and never
/// touches slice A's/slice B's read services.
/// </para>
/// </summary>
public sealed class FtmoBreachSimulationReadService(AppDbContext db) : IFtmoBreachSimulationReadService
{
    public async Task<FtmoBreachSimulationDto> SimulateAsync(FtmoBreachSimulationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = await FtmoSimulationInputs.ResolveSharedAsync(db, request, ct);

        if (resolution.NoRuns)
            return new FtmoBreachSimulationDto(request.StrategyId, []);

        if (resolution.Refusal is not null)
            return RefuseAll(request.StrategyId, resolution.Runs, resolution.Refusal.Value);

        var results = new List<FtmoRunSimulationResultDto>(resolution.Runs.Count);
        foreach (var run in resolution.Runs)
        {
            var trades = await db.BacktestTrades.AsNoTracking()
                .Where(t => t.BacktestRunId == run.Id)
                .ToListAsync(ct);

            results.Add(SimulateRun(
                run.Id, run.Kind, trades, resolution.SourceGrid!, resolution.FtmoGrid!, request.TargetRiskPerTrade,
                resolution.PointValue, resolution.Spec!.ContractSize, resolution.FxBand, resolution.SourceZone!, resolution.BerlinZone!,
                request.InitialCapital, resolution.DailyPct, resolution.MaxPct, resolution.ProfitTargetPct));
        }

        return new FtmoBreachSimulationDto(request.StrategyId, results);
    }

    private static FtmoRunSimulationResultDto SimulateRun(
        Guid runId,
        BacktestRunKind kind,
        List<BacktestTrade> trades,
        LotGrid sourceGrid,
        LotGrid ftmoGrid,
        decimal targetRiskPerTrade,
        decimal pointValue,
        decimal contractSize,
        (decimal Low, decimal High) fxBand,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal initialCapital,
        decimal dailyPct,
        decimal maxPct,
        decimal? profitTargetPct)
    {
        var projection = FtmoSimulationInputs.ProjectRun(
            trades, sourceGrid, ftmoGrid, targetRiskPerTrade, pointValue, contractSize, fxBand);
        if (projection.Refusal is not null)
            return Refused(runId, kind, projection.Refusal.Value, projection.Segment);

        var lowEvaluation = FtmoBreachEvaluator.Evaluate(
            projection.ProjectedLow!, sourceZone, berlinZone, initialCapital, dailyPct, maxPct);
        var highEvaluation = FtmoBreachEvaluator.Evaluate(
            projection.ProjectedHigh!, sourceZone, berlinZone, initialCapital, dailyPct, maxPct);

        // ftmo-first-breach-timing (design.md Data Flow): the anchor and elapsed-day close-set do not
        // depend on FX (open/close instants are shared by both projections) — either FX end's
        // projected trades yields the same anchor and close-day set. Called ONCE per run.
        var anchor = FtmoReplayCalendar.Build(projection.ProjectedLow!, sourceZone, berlinZone);

        var (dailyFirstBreachMerged, daily) = MergeFinding(
            lowEvaluation.Daily, highEvaluation.Daily, anchor, projection.ProjectedLow!, sourceZone, berlinZone);
        var (maxFirstBreachMerged, max) = MergeFinding(
            lowEvaluation.Max, highEvaluation.Max, anchor, projection.ProjectedLow!, sourceZone, berlinZone);

        var firstLimit = FtmoBreachTiming.FirstLimit(dailyFirstBreachMerged?.Point, maxFirstBreachMerged?.Point);
        FtmoFirstLimitBreachDto? firstLimitBreach = null;
        if (firstLimit is not null)
        {
            var winning = firstLimit == FtmoFirstBreachingLimit.Max ? maxFirstBreachMerged!.Value : dailyFirstBreachMerged!.Value;
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, winning.Point.FtmoDay, winning.Point.SourceTime, projection.ProjectedLow!, sourceZone, berlinZone);
            firstLimitBreach = new FtmoFirstLimitBreachDto(
                firstLimit.Value, winning.Point.SourceTime, winning.Point.FtmoDay, tradingDays, calendarDays);
        }

        // ftmo-challenge-race (design.md Data Flow): a separate, non-truncating composition over the
        // same trade series (hard rule 1 — FtmoBreachEvaluator.cs is not edited or truncated by this).
        // ftmo-multi-start PR1 apply follow-up (option A): lowEvaluation/highEvaluation above are already
        // FtmoBreachEvaluator.Evaluate on these exact projected series, at these exact capital/loss-limit
        // pcts — the same phase-1 evaluation the race would otherwise recompute internally. Reused here.
        var challengeRace = FtmoChallengeRace.Evaluate(
            profitTargetPct, projection.ProjectedLow!, projection.ProjectedHigh!, sourceZone, berlinZone, initialCapital, dailyPct, maxPct,
            lowEvaluation, highEvaluation);

        return new FtmoRunSimulationResultDto(
            runId, kind, projection.Segment, FtmoSimulationStatus.Evaluated, Refusal: null,
            daily, max,
            projection.RaisedToMinimumCount, projection.CappedAtMaximumCount, projection.UnscalableCount,
            fxBand.Low, fxBand.High,
            FtmoRunSimulationResultDto.DefaultNotModelled,
            FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
        {
            FirstLimitBreach = firstLimitBreach,
            ReplayStartSourceTime = anchor.SourceOpen,
            ReplayStartFtmoDay = anchor.FtmoDay,
            ChallengeRace = challengeRace,
        };
    }

    /// <summary>
    /// FX-band merge (design.md Decision 3): disagreeing verdicts across <c>fxLow</c>/<c>fxHigh</c>
    /// become <see cref="FtmoBreachVerdict.BreachContingent"/> with cause <see cref="BreachContingencyCause.FxRoundingSensitive"/>.
    /// <para>
    /// ftmo-first-breach-timing: <c>FirstBreach</c>/<c>FirstCleanBreach</c> timing is merged
    /// SEPARATELY and INDEPENDENTLY from this verdict-level merge (design.md Decision 6) — this
    /// verdict/causes/disclosure branch is UNCHANGED by the addition. Returns the merged
    /// <c>FirstBreach</c> point (for the run-level first-limit pick) alongside the DTO.
    /// </para>
    /// </summary>
    private static ((FtmoBreachEvaluator.BreachPoint Point, FtmoFxBandEnd End)? FirstBreachMerged, FtmoLimitFindingDto Dto) MergeFinding(
        FtmoBreachEvaluator.FtmoLimitFinding low,
        FtmoBreachEvaluator.FtmoLimitFinding high,
        FtmoReplayCalendar.ReplayAnchor anchor,
        List<FtmoTradeProjector.ProjectedTrade> projectedForCalendar,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone)
    {
        var firstBreachMerged = FtmoBreachTiming.Earliest(low.FirstBreach, high.FirstBreach);
        var firstCleanBreachMerged = FtmoBreachTiming.Earliest(low.FirstCleanBreach, high.FirstCleanBreach);

        var firstBreachDto = ToTimingDto(firstBreachMerged, anchor, projectedForCalendar, sourceZone, berlinZone);
        var firstCleanBreachDto = ToTimingDto(firstCleanBreachMerged, anchor, projectedForCalendar, sourceZone, berlinZone);

        if (low.Verdict == high.Verdict)
        {
            return (firstBreachMerged, new FtmoLimitFindingDto(low.Verdict, low.Causes, low.DisclosureText)
            {
                FirstBreach = firstBreachDto,
                FirstCleanBreach = firstCleanBreachDto,
            });
        }

        var causes = new List<BreachContingencyCause> { BreachContingencyCause.FxRoundingSensitive };
        foreach (var cause in low.Causes.Concat(high.Causes))
        {
            if (!causes.Contains(cause))
                causes.Add(cause);
        }

        var text = "A closed-trade breach was detected, but it coincides with a condition that makes it "
            + "uncertain: " + string.Join(", ", causes) + ".";

        return (firstBreachMerged, new FtmoLimitFindingDto(FtmoBreachVerdict.BreachContingent, causes, text)
        {
            FirstBreach = firstBreachDto,
            FirstCleanBreach = firstCleanBreachDto,
        });
    }

    private static FtmoBreachTimingDto? ToTimingDto(
        (FtmoBreachEvaluator.BreachPoint Point, FtmoFxBandEnd End)? merged,
        FtmoReplayCalendar.ReplayAnchor anchor,
        List<FtmoTradeProjector.ProjectedTrade> projectedForCalendar,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone)
    {
        if (merged is null)
            return null;

        var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
            anchor, merged.Value.Point.FtmoDay, merged.Value.Point.SourceTime, projectedForCalendar, sourceZone, berlinZone);
        return FtmoBreachTiming.ToDto(merged.Value, tradingDays, calendarDays);
    }

    private static FtmoBreachSimulationDto RefuseAll(
        Guid strategyId, IEnumerable<(Guid Id, BacktestRunKind Kind)> runs, FtmoSimulationRefusal reason)
        => new(strategyId, [.. runs.Select(r => Refused(r.Id, r.Kind, reason))]);

    private static FtmoRunSimulationResultDto Refused(
        Guid runId, BacktestRunKind kind, FtmoSimulationRefusal reason, BacktestSegment segment = BacktestSegment.Unknown)
        => new(
            runId, kind, segment, FtmoSimulationStatus.Refused, reason,
            Daily: null, Max: null,
            RaisedToMinimumCount: 0, CappedAtMaximumCount: 0, UnscalableCount: 0,
            FxLow: null, FxHigh: null,
            FtmoRunSimulationResultDto.DefaultNotModelled,
            FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
        {
            FirstLimitBreach = null,
            ReplayStartSourceTime = null,
            ReplayStartFtmoDay = null,
            ChallengeRace = null,
        };
}
