using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-challenge-race — composes the unchanged <see cref="FtmoBreachEvaluator"/> with a target
/// scanner to report, per phase, whichever of "target reached" or "first breach" happens first
/// (design.md Decision 1: Compose). <c>internal static</c>, pure: no I/O.
/// <para>
/// A separate, truncating loop over the same trade series as the shipped breach replay (spec.md
/// "The Race Leaves The Shipped Breach Result Byte-Identical"): <see cref="FtmoBreachEvaluator.Evaluate"/>
/// is never edited and never truncated by this class.
/// </para>
/// </summary>
internal static class FtmoChallengeRace
{
    /// <summary>Disclosure (design.md "Disclosure"). Never affirms survival — spec.md's no-pass-wording requirement.</summary>
    internal const string Disclosure =
        "This race replays closed trades only and is not a prediction. A profit target reached first is "
        + "an optimistic reading in two ways: swap is not modelled, so the target can arrive earlier than "
        + "on a live account, and a closed-trade replay cannot see intraday equity dips, so breaches are "
        + "understated. A breach reached first is a strong result, because both biases push against it. "
        + "Neither by end of data means the replayed history ended before either event occurred.";

    internal readonly record struct PhaseResult(
        FtmoPhaseOutcome Outcome,
        DateTime? StartSourceOpen,
        DateTime? FirstTargetTouchSourceClose,
        DateOnly? MinTradingDaysMetFtmoDay,
        DateTime? OutcomeSourceClose,
        FtmoFirstBreachingLimit? BreachLimit,
        FtmoBreachPointClass? BreachPointClass,
        int? CalendarDaysElapsed,
        int? FtmoTradingDaysElapsed)
    {
        internal static readonly PhaseResult NotStarted =
            new(FtmoPhaseOutcome.NotStarted, null, null, null, null, null, null, null, null);
    }

    internal readonly record struct ChainResult(PhaseResult Phase1, PhaseResult Phase2);

    /// <summary>
    /// Runs one phase's scanner (target, close groups, flat-book check) plus the unchanged evaluator
    /// (breach) over <paramref name="phaseTrades"/>, and combines them per design.md Decision 3
    /// (breach wins on a tie).
    /// </summary>
    internal static PhaseResult RunPhase(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> phaseTrades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct,
        decimal targetPct)
    {
        ArgumentNullException.ThrowIfNull(phaseTrades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        if (phaseTrades.Count == 0)
            return PhaseResult.NotStarted with { Outcome = FtmoPhaseOutcome.NeitherByEndOfData };

        var anchor = FtmoReplayCalendar.Build(phaseTrades, sourceZone, berlinZone);
        var startOpen = anchor.SourceOpen;

        var ordered = phaseTrades
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        var targetLevel = capital * (1m + targetPct);

        DateTime? firstTouch = null;
        DateOnly? minDaysMetDay = null;
        DateTime? decidedClose = null;
        DateOnly? decidedDay = null;
        int? decidedCalendarDays = null;
        int? decidedTradingDays = null;

        var runningBalance = capital;
        var index = 0;
        while (index < ordered.Count)
        {
            var groupClose = ordered[index].CloseSource;
            while (index < ordered.Count && ordered[index].CloseSource == groupClose)
            {
                if (ordered[index].Net is { } net)
                    runningBalance += net;
                index++;
            }

            if (firstTouch is null && runningBalance >= targetLevel)
                firstTouch = groupClose;

            var groupDay = FtmoDayClock.Attribute(groupClose, sourceZone, berlinZone).BookkeepingDay;
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, groupDay, groupClose, phaseTrades, sourceZone, berlinZone);

            if (minDaysMetDay is null && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase)
                minDaysMetDay = groupDay;

            if (decidedClose is null
                && runningBalance >= targetLevel
                && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase
                && !FtmoOpenPositionSweep.OpenAt(phaseTrades, groupClose))
            {
                decidedClose = groupClose;
                decidedDay = groupDay;
                decidedCalendarDays = calendarDays;
                decidedTradingDays = tradingDays;
                break;
            }
        }

        var evaluation = FtmoBreachEvaluator.Evaluate(phaseTrades, sourceZone, berlinZone, capital, dailyPct, maxPct);

        FtmoBreachEvaluator.BreachPoint? breachPoint = null;
        FtmoFirstBreachingLimit? breachLimit = null;
        if (evaluation.Daily.FirstBreach is not null || evaluation.Max.FirstBreach is not null)
        {
            var daily = evaluation.Daily.FirstBreach;
            var max = evaluation.Max.FirstBreach;

            if (daily is null)
            {
                breachPoint = max;
                breachLimit = FtmoFirstBreachingLimit.Max;
            }
            else if (max is null)
            {
                breachPoint = daily;
                breachLimit = FtmoFirstBreachingLimit.Daily;
            }
            else if (daily.Value.RowIndex == max.Value.RowIndex)
            {
                breachPoint = daily;
                breachLimit = FtmoFirstBreachingLimit.BothSameClose;
            }
            else
            {
                var dailyIsEarlier = (daily.Value.SourceTime, daily.Value.RowIndex)
                    .CompareTo((max.Value.SourceTime, max.Value.RowIndex)) < 0;
                breachPoint = dailyIsEarlier ? daily : max;
                breachLimit = dailyIsEarlier ? FtmoFirstBreachingLimit.Daily : FtmoFirstBreachingLimit.Max;
            }
        }

        // Breach wins on a tie (design.md Decision 3): breach at or before the decided close.
        if (breachPoint is not null && (decidedClose is null || breachPoint.Value.SourceTime <= decidedClose.Value))
        {
            var (calendarDays, tradingDays) = FtmoReplayCalendar.ElapsedDays(
                anchor, breachPoint.Value.FtmoDay, breachPoint.Value.SourceTime, phaseTrades, sourceZone, berlinZone);

            return new PhaseResult(
                FtmoPhaseOutcome.BreachedFirst, startOpen, firstTouch, minDaysMetDay,
                breachPoint.Value.SourceTime, breachLimit,
                breachPoint.Value.Causes.Count == 0 ? FtmoBreachPointClass.Clean : FtmoBreachPointClass.Contingent,
                calendarDays, tradingDays);
        }

        if (decidedClose is not null)
        {
            return new PhaseResult(
                FtmoPhaseOutcome.TargetReachedFirst, startOpen, firstTouch, minDaysMetDay,
                decidedClose, null, null, decidedCalendarDays, decidedTradingDays);
        }

        var last = ordered[^1];
        var lastDay = FtmoDayClock.Attribute(last.CloseSource, sourceZone, berlinZone).BookkeepingDay;
        var (lastCalendarDays, lastTradingDays) = FtmoReplayCalendar.ElapsedDays(
            anchor, lastDay, last.CloseSource, phaseTrades, sourceZone, berlinZone);

        return new PhaseResult(
            FtmoPhaseOutcome.NeitherByEndOfData, startOpen, firstTouch, minDaysMetDay,
            null, null, null, lastCalendarDays, lastTradingDays);
    }

    /// <summary>
    /// Runs the two-phase chain: phase 1 over the whole series, phase 2 (design.md Decision 1/2) over
    /// the post-handover subset (<c>Close &gt; T</c>, every row with <c>Open &gt;= T</c> by
    /// construction) with capital reset to Initial Capital, only when phase 1 reaches its target.
    /// </summary>
    internal static ChainResult RunChain(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct)
    {
        var phase1 = RunPhase(
            trades, sourceZone, berlinZone, capital, dailyPct, maxPct, FtmoChallengeRules.Phase1TargetPct);

        if (phase1.Outcome != FtmoPhaseOutcome.TargetReachedFirst)
            return new ChainResult(phase1, PhaseResult.NotStarted);

        var phase2Trades = trades
            .Where(t => t.CloseSource > phase1.OutcomeSourceClose!.Value)
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        if (phase2Trades.Count == 0)
        {
            return new ChainResult(
                phase1, PhaseResult.NotStarted with { Outcome = FtmoPhaseOutcome.NeitherByEndOfData });
        }

        var phase2 = RunPhase(
            phase2Trades, sourceZone, berlinZone, capital, dailyPct, maxPct, FtmoChallengeRules.Phase2TargetPct);

        return new ChainResult(phase1, phase2);
    }

    /// <summary>
    /// Whole-chain rank, least to most favourable to reaching the target (design.md Decision 6):
    /// <c>P1 Breached &lt; P1 Neither &lt; (P1 Target, P2 Breached) &lt; (P1 Target, P2 Neither) &lt;
    /// (P1 Target, P2 Target)</c>.
    /// </summary>
    private static int Rank(ChainResult chain) => chain.Phase1.Outcome switch
    {
        FtmoPhaseOutcome.BreachedFirst => 0,
        FtmoPhaseOutcome.NeitherByEndOfData => 1,
        FtmoPhaseOutcome.TargetReachedFirst => chain.Phase2.Outcome switch
        {
            FtmoPhaseOutcome.BreachedFirst => 2,
            FtmoPhaseOutcome.TargetReachedFirst => 4,
            _ => 3,
        },
        _ => 1,
    };

    /// <summary>
    /// Merges the two FX-band ends' whole chains (design.md Decision 6): reports the lower-ranked
    /// (less favourable) chain; on a rank tie, the less favourable timing; on an identical row,
    /// <see cref="FtmoFxBandEnd.BothEnds"/> with the <c>fxLow</c> values.
    /// </summary>
    internal static (ChainResult Chain, FtmoFxBandEnd End, bool RoundingSensitive) MergeEnds(
        ChainResult low, ChainResult high)
    {
        // "Outcome pairs differ" (spec.md's FX-band requirement) is read together with scenarios
        // 2.8.4/2.8.5: an identical ROW (same outcome AND same deciding close for every phase) is the
        // only case that needs no tag — a timing-only disagreement at the same rank still counts as
        // the two ends disagreeing on the outcome pair's own evidentiary weight.
        var identicalRow =
            low.Phase1.Outcome == high.Phase1.Outcome
            && low.Phase1.OutcomeSourceClose == high.Phase1.OutcomeSourceClose
            && low.Phase2.Outcome == high.Phase2.Outcome
            && low.Phase2.OutcomeSourceClose == high.Phase2.OutcomeSourceClose;
        var roundingSensitive = !identicalRow;

        var rankLow = Rank(low);
        var rankHigh = Rank(high);

        if (rankLow != rankHigh)
            return rankLow < rankHigh
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);

        // Same rank: identical row -> BothEnds.
        if (identicalRow)
            return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

        // Ranks 0 (P1 Breached), 2 (P2 Breached) and 4 (P2 Target) have an explicit, spec-named timing
        // tie-break (earlier breach / later target). Ranks 1 and 3 (a tie between two undecided
        // chains — both P1 Neither, or both P1 Target/P2 Neither at different phase-1 closes) have no
        // such rule (orchestrator decision, point 3): report the fxLow end, tagged sensitive, since the
        // rows are already known not to be identical at this point.
        if (rankLow is 0 or 2 or 4)
        {
            var preferEarlier = rankLow != 4;
            var (lowClose, highClose) = rankLow == 0
                ? (low.Phase1.OutcomeSourceClose, high.Phase1.OutcomeSourceClose)
                : (low.Phase2.OutcomeSourceClose, high.Phase2.OutcomeSourceClose);

            if (lowClose == highClose)
                return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

            var lowIsPreferred = preferEarlier ? lowClose < highClose : lowClose > highClose;
            return lowIsPreferred
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);
        }

        return (low, FtmoFxBandEnd.FxLow, roundingSensitive);
    }

    /// <summary>
    /// Top-level entry: race-only refusal (<see cref="FtmoChallengeRaceRefusal.ProfitTargetMismatch"/>),
    /// then the FX-band whole-chain race, mapped to <see cref="FtmoChallengeRaceDto"/>. The caller is
    /// responsible for the whole-run refusal path (hard rule 7) — this method assumes a TwoStep,
    /// Evaluated run.
    /// </summary>
    internal static FtmoChallengeRaceDto Evaluate(
        decimal? profitTargetPct,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> lowProjected,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> highProjected,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct)
    {
        var rules = new FtmoChallengeRulesDto(
            FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
            FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null);

        if (profitTargetPct is not null && profitTargetPct.Value != FtmoChallengeRules.Phase1TargetPct)
        {
            return new FtmoChallengeRaceDto(
                FtmoChallengeRaceRefusal.ProfitTargetMismatch, profitTargetPct, rules, null, null, false, Disclosure);
        }

        var low = RunChain(lowProjected, sourceZone, berlinZone, capital, dailyPct, maxPct);
        var high = RunChain(highProjected, sourceZone, berlinZone, capital, dailyPct, maxPct);
        var (chain, end, roundingSensitive) = MergeEnds(low, high);

        var phase1Dto = ToPhaseDto(chain.Phase1, end);
        var phase2Dto = ToPhaseDto(chain.Phase2, chain.Phase2.Outcome == FtmoPhaseOutcome.NotStarted ? null : end);

        return new FtmoChallengeRaceDto(
            null, profitTargetPct, rules, phase1Dto, phase2Dto, roundingSensitive, Disclosure);
    }

    private static FtmoChallengePhaseDto ToPhaseDto(PhaseResult phase, FtmoFxBandEnd? end) => new(
        phase.Outcome, phase.StartSourceOpen, phase.FirstTargetTouchSourceClose, phase.MinTradingDaysMetFtmoDay,
        phase.OutcomeSourceClose, phase.BreachLimit, phase.BreachPointClass,
        phase.CalendarDaysElapsed, phase.FtmoTradingDaysElapsed, end);
}
