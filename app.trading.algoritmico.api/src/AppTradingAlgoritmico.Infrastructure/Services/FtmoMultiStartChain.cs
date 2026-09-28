using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-multi-start PR1, tasks 1.4 and 1.5 (design.md Decisions 5 and 6) — <see cref="Classify"/> maps
/// one start's phase 1, phase 2, and funded results to its whole-chain <see cref="FtmoChainOutcome"/>;
/// <see cref="Merge3"/> extends the shipped two-phase FX-band comparison
/// (<see cref="FtmoChallengeRace.MergeEnds"/>, unedited) to the funded phase. <c>internal static</c>,
/// pure: no I/O.
/// </summary>
internal static class FtmoMultiStartChain
{
    /// <summary>One start's whole three-phase chain, used only by <see cref="Merge3"/> (design.md File
    /// Changes table, "Chain3").</summary>
    internal readonly record struct ChainResult3(
        FtmoChallengeRace.PhaseResult Phase1,
        FtmoChallengeRace.PhaseResult Phase2,
        FtmoFundedPhase.FundedResult Funded);

    /// <summary>
    /// Maps phase 1, phase 2, and the funded phase to one of the six chain outcomes (spec.md "Six Chain
    /// Outcomes, Three Right-Censored And Labelled As Such"). The caller is responsible for the
    /// phase-to-phase consistency these three results assume: phase 2 only runs after phase 1's target
    /// (design.md Decision 1's chain), and the funded phase only runs after phase 2's target (design.md
    /// Decision 3) — a mismatched combination (e.g. phase 2 undecided but funded already resolved) is a
    /// caller defect, not a sixth outcome, so it throws rather than silently misclassifying.
    /// </summary>
    internal static FtmoChainOutcome Classify(
        FtmoChallengeRace.PhaseResult phase1,
        FtmoChallengeRace.PhaseResult phase2,
        FtmoFundedPhase.FundedResult funded)
    {
        if (phase1.Outcome == FtmoPhaseOutcome.BreachedFirst)
            return FtmoChainOutcome.Phase1Breached;

        if (phase1.Outcome == FtmoPhaseOutcome.NeitherByEndOfData)
            return FtmoChainOutcome.Phase1UndecidedAtEndOfData;

        if (phase1.Outcome != FtmoPhaseOutcome.TargetReachedFirst)
            throw new InvalidOperationException($"Unexpected phase 1 outcome: {phase1.Outcome}.");

        if (phase2.Outcome == FtmoPhaseOutcome.BreachedFirst)
            return FtmoChainOutcome.Phase2Breached;

        if (phase2.Outcome == FtmoPhaseOutcome.NeitherByEndOfData)
            return FtmoChainOutcome.Phase2UndecidedAtEndOfData;

        if (phase2.Outcome != FtmoPhaseOutcome.TargetReachedFirst)
            throw new InvalidOperationException($"Unexpected phase 2 outcome: {phase2.Outcome}.");

        return funded.Outcome switch
        {
            FtmoFundedOutcome.BreachedFirst => FtmoChainOutcome.FundedBreached,
            FtmoFundedOutcome.NoBreachByEndOfData => FtmoChainOutcome.FundedNoBreachAtEndOfData,
            _ => throw new InvalidOperationException($"Unexpected funded outcome: {funded.Outcome}."),
        };
    }

    /// <summary>
    /// Three-phase whole-chain rank, least to most favourable to reaching the target (design.md
    /// Decision 5): <c>P1 Breached 0 &lt; P1 Neither 1 &lt; P2 Breached 2 &lt; P2 Neither 3 &lt;
    /// Funded Breached 4 &lt; Funded No-Breach 5</c>.
    /// </summary>
    private static int Rank3(ChainResult3 chain) => chain.Phase1.Outcome switch
    {
        FtmoPhaseOutcome.BreachedFirst => 0,
        FtmoPhaseOutcome.NeitherByEndOfData => 1,
        FtmoPhaseOutcome.TargetReachedFirst => chain.Phase2.Outcome switch
        {
            FtmoPhaseOutcome.BreachedFirst => 2,
            FtmoPhaseOutcome.TargetReachedFirst => chain.Funded.Outcome switch
            {
                FtmoFundedOutcome.BreachedFirst => 4,
                _ => 5,
            },
            _ => 3,
        },
        _ => 1,
    };

    /// <summary>
    /// Merges the two FX-band ends' three-phase chains (design.md Decision 5): reports the lower-ranked
    /// (less favourable) chain; on a rank tie, ranks 0/2/4 (breach ranks) use the earlier close (less
    /// favourable), ranks 1/3/5 (undecided/no-breach ranks) report the <c>fxLow</c> end, tagged; on an
    /// identical row, <see cref="FtmoFxBandEnd.BothEnds"/>. This is NEW code — the shipped two-phase
    /// <see cref="FtmoChallengeRace.MergeEnds"/> is not edited (spec.md "The FX Whole-Chain Rule Extends
    /// To Three Phases"). A degenerate band (<c>fxLow == fxHigh</c>) is simply <c>Merge3(x, x)</c>,
    /// which the identical-row branch below already resolves to <see cref="FtmoFxBandEnd.BothEnds"/>,
    /// not sensitive.
    /// </summary>
    internal static (ChainResult3 Chain, FtmoFxBandEnd End, bool RoundingSensitive) Merge3(
        ChainResult3 low, ChainResult3 high)
    {
        var identicalRow =
            low.Phase1.Outcome == high.Phase1.Outcome
            && low.Phase1.OutcomeSourceClose == high.Phase1.OutcomeSourceClose
            && low.Phase2.Outcome == high.Phase2.Outcome
            && low.Phase2.OutcomeSourceClose == high.Phase2.OutcomeSourceClose
            && low.Funded.Outcome == high.Funded.Outcome
            && low.Funded.OutcomeSourceClose == high.Funded.OutcomeSourceClose;
        var roundingSensitive = !identicalRow;

        var rankLow = Rank3(low);
        var rankHigh = Rank3(high);

        if (rankLow != rankHigh)
            return rankLow < rankHigh
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);

        // Same rank: identical row -> BothEnds.
        if (identicalRow)
            return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

        if (rankLow is 0 or 2 or 4)
        {
            var (lowClose, highClose) = rankLow switch
            {
                0 => (low.Phase1.OutcomeSourceClose, high.Phase1.OutcomeSourceClose),
                2 => (low.Phase2.OutcomeSourceClose, high.Phase2.OutcomeSourceClose),
                _ => (low.Funded.OutcomeSourceClose, high.Funded.OutcomeSourceClose),
            };

            if (lowClose == highClose)
                return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

            var lowIsPreferred = lowClose < highClose;
            return lowIsPreferred
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);
        }

        // Ranks 1, 3, 5: no explicit spec-named timing tie-break (same pattern as the shipped two-phase
        // MergeEnds' ranks 1 and 3) — report the fxLow end, tagged sensitive.
        return (low, FtmoFxBandEnd.FxLow, roundingSensitive);
    }
}
