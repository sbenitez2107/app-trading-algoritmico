using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoChallengeRace;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-challenge-race, Phase 2.8 — <see cref="FtmoChallengeRace.MergeEnds"/>: whole-chain FX-band
/// ranking (design.md Decision 6). Builds <see cref="ChainResult"/> fixtures directly rather than
/// through the scanner, since the merge itself is what's under test.
/// </summary>
public class FtmoChallengeRaceFxMergeTests
{
    private static readonly DateTime Close1 = new(2026, 1, 10, 9, 0, 0);
    private static readonly DateTime Close2 = new(2026, 1, 12, 9, 0, 0);

    private static PhaseResult Phase(FtmoPhaseOutcome outcome, DateTime? close = null, int? calendarDays = null) =>
        new(outcome, StartSourceOpen: null, FirstTargetTouchSourceClose: null, MinTradingDaysMetFtmoDay: null,
            OutcomeSourceClose: close, BreachLimit: null, BreachPointClass: null,
            CalendarDaysElapsed: calendarDays ?? (close is null ? 5 : null),
            FtmoTradingDaysElapsed: close is null ? 4 : null);

    [Fact]
    public void DisagreeingFxEnds_ReportTheLessFavourableOutcome()
    {
        var low = new ChainResult(Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), PhaseResult.NotStarted);
        var high = new ChainResult(Phase(FtmoPhaseOutcome.BreachedFirst, Close1), PhaseResult.NotStarted);

        var (chain, end, sensitive) = MergeEnds(low, high);

        chain.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        end.Should().Be(FtmoFxBandEnd.FxHigh);
        sensitive.Should().BeTrue();
    }

    [Fact]
    public void AgreeingFxEnds_NeedNoRoundingSensitivityTag()
    {
        var low = new ChainResult(Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), PhaseResult.NotStarted);
        var high = new ChainResult(Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), PhaseResult.NotStarted);

        var (chain, end, sensitive) = MergeEnds(low, high);

        chain.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        sensitive.Should().BeFalse();
    }

    [Fact]
    public void ASameCurrencySymbolNeedsNoFxBandComparison_IdenticalEndsReportBothEnds()
    {
        var low = new ChainResult(Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), PhaseResult.NotStarted);
        var high = new ChainResult(Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), PhaseResult.NotStarted);

        var (_, end, sensitive) = MergeEnds(low, high);

        end.Should().Be(FtmoFxBandEnd.BothEnds);
        sensitive.Should().BeFalse();
    }

    [Fact]
    public void APhaseTwoOutcomeTieBetweenEnds_IsBrokenByTimingNotOutcomeAlone()
    {
        var low = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Phase(FtmoPhaseOutcome.BreachedFirst, Close1));
        var high = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Phase(FtmoPhaseOutcome.BreachedFirst, Close2)); // later breach on the high end

        var (chain, end, sensitive) = MergeEnds(low, high);

        chain.Phase2.OutcomeSourceClose.Should().Be(Close1);
        end.Should().Be(FtmoFxBandEnd.FxLow);
        sensitive.Should().BeTrue();
    }

    [Fact]
    public void IdenticalOutcomeAndDecidingClose_ReportsBothEnds()
    {
        var low = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close2));
        var high = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close2));

        var (chain, end, sensitive) = MergeEnds(low, high);

        end.Should().Be(FtmoFxBandEnd.BothEnds);
        sensitive.Should().BeFalse();
        chain.Should().Be(low);
    }

    /// <summary>
    /// Orchestrator decision (point 3, resolving the flagged fallback): a rank tie NOT covered by an
    /// explicit breach/target-timing comparison (here: rank 3, <c>P1 Target/P2 Neither</c>, with
    /// phase 1 deciding at a DIFFERENT close on each end so the rows are not identical) reports the
    /// <c>fxLow</c> end's values — never a bespoke "later elapsed time" comparison — tagged
    /// <c>FxRoundingSensitive</c> because the rows differ.
    /// </summary>
    [Fact]
    public void ATieBetweenTwoUndecidedChains_ReportsTheFxLowEnd_NotTheLaterElapsedTime()
    {
        var low = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 5)); // fewer elapsed days
        var high = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close2), // different phase-1 close -> rows differ
            Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 20)); // more elapsed days

        var (chain, end, sensitive) = MergeEnds(low, high);

        // Must report fxLow regardless of which end has the "later" elapsed time.
        chain.Should().Be(low);
        end.Should().Be(FtmoFxBandEnd.FxLow);
        sensitive.Should().BeTrue();
    }

    /// <summary>
    /// Falsification (hard rule 5): merging each phase independently (mixing a phase 2 from one end
    /// with a phase 1 from the other) can produce an impossible chain. Demonstrated by comparing
    /// per-phase picks against the whole-chain pick on the phase-2-tie fixture above.
    /// </summary>
    [Fact]
    public void MixingPhasesIndependently_WouldProduceAnImpossibleChain()
    {
        var low = new ChainResult(
            Phase(FtmoPhaseOutcome.BreachedFirst, Close1), // phase 1 breached on low
            PhaseResult.NotStarted);
        var high = new ChainResult(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close2), // phase 1 reached target on high
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close2));

        var (chain, _, _) = MergeEnds(low, high);

        // Whole-chain comparison: low's chain (P1 Breached, rank 0) beats high's (rank 4) -> low's
        // ENTIRE chain is reported, including its NotStarted phase 2 -- never high's phase-2 result
        // spliced onto low's phase-1 breach (which would be the impossible, independently-merged chain).
        chain.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        chain.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.NotStarted);
    }
}
