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
    private const string BerlinIanaId = "Europe/Berlin";
    private const string UsdCurrency = "USD";

    public async Task<FtmoBreachSimulationDto> SimulateAsync(FtmoBreachSimulationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runs = await db.BacktestRuns.AsNoTracking()
            .Where(r => r.StrategyId == request.StrategyId)
            .Select(r => new { r.Id, r.Kind })
            .ToListAsync(ct);

        if (runs.Count == 0)
            return new FtmoBreachSimulationDto(request.StrategyId, []);

        var limits = await db.BrokerRiskLimits.AsNoTracking()
            .Where(l => l.Broker == request.Broker && l.Kind == GuardrailKind.LossLimits)
            .FirstOrDefaultAsync(ct);

        // A null percentage means the rule is NOT configured — never a 0% rule: 0% would put the max
        // floor at initial capital and read every loss as a false Breached. A stored value outside
        // (0, 1] is not a usable rule either (the fraction contract RiskLimitsService enforces for
        // stages and VaR targets). Both are properties of the stored configuration, not of the
        // request, so they refuse as LimitsNotConfigured, exactly like a missing row. The pattern
        // binds dailyPct/maxPct: the compiler, not a fallback value, proves them set past this guard.
        if (limits is not
            {
                DailyLossLimitPct: { } dailyPct and > 0m and <= 1m,
                MaxLossLimitPct: { } maxPct and > 0m and <= 1m,
            })
        {
            return RefuseAll(request.StrategyId, runs.Select(r => (r.Id, r.Kind)), FtmoSimulationRefusal.LimitsNotConfigured);
        }

        FtmoSimulationRefusal? sharedRefusal =
            limits.FtmoProduct is null || limits.FtmoProduct != FtmoProduct.TwoStep ? FtmoSimulationRefusal.ProductNotTwoStep
            : limits.DrawdownModel != DrawdownModel.Static ? FtmoSimulationRefusal.DrawdownModelNotStatic
            : null;

        FtmoInstrumentSpec? spec = null;
        LotGrid? ftmoGrid = null;
        if (sharedRefusal is null)
        {
            spec = await db.FtmoInstrumentSpecs.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SqxSymbol == request.SqxSymbol, ct);

            if (spec is null || spec.ContractSize <= 0m)
            {
                sharedRefusal = FtmoSimulationRefusal.InstrumentSpecMissing;
            }
            else
            {
                try
                {
                    ftmoGrid = new LotGrid(spec.SizeDecimals, spec.Step, spec.MinLot, spec.MaxLots);
                }
                catch (ArgumentOutOfRangeException)
                {
                    sharedRefusal = FtmoSimulationRefusal.InstrumentSpecMissing;
                }
            }
        }

        var pointValue = 0m;
        if (sharedRefusal is null)
        {
            var calibration = await db.SymbolCalibrations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Symbol == request.SqxSymbol, ct);

            // Load-bearing null check (design.md Decision 6): Calibrated == 0 is the CLR default, so
            // a null PointValue must be checked independently of Status. Covers NQ.
            if (calibration is null
                || calibration.Status != CalibrationStatus.Calibrated
                || calibration.PointValue is null
                || calibration.PointValue.Value <= 0m)
            {
                sharedRefusal = FtmoSimulationRefusal.PointValueNotCalibrated;
            }
            else
            {
                pointValue = calibration.PointValue.Value;
            }
        }

        (decimal Low, decimal High) fxBand = (1m, 1m);
        if (sharedRefusal is null)
        {
            var sameCurrency = string.Equals(spec!.ProfitCurrency, UsdCurrency, StringComparison.OrdinalIgnoreCase);
            if (!sameCurrency)
            {
                if (request.FxLow is null || request.FxHigh is null)
                {
                    sharedRefusal = FtmoSimulationRefusal.FxRateNotDeclared;
                }
                else if (request.FxLow.Value <= 0m || request.FxHigh.Value <= 0m || request.FxLow.Value > request.FxHigh.Value)
                {
                    sharedRefusal = FtmoSimulationRefusal.InvalidFxBand;
                }
                else
                {
                    fxBand = (request.FxLow.Value, request.FxHigh.Value);
                }
            }
        }

        var sourceGrid = request.TryBuildSourceGrid();
        if (sharedRefusal is null
            && (sourceGrid is null || request.InitialCapital <= 0m || request.TargetRiskPerTrade <= 0m))
        {
            sharedRefusal = FtmoSimulationRefusal.InvalidRequest;
        }

        TimeZoneInfo? sourceZone = null;
        TimeZoneInfo? berlinZone = null;
        if (sharedRefusal is null)
        {
            // Known, deliberate test gap: the Berlin disjunct is unreachable in practice because
            // BerlinIanaId is a hardcoded, always-shipped IANA id. It stays as a defensive guard for
            // a host without ICU/tz data; no test contorts the environment to reach it.
            if (!FtmoDayClock.TryResolveZone(spec!.SourceTimeZoneId, out sourceZone)
                || !FtmoDayClock.TryResolveZone(BerlinIanaId, out berlinZone))
            {
                sharedRefusal = FtmoSimulationRefusal.TimeZoneDataUnavailable;
            }
        }

        if (sharedRefusal is not null)
            return RefuseAll(request.StrategyId, runs.Select(r => (r.Id, r.Kind)), sharedRefusal.Value);

        var results = new List<FtmoRunSimulationResultDto>(runs.Count);
        foreach (var run in runs)
        {
            var trades = await db.BacktestTrades.AsNoTracking()
                .Where(t => t.BacktestRunId == run.Id)
                .ToListAsync(ct);

            results.Add(SimulateRun(
                run.Id, run.Kind, trades, sourceGrid!, ftmoGrid!, request.TargetRiskPerTrade,
                pointValue, spec!.ContractSize, fxBand, sourceZone!, berlinZone!,
                request.InitialCapital, dailyPct, maxPct));
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
        decimal maxPct)
    {
        var segment = trades.Count > 0 ? trades[0].Segment : BacktestSegment.Unknown;

        if (trades.Select(t => t.Segment).Distinct().Count() > 1)
            return Refused(runId, kind, FtmoSimulationRefusal.RunSegmentsDisagree, segment);

        // The RiskPerTrade-is-null disjunct is unreachable through TryNormalize: it returns true only
        // for Status == Estimated, which always carries a value. Kept as a null guard, not a tested path.
        if (!TradeRiskNormalizer.TryNormalize(trades, sourceGrid, out var profile)
            || profile!.Estimate.RiskPerTrade is null)
        {
            return Refused(runId, kind, FtmoSimulationRefusal.RiskNotEstimable, segment);
        }

        var estimated = profile.Estimate.RiskPerTrade!.Value;

        // Through this service, RefuseRunInputs' estimatedRisk <= 0 and target <= 0 branches are
        // unreachable: Â is |RealizedRisk| of a non-zero SL row (strictly positive), and a non-positive
        // target was already refused as InvalidRequest above. Both are covered in FtmoTradeProjectorTests.
        var runInputRefusal = FtmoTradeProjector.RefuseRunInputs(
            estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.Low);
        if (runInputRefusal is not null)
            return Refused(runId, kind, runInputRefusal.Value, segment);

        var low = EvaluateAtFx(
            trades, estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.Low, ftmoGrid,
            sourceZone, berlinZone, initialCapital, dailyPct, maxPct);
        var high = EvaluateAtFx(
            trades, estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.High, ftmoGrid,
            sourceZone, berlinZone, initialCapital, dailyPct, maxPct);

        // ftmo-first-breach-timing (design.md Data Flow): the anchor and elapsed-day close-set do not
        // depend on FX (open/close instants are shared by both projections) — either FX end's
        // projected trades yields the same anchor and close-day set. Called ONCE per run.
        var anchor = FtmoReplayCalendar.Build(low.Projected, sourceZone, berlinZone);

        var (dailyFirstBreachMerged, daily) = MergeFinding(low.Evaluation.Daily, high.Evaluation.Daily, anchor, low.Projected, sourceZone, berlinZone);
        var (maxFirstBreachMerged, max) = MergeFinding(low.Evaluation.Max, high.Evaluation.Max, anchor, low.Projected, sourceZone, berlinZone);

        var firstLimit = FtmoBreachTiming.FirstLimit(dailyFirstBreachMerged?.Point, maxFirstBreachMerged?.Point);
        FtmoFirstLimitBreachDto? firstLimitBreach = null;
        if (firstLimit is not null)
        {
            var winning = firstLimit == FtmoFirstBreachingLimit.Max ? maxFirstBreachMerged!.Value : dailyFirstBreachMerged!.Value;
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, winning.Point.FtmoDay, low.Projected, sourceZone, berlinZone);
            firstLimitBreach = new FtmoFirstLimitBreachDto(
                firstLimit.Value, winning.Point.SourceTime, winning.Point.FtmoDay, tradingDays, calendarDays);
        }

        return new FtmoRunSimulationResultDto(
            runId, kind, segment, FtmoSimulationStatus.Evaluated, Refusal: null,
            daily, max,
            low.Raised, low.Capped, low.Unscalable,
            fxBand.Low, fxBand.High,
            FtmoRunSimulationResultDto.DefaultNotModelled,
            FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
        {
            FirstLimitBreach = firstLimitBreach,
            ReplayStartSourceTime = anchor.SourceOpen,
            ReplayStartFtmoDay = anchor.FtmoDay,
        };
    }

    private readonly record struct FxEvaluation(
        FtmoBreachEvaluator.FtmoBreachEvaluation Evaluation,
        List<FtmoTradeProjector.ProjectedTrade> Projected,
        int Raised,
        int Capped,
        int Unscalable);

    private static FxEvaluation EvaluateAtFx(
        List<BacktestTrade> trades,
        decimal estimated,
        decimal target,
        decimal pointValue,
        decimal contractSize,
        decimal fx,
        LotGrid ftmoGrid,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal initialCapital,
        decimal dailyPct,
        decimal maxPct)
    {
        var projected = trades
            .Select(t => FtmoTradeProjector.Project(t, estimated, target, pointValue, contractSize, fx, ftmoGrid))
            .ToList();

        var evaluation = FtmoBreachEvaluator.Evaluate(projected, sourceZone, berlinZone, initialCapital, dailyPct, maxPct);

        return new FxEvaluation(
            evaluation,
            projected,
            projected.Count(p => p.Outcome == ResizeOutcome.RaisedToMinimum),
            projected.Count(p => p.Outcome == ResizeOutcome.CappedAtMaximum),
            projected.Count(p => p.Outcome == ResizeOutcome.Unscalable));
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
            anchor, merged.Value.Point.FtmoDay, projectedForCalendar, sourceZone, berlinZone);
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
        };
}
