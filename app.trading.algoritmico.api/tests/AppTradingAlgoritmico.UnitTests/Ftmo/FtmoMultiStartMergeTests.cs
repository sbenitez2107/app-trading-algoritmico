using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoMultiStartChain;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.5 (design.md Decision 5) — <see cref="FtmoMultiStartChain.Merge3"/>:
/// the whole-chain FX-band comparison extended to the funded phase, rank order
/// <c>P1B 0 &lt; P1N 1 &lt; P2B 2 &lt; P2N 3 &lt; FB 4 &lt; FN 5</c>. This is NEW code — the shipped
/// two-phase <see cref="FtmoChallengeRace.MergeEnds"/> is not edited.
/// </summary>
public class FtmoMultiStartMergeTests
{
    private static readonly DateTime CloseA = new(2026, 1, 10, 9, 0, 0);
    private static readonly DateTime CloseB = new(2026, 1, 12, 9, 0, 0);

    private static FtmoChallengeRace.PhaseResult Phase(FtmoPhaseOutcome outcome, DateTime? close = null, int? calendarDays = null) =>
        new(outcome, StartSourceOpen: null, FirstTargetTouchSourceClose: null, MinTradingDaysMetFtmoDay: null,
            OutcomeSourceClose: close, BreachLimit: null, BreachPointClass: null,
            CalendarDaysElapsed: calendarDays ?? (close is null ? 5 : null),
            FtmoTradingDaysElapsed: close is null ? 4 : null);

    private static FtmoFundedPhase.FundedResult Funded(
        FtmoFundedOutcome outcome, DateTime? close = null, int? calendarDays = null) =>
        new(outcome, StartSourceOpen: null, OutcomeSourceClose: close, BreachLimit: null, BreachPointClass: null,
            CalendarDaysFromFundedStart: calendarDays ?? (close is null ? 5 : null),
            FtmoTradingDaysFromFundedStart: null, CalendarDaysFromChainStart: null, FtmoTradingDaysFromChainStart: null);

    private static readonly FtmoChallengeRace.PhaseResult NotStartedPhase = FtmoChallengeRace.PhaseResult.NotStarted;
    private static readonly FtmoFundedPhase.FundedResult NotStartedFunded = FtmoFundedPhase.FundedResult.NotStarted;

    [Fact]
    public void AnEarlierFundedBreach_IsLessFavourableBetweenFxEnds()
    {
        var earlierFundedBreach = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Funded(FtmoFundedOutcome.BreachedFirst, CloseA));
        var laterFundedBreach = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Funded(FtmoFundedOutcome.BreachedFirst, CloseB));

        var (chain, _, sensitive) = Merge3(earlierFundedBreach, laterFundedBreach);

        chain.Funded.OutcomeSourceClose.Should().Be(CloseA);
        sensitive.Should().BeTrue();
    }

    [Fact]
    public void ADegenerateBand_EvaluatesOneEndOnly()
    {
        var chain = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Funded(FtmoFundedOutcome.NoBreachByEndOfData));

        var (result, end, sensitive) = Merge3(chain, chain);

        result.Should().Be(chain);
        end.Should().Be(FtmoFxBandEnd.BothEnds);
        sensitive.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)] // Phase1Breached
    [InlineData(2)] // Phase2Breached
    [InlineData(4)] // FundedBreached
    public void BreachRanks_TheEarlierCloseIsLessFavourableAndIsReported(int rank)
    {
        var (low, high) = BuildPair(rank, CloseA, CloseB);

        var (chain, _, sensitive) = Merge3(low, high);

        chain.Should().Be(low); // low carries CloseA, the earlier of the two.
        sensitive.Should().BeTrue();
    }

    [Theory]
    [InlineData(3)] // Phase2UndecidedAtEndOfData: phase 1's close differs between ends, so the rows differ.
    [InlineData(5)] // FundedNoBreachAtEndOfData: same.
    public void UndecidedRanks_ReportTheFxLowEndTagged(int rank)
    {
        var (low, high) = BuildPair(rank, CloseA, CloseB);

        var (chain, end, sensitive) = Merge3(low, high);

        chain.Should().Be(low);
        end.Should().Be(FtmoFxBandEnd.FxLow);
        sensitive.Should().BeTrue();
    }

    /// <summary>
    /// Rank 1 (<c>Phase1UndecidedAtEndOfData</c>) never carries a deciding close in production (only
    /// <c>TargetReachedFirst</c>/<c>BreachedFirst</c> phases do), so two different rank-1 chains are
    /// indistinguishable under the identical-row check — the same limitation the shipped two-phase
    /// <see cref="FtmoChallengeRace.MergeEnds"/> has for its own rank 1. They resolve as
    /// <see cref="FtmoFxBandEnd.BothEnds"/>, not sensitive, even though their calendar-day counts differ.
    /// </summary>
    [Fact]
    public void RankOne_TwoUndecidedPhase1ChainsAreIndistinguishable_ResolveAsBothEnds()
    {
        var (low, high) = BuildPair(1, CloseA, CloseB);

        var (chain, end, sensitive) = Merge3(low, high);

        chain.Should().Be(low);
        end.Should().Be(FtmoFxBandEnd.BothEnds);
        sensitive.Should().BeFalse();
    }

    [Fact]
    public void RankOrder_P1BreachedIsLessFavourableThanP1Undecided()
    {
        var (p1Breached, _) = BuildPair(0, CloseA, CloseA);
        var (p1Undecided, _) = BuildPair(1, CloseA, CloseA);

        var (chain, _, _) = Merge3(p1Breached, p1Undecided);

        chain.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
    }

    [Fact]
    public void RankOrder_P2UndecidedIsMoreFavourableThanP2Breached_ButLessThanFundedBreached()
    {
        var (p2Breached, _) = BuildPair(2, CloseA, CloseA);
        var (p2Undecided, _) = BuildPair(3, CloseA, CloseA);
        var (fundedBreached, _) = BuildPair(4, CloseA, CloseA);

        Merge3(p2Breached, p2Undecided).Chain.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        Merge3(p2Undecided, fundedBreached).Chain.Funded.Outcome.Should().Be(FtmoFundedOutcome.NotStarted);
    }

    /// <summary>
    /// design.md Decision 5's pin, corrected (spec.md's own correction): the three-phase merge's phase-1
    /// and phase-2 outputs equal the shipped <see cref="FtmoChallengeRace.MergeEnds"/> ONLY when the
    /// less favourable end's chain resolves at or before <c>Phase2Undecided</c> (rank &lt;= 3).
    /// Falsification-bearing (hard rule 6): one fixture in each half proves the assertion direction
    /// actually distinguishes them.
    /// </summary>
    [Fact]
    public void ScopedMergeEndsPin_EqualsMergeEndsOnlyWhenNeitherEndReachesFunded()
    {
        // Half 1: both ends resolve at rank <= 3 (phase 2 undecided) -> equality holds.
        var lowRank3 = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 5),
            NotStartedFunded);
        var highRank3 = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseB),
            Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 20),
            NotStartedFunded);

        var threePhase = Merge3(lowRank3, highRank3);
        var twoPhase = FtmoChallengeRace.MergeEnds(
            new FtmoChallengeRace.ChainResult(lowRank3.Phase1, lowRank3.Phase2),
            new FtmoChallengeRace.ChainResult(highRank3.Phase1, highRank3.Phase2));

        threePhase.Chain.Phase1.Should().Be(twoPhase.Chain.Phase1);
        threePhase.Chain.Phase2.Should().Be(twoPhase.Chain.Phase2);

        // Half 2: both ends reach the funded phase -> the funded outcome decides, equality does NOT hold.
        var lowFunded = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseA),
            Funded(FtmoFundedOutcome.BreachedFirst, CloseA)); // earlier funded breach -> reported by Merge3
        var highFunded = new ChainResult3(
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseB),
            Phase(FtmoPhaseOutcome.TargetReachedFirst, CloseB),
            Funded(FtmoFundedOutcome.BreachedFirst, CloseB)); // later funded breach

        var threePhaseFunded = Merge3(lowFunded, highFunded);
        // Two-phase MergeEnds compares phase-2 closes only; rank 4 there prefers the LATER close, so it
        // reports highFunded's (Close B) phase 1/2 -- while Merge3 reports lowFunded's (earlier funded
        // breach, Close A) phase 1/2. The two disagree by construction.
        var twoPhaseFunded = FtmoChallengeRace.MergeEnds(
            new FtmoChallengeRace.ChainResult(lowFunded.Phase1, lowFunded.Phase2),
            new FtmoChallengeRace.ChainResult(highFunded.Phase1, highFunded.Phase2));

        threePhaseFunded.Chain.Phase1.OutcomeSourceClose.Should().NotBe(twoPhaseFunded.Chain.Phase1.OutcomeSourceClose);
    }

    private static (ChainResult3 Low, ChainResult3 High) BuildPair(int rank, DateTime lowClose, DateTime highClose) =>
        rank switch
        {
            0 => (
                new ChainResult3(Phase(FtmoPhaseOutcome.BreachedFirst, lowClose), NotStartedPhase, NotStartedFunded),
                new ChainResult3(Phase(FtmoPhaseOutcome.BreachedFirst, highClose), NotStartedPhase, NotStartedFunded)),
            1 => (
                new ChainResult3(Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 5), NotStartedPhase, NotStartedFunded),
                new ChainResult3(Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 20), NotStartedPhase, NotStartedFunded)),
            2 => (
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose), Phase(FtmoPhaseOutcome.BreachedFirst, lowClose), NotStartedFunded),
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose), Phase(FtmoPhaseOutcome.BreachedFirst, highClose), NotStartedFunded)),
            3 => (
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose),
                    Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 5), NotStartedFunded),
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose),
                    Phase(FtmoPhaseOutcome.NeitherByEndOfData, calendarDays: 20), NotStartedFunded)),
            4 => (
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose), Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose),
                    Funded(FtmoFundedOutcome.BreachedFirst, lowClose)),
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose), Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose),
                    Funded(FtmoFundedOutcome.BreachedFirst, highClose))),
            _ => (
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose), Phase(FtmoPhaseOutcome.TargetReachedFirst, lowClose),
                    Funded(FtmoFundedOutcome.NoBreachByEndOfData, calendarDays: 5)),
                new ChainResult3(
                    Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose), Phase(FtmoPhaseOutcome.TargetReachedFirst, highClose),
                    Funded(FtmoFundedOutcome.NoBreachByEndOfData, calendarDays: 20))),
        };

    /// <summary>Falsification (hard rule 6): a Merge3 that always returns <paramref name="low"/> would
    /// pass the breach-rank facts vacuously but must go RED on the undecided-rank facts, which assert
    /// <c>FxLow</c> specifically (already true for <c>low</c> in this fixture layout) AND
    /// <c>sensitive == true</c> only when the two differ -- broken by asserting the reverse tag below.</summary>
    [Fact]
    public void Falsification_AConstantEndTag_WouldFailTheUndecidedRankFacts()
    {
        var (low, high) = BuildPair(3, CloseA, CloseB);

        var (_, end, _) = Merge3(low, high);

        end.Should().Be(FtmoFxBandEnd.FxLow);
        end.Should().NotBe(FtmoFxBandEnd.FxHigh); // A hypothetical "always FxHigh" implementation fails here.
    }
}
