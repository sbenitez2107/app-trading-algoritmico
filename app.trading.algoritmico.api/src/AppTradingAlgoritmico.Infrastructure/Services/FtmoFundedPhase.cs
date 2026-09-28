using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-multi-start PR1, task 1.3 (design.md Decision 3) — the funded phase that runs after phase 2
/// reaches its target: a fresh account at Initial Capital, same loss limits, no profit target and no
/// day minimum, on trades with <c>Open &gt;= T2 AND Close &gt; T2</c> — the same PR0-fixed handover
/// rule <see cref="FtmoChallengeRace.RunChain"/> uses between phase 1 and phase 2 (spec.md "Each Start
/// Runs Phase 1, Phase 2, Then A Funded Phase"). Evaluated by the UNEDITED
/// <see cref="FtmoBreachEvaluator"/>; first breach via <see cref="FtmoChallengeRace.FirstBreach"/>.
/// <c>internal static</c>, pure: no I/O.
/// </summary>
internal static class FtmoFundedPhase
{
    internal readonly record struct FundedResult(
        FtmoFundedOutcome Outcome,
        DateTime? StartSourceOpen,
        DateTime? OutcomeSourceClose,
        FtmoFirstBreachingLimit? BreachLimit,
        FtmoBreachPointClass? BreachPointClass,
        int? CalendarDaysFromFundedStart,
        int? FtmoTradingDaysFromFundedStart,
        int? CalendarDaysFromChainStart,
        int? FtmoTradingDaysFromChainStart)
    {
        internal static readonly FundedResult NotStarted =
            new(FtmoFundedOutcome.NotStarted, null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// Runs the funded phase for one chain. <paramref name="trades"/> is the same full series
    /// <see cref="FtmoChallengeRace.RunChain"/> was given (not pre-sliced); the funded subset is
    /// derived here from <paramref name="phase2"/>'s outcome close (T2), never leaking a straddling
    /// row (spec.md "Each Start Runs Phase 1, Phase 2, Then A Funded Phase"). Elapsed days are reported
    /// both from the funded phase's own anchor and from <paramref name="phase1"/>'s anchor (this
    /// start's own chain start) — spec.md "Funded Duration Is Reported From Both The Funded Start And
    /// The Chain Start".
    /// </summary>
    internal static FundedResult Run(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        FtmoChallengeRace.PhaseResult phase1,
        FtmoChallengeRace.PhaseResult phase2,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        if (phase2.Outcome != FtmoPhaseOutcome.TargetReachedFirst)
            return FundedResult.NotStarted;

        var t2 = phase2.OutcomeSourceClose!.Value;
        var fundedTrades = trades
            .Where(t => t.OpenSource >= t2 && t.CloseSource > t2)
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        var chainAnchorDay = FtmoDayClock.Attribute(phase1.StartSourceOpen!.Value, sourceZone, berlinZone).BookkeepingDay;
        var chainAnchor = new FtmoReplayCalendar.ReplayAnchor(phase1.StartSourceOpen.Value, chainAnchorDay);

        if (fundedTrades.Count == 0)
        {
            return FundedResult.NotStarted with
            {
                Outcome = FtmoFundedOutcome.NoBreachByEndOfData,
                CalendarDaysFromFundedStart = 0,
                FtmoTradingDaysFromFundedStart = 0,
                CalendarDaysFromChainStart = 0,
                FtmoTradingDaysFromChainStart = 0,
            };
        }

        var fundedAnchor = FtmoReplayCalendar.Build(fundedTrades, sourceZone, berlinZone);

        // Fresh account: capital reset to Initial Capital, same loss limits, no target, no day minimum.
        var evaluation = FtmoBreachEvaluator.Evaluate(fundedTrades, sourceZone, berlinZone, capital, dailyPct, maxPct);
        var firstBreach = FtmoChallengeRace.FirstBreach(evaluation);

        if (firstBreach is not null)
        {
            var breachPoint = firstBreach.Value.BreachPoint;

            var (fundedCalendarDays, fundedTradingDays) = FtmoReplayCalendar.ElapsedDays(
                fundedAnchor, breachPoint.FtmoDay, breachPoint.SourceTime, fundedTrades, sourceZone, berlinZone);
            var (chainCalendarDays, chainTradingDays) = FtmoReplayCalendar.ElapsedDays(
                chainAnchor, breachPoint.FtmoDay, breachPoint.SourceTime, trades, sourceZone, berlinZone);

            return new FundedResult(
                FtmoFundedOutcome.BreachedFirst,
                fundedAnchor.SourceOpen,
                breachPoint.SourceTime,
                firstBreach.Value.BreachLimit,
                breachPoint.Causes.Count == 0 ? FtmoBreachPointClass.Clean : FtmoBreachPointClass.Contingent,
                fundedCalendarDays,
                fundedTradingDays,
                chainCalendarDays,
                chainTradingDays);
        }

        var last = fundedTrades[^1];
        var lastDay = FtmoDayClock.Attribute(last.CloseSource, sourceZone, berlinZone).BookkeepingDay;
        var (lastFundedCalendarDays, lastFundedTradingDays) = FtmoReplayCalendar.ElapsedDays(
            fundedAnchor, lastDay, last.CloseSource, fundedTrades, sourceZone, berlinZone);
        var (lastChainCalendarDays, lastChainTradingDays) = FtmoReplayCalendar.ElapsedDays(
            chainAnchor, lastDay, last.CloseSource, trades, sourceZone, berlinZone);

        return new FundedResult(
            FtmoFundedOutcome.NoBreachByEndOfData,
            fundedAnchor.SourceOpen,
            null,
            null,
            null,
            lastFundedCalendarDays,
            lastFundedTradingDays,
            lastChainCalendarDays,
            lastChainTradingDays);
    }
}
