using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-multi-start PR4 (design.md Data Flow) — replays the FTMO 2-Step challenge race plus a funded
/// phase from every FTMO-month start the data offers, and reports the distribution of chain outcomes.
/// Reuses the shipped guard chain and projection (<see cref="FtmoSimulationInputs"/>, PR3), the shipped
/// two-phase race (<see cref="FtmoChallengeRace"/>, PR0/PR1, UNEDITED beyond its own PR0/PR1 additions),
/// the funded phase (<see cref="FtmoFundedPhase"/>, PR1), the three-phase merge/classification
/// (<see cref="FtmoMultiStartChain"/>, PR1), start enumeration/slicing (<see cref="FtmoStartEnumerator"/>,
/// PR2) and order statistics (<see cref="FtmoOrderStatistics"/>, PR2) — this service composes those
/// pure units, it introduces no new race/evaluation logic of its own (spec.md, design.md Decision 8).
/// <para>
/// Synchronous per start; <see cref="CancellationToken.ThrowIfCancellationRequested"/> is checked
/// before each start (design.md Decision 9/D10) so a caller-requested cancellation is honoured before
/// the next (potentially expensive) start begins.
/// </para>
/// </summary>
public sealed class FtmoMultiStartReadService(AppDbContext db) : IFtmoMultiStartReadService
{
    /// <summary>Disclosure (design.md "Disclosure"; spec.md "Every Run Discloses Non-Independence And Optimistic Bias"). Never affirms survival.</summary>
    internal const string Disclosure =
        "Consecutive monthly starts share most of their trades, so these starts are not independent "
        + "trials. These figures describe this backtest; they are not probabilities or a forecast. "
        + "Unmodelled swap and closed-trade replay make targets easier to reach and understate breaches. "
        + "The funded phase models no reward withdrawal and no Scaling Plan, which keeps profit as "
        + "cushion — that omission is also optimistic.";

    /// <summary>The three right-censored outcomes (spec.md "Six Chain Outcomes..."): data ran out before a decision.</summary>
    private static readonly IReadOnlyCollection<FtmoChainOutcome> CensoredOutcomes =
    [
        FtmoChainOutcome.Phase1UndecidedAtEndOfData,
        FtmoChainOutcome.Phase2UndecidedAtEndOfData,
        FtmoChainOutcome.FundedNoBreachAtEndOfData,
    ];

    public async Task<FtmoMultiStartDto> SimulateAsync(FtmoBreachSimulationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = await FtmoSimulationInputs.ResolveSharedAsync(db, request, ct);

        if (resolution.NoRuns)
            return new FtmoMultiStartDto(request.StrategyId, []);

        if (resolution.Refusal is not null)
            return RefuseAll(request.StrategyId, resolution.Runs, resolution.Refusal.Value);

        var results = new List<FtmoMultiStartRunDto>(resolution.Runs.Count);
        foreach (var run in resolution.Runs)
        {
            ct.ThrowIfCancellationRequested();

            var trades = await db.BacktestTrades.AsNoTracking()
                .Where(t => t.BacktestRunId == run.Id)
                .ToListAsync(ct);

            results.Add(SimulateRun(
                run.Id, run.Kind, trades, resolution.SourceGrid!, resolution.FtmoGrid!, request.TargetRiskPerTrade,
                resolution.PointValue, resolution.Spec!.ContractSize, resolution.FxBand, resolution.SourceZone!,
                resolution.BerlinZone!, request.InitialCapital, resolution.DailyPct, resolution.MaxPct,
                resolution.ProfitTargetPct, ct));
        }

        return new FtmoMultiStartDto(request.StrategyId, results);
    }

    private static FtmoMultiStartRunDto SimulateRun(
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
        decimal? profitTargetPct,
        CancellationToken ct)
    {
        var rules = new FtmoChallengeRulesDto(
            FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
            FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null);

        var projection = FtmoSimulationInputs.ProjectRun(
            trades, sourceGrid, ftmoGrid, targetRiskPerTrade, pointValue, contractSize, fxBand);
        if (projection.Refusal is not null)
        {
            return Refused(runId, kind, projection.Refusal.Value, projection.Segment, rules, fxBand);
        }

        return ComputeRun(
            runId, kind, projection.Segment, projection.ProjectedLow!, projection.ProjectedHigh!,
            sourceZone, berlinZone, initialCapital, dailyPct, maxPct, profitTargetPct, fxBand,
            projection.UnscalableCount, rules, ct);
    }

    /// <summary>
    /// Everything from the single-start anchor onward — start enumeration, per-start phase 1/2/funded,
    /// the three-phase merge and classification, and the aggregate order statistics (design.md Data
    /// Flow) — taking ALREADY-PROJECTED trades. Split out from <see cref="SimulateRun"/> so the pure,
    /// CPU-bound composition can be measured directly (<c>FtmoMultiStartBenchmarkTests</c>) without a
    /// database round trip, using the SAME production method the DB-backed path calls.
    /// </summary>
    internal static FtmoMultiStartRunDto ComputeRun(
        Guid runId,
        BacktestRunKind kind,
        BacktestSegment segment,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> projectedLow,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> projectedHigh,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal initialCapital,
        decimal dailyPct,
        decimal maxPct,
        decimal? profitTargetPct,
        (decimal Low, decimal High) fxBand,
        int unscalableCount,
        FtmoChallengeRulesDto rules,
        CancellationToken ct)
    {
        var singleStartAnchor = FtmoReplayCalendar.Build(projectedLow, sourceZone, berlinZone).SourceOpen;

        if (profitTargetPct is not null && profitTargetPct.Value != FtmoChallengeRules.Phase1TargetPct)
        {
            return new FtmoMultiStartRunDto(
                runId, kind, segment, FtmoSimulationStatus.Evaluated, Refusal: null,
                FtmoChallengeRaceRefusal.ProfitTargetMismatch, profitTargetPct, FtmoStartGrain.Monthly, rules,
                Starts: [], Summary: null, MonthsWithoutStart: [], Start1DiffersFromSingleStartAnchor: false,
                fxBand.Low, fxBand.High, unscalableCount,
                FtmoRunSimulationResultDto.DefaultNotModelled, Disclosure);
        }

        var (starts, monthsWithoutStart) = FtmoStartEnumerator.Enumerate(projectedLow, sourceZone, berlinZone);

        var attribution = FtmoChallengeRace.AttributeOpenDays(projectedLow, sourceZone, berlinZone);

        var rows = new List<FtmoMultiStartRowDto>(starts.Count);
        for (var i = 0; i < starts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var start = starts[i];
            var lowSeries = FtmoStartEnumerator.SliceFromStart(projectedLow, start.SourceOpen);
            var highSeries = FtmoStartEnumerator.SliceFromStart(projectedHigh, start.SourceOpen);

            var lowChain = RunOneEnd(lowSeries, sourceZone, berlinZone, initialCapital, dailyPct, maxPct, attribution);
            var highChain = RunOneEnd(highSeries, sourceZone, berlinZone, initialCapital, dailyPct, maxPct, attribution);

            var (chosen, end, sensitive) = FtmoMultiStartChain.Merge3(lowChain, highChain);
            var outcome = FtmoMultiStartChain.Classify(chosen.Phase1, chosen.Phase2, chosen.Funded);
            var isCensored = CensoredOutcomes.Contains(outcome);

            var startDay = FtmoDayClock.Attribute(start.SourceOpen, sourceZone, berlinZone).BookkeepingDay;

            int? calendarDaysToBothTargets = chosen.Phase2.Outcome == FtmoPhaseOutcome.TargetReachedFirst
                ? FtmoDayClock.Attribute(chosen.Phase2.OutcomeSourceClose!.Value, sourceZone, berlinZone).BookkeepingDay.DayNumber - startDay.DayNumber
                : null;

            // A phase 2 with no trades left (phase 1 reached its target on the last replayed close) never
            // started, so the race reports it with null days; its runway is 0 — the same rule
            // FtmoFundedPhase applies to an empty funded phase (spec.md "A funded phase with no trades
            // left reports NoBreachByEndOfData with zero runway").
            int? runwayCalendarDays = outcome switch
            {
                FtmoChainOutcome.Phase1UndecidedAtEndOfData => chosen.Phase1.CalendarDaysElapsed,
                FtmoChainOutcome.Phase2UndecidedAtEndOfData when chosen.Phase2.StartSourceOpen is null => 0,
                FtmoChainOutcome.Phase2UndecidedAtEndOfData => chosen.Phase2.CalendarDaysElapsed,
                FtmoChainOutcome.FundedNoBreachAtEndOfData => chosen.Funded.CalendarDaysFromFundedStart,
                _ => null,
            };

            rows.Add(new FtmoMultiStartRowDto(
                i, start.SourceOpen, startDay, start.FtmoMonth, outcome, isCensored, runwayCalendarDays,
                calendarDaysToBothTargets, ToPhaseDto(chosen.Phase1), ToPhaseDto(chosen.Phase2),
                ToFundedDto(chosen.Funded), end, sensitive));
        }

        var summary = Summarize(rows);
        var start1DiffersFromAnchor = starts.Count > 0 && starts[0].SourceOpen != singleStartAnchor;

        return new FtmoMultiStartRunDto(
            runId, kind, segment, FtmoSimulationStatus.Evaluated, Refusal: null, RaceRefusal: null,
            profitTargetPct, FtmoStartGrain.Monthly, rules, rows, summary, monthsWithoutStart,
            start1DiffersFromAnchor, fxBand.Low, fxBand.High, unscalableCount,
            FtmoRunSimulationResultDto.DefaultNotModelled, Disclosure);
    }

    /// <summary>Phase 1 -> phase 2 -> funded, for one FX end's already-sliced series (design.md Data Flow).</summary>
    private static FtmoMultiStartChain.ChainResult3 RunOneEnd(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> series,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct,
        IReadOnlyDictionary<int, DateOnly> attribution)
    {
        var chain = FtmoChallengeRace.RunChain(
            series, sourceZone, berlinZone, capital, dailyPct, maxPct,
            precomputedPhase1Evaluation: null, precomputedOpenDayAttribution: attribution);

        var funded = FtmoFundedPhase.Run(
            series, chain.Phase1, chain.Phase2, sourceZone, berlinZone, capital, dailyPct, maxPct);

        return new FtmoMultiStartChain.ChainResult3(chain.Phase1, chain.Phase2, funded);
    }

    private static FtmoMultiStartSummaryDto? Summarize(IReadOnlyList<FtmoMultiStartRowDto> rows)
    {
        if (rows.Count == 0)
            return null;

        var startCount = rows.Count;

        var outcomes = Enum.GetValues<FtmoChainOutcome>()
            .Select(outcome =>
            {
                var count = rows.Count(r => r.Outcome == outcome);
                var isCensored = CensoredOutcomes.Contains(outcome);
                return new FtmoOutcomeCountDto(outcome, isCensored, count, (decimal)count / startCount);
            })
            .ToList();

        var fxRoundingSensitiveCount = rows.Count(r => r.FxRoundingSensitive);

        var daysToPhase1Target = OrderStats(rows
            .Where(r => r.Phase1.Outcome == FtmoPhaseOutcome.TargetReachedFirst)
            .Select(r => r.Phase1.CalendarDaysElapsed!.Value));
        var daysToPhase2Target = OrderStats(rows
            .Where(r => r.Phase2.Outcome == FtmoPhaseOutcome.TargetReachedFirst)
            .Select(r => r.Phase2.CalendarDaysElapsed!.Value));
        var daysToBothTargets = OrderStats(rows
            .Where(r => r.CalendarDaysToBothTargets is not null)
            .Select(r => r.CalendarDaysToBothTargets!.Value));
        var fundedDaysToBreachFromFundedStart = OrderStats(rows
            .Where(r => r.Funded.Outcome == FtmoFundedOutcome.BreachedFirst)
            .Select(r => r.Funded.CalendarDaysFromFundedStart!.Value));
        var fundedDaysToBreachFromChainStart = OrderStats(rows
            .Where(r => r.Funded.Outcome == FtmoFundedOutcome.BreachedFirst)
            .Select(r => r.Funded.CalendarDaysFromChainStart!.Value));
        var censoredRunway = OrderStats(rows
            .Where(r => r.IsCensored)
            .Select(r => r.RunwayCalendarDays!.Value));

        return new FtmoMultiStartSummaryDto(
            startCount, outcomes, fxRoundingSensitiveCount, daysToPhase1Target, daysToPhase2Target,
            daysToBothTargets, fundedDaysToBreachFromFundedStart, fundedDaysToBreachFromChainStart, censoredRunway);
    }

    /// <summary>The caller sorts before <see cref="FtmoOrderStatistics.Compute"/> — an unsorted input is a caller defect, not that method's concern.</summary>
    private static FtmoOrderStatisticsDto OrderStats(IEnumerable<int> values)
        => FtmoOrderStatistics.Compute([.. values.OrderBy(v => v)]);

    private static FtmoChallengePhaseDto ToPhaseDto(FtmoChallengeRace.PhaseResult phase) => new(
        phase.Outcome, phase.StartSourceOpen, phase.FirstTargetTouchSourceClose, phase.MinTradingDaysMetFtmoDay,
        phase.OutcomeSourceClose, phase.BreachLimit, phase.BreachPointClass,
        phase.CalendarDaysElapsed, phase.FtmoTradingDaysElapsed, FxBandEnd: null);

    private static FtmoFundedPhaseDto ToFundedDto(FtmoFundedPhase.FundedResult funded) => new(
        funded.Outcome, funded.StartSourceOpen, funded.OutcomeSourceClose, funded.BreachLimit, funded.BreachPointClass,
        funded.CalendarDaysFromFundedStart, funded.FtmoTradingDaysFromFundedStart,
        funded.CalendarDaysFromChainStart, funded.FtmoTradingDaysFromChainStart);

    private static FtmoMultiStartDto RefuseAll(
        Guid strategyId, IEnumerable<(Guid Id, BacktestRunKind Kind)> runs, FtmoSimulationRefusal reason)
        => new(strategyId, [.. runs.Select(r => Refused(r.Id, r.Kind, reason))]);

    private static FtmoMultiStartRunDto Refused(
        Guid runId, BacktestRunKind kind, FtmoSimulationRefusal reason, BacktestSegment segment = BacktestSegment.Unknown,
        FtmoChallengeRulesDto? rules = null, (decimal Low, decimal High)? fxBand = null)
        => new(
            runId, kind, segment, FtmoSimulationStatus.Refused, reason, RaceRefusal: null,
            StoredProfitTargetPct: null, FtmoStartGrain.Monthly,
            rules ?? new FtmoChallengeRulesDto(
                FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
                FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null),
            Starts: [], Summary: null, MonthsWithoutStart: [], Start1DiffersFromSingleStartAnchor: false,
            fxBand?.Low, fxBand?.High, UnscalableCount: 0,
            FtmoRunSimulationResultDto.DefaultNotModelled, Disclosure);
}
