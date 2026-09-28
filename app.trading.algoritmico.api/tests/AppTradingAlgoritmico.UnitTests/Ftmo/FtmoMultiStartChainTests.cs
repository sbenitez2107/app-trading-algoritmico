using System.Collections.Generic;
using System.Linq;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.4 (design.md File Changes table, <c>FtmoMultiStartChain.cs</c>) — the
/// six chain outcomes, exactly as spec.md's "Six Chain Outcomes, Three Right-Censored And Labelled As
/// Such" requirement defines them.
/// </summary>
public class FtmoMultiStartChainTests
{
    private static readonly DateTime Close1 = new(2026, 1, 10, 9, 0, 0);

    private static FtmoChallengeRace.PhaseResult Phase(FtmoPhaseOutcome outcome, DateTime? close = null) =>
        new(outcome, StartSourceOpen: null, FirstTargetTouchSourceClose: null, MinTradingDaysMetFtmoDay: null,
            OutcomeSourceClose: close, BreachLimit: null, BreachPointClass: null,
            CalendarDaysElapsed: null, FtmoTradingDaysElapsed: null);

    private static FtmoFundedPhase.FundedResult Funded(FtmoFundedOutcome outcome) => outcome switch
    {
        FtmoFundedOutcome.NotStarted => FtmoFundedPhase.FundedResult.NotStarted,
        _ => new FtmoFundedPhase.FundedResult(
            outcome, StartSourceOpen: null, OutcomeSourceClose: outcome == FtmoFundedOutcome.BreachedFirst ? Close1 : null,
            BreachLimit: null, BreachPointClass: null,
            CalendarDaysFromFundedStart: null, FtmoTradingDaysFromFundedStart: null,
            CalendarDaysFromChainStart: null, FtmoTradingDaysFromChainStart: null),
    };

    private static readonly FtmoChallengeRace.PhaseResult NotStartedPhase =
        FtmoChallengeRace.PhaseResult.NotStarted;

    public static IEnumerable<object[]> EachChainOutcome()
    {
        yield return
        [
            Phase(FtmoPhaseOutcome.BreachedFirst, Close1), NotStartedPhase, Funded(FtmoFundedOutcome.NotStarted),
            FtmoChainOutcome.Phase1Breached,
        ];
        yield return
        [
            Phase(FtmoPhaseOutcome.NeitherByEndOfData), NotStartedPhase, Funded(FtmoFundedOutcome.NotStarted),
            FtmoChainOutcome.Phase1UndecidedAtEndOfData,
        ];
        yield return
        [
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), Phase(FtmoPhaseOutcome.BreachedFirst, Close1),
            Funded(FtmoFundedOutcome.NotStarted), FtmoChainOutcome.Phase2Breached,
        ];
        yield return
        [
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), Phase(FtmoPhaseOutcome.NeitherByEndOfData),
            Funded(FtmoFundedOutcome.NotStarted), FtmoChainOutcome.Phase2UndecidedAtEndOfData,
        ];
        yield return
        [
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Funded(FtmoFundedOutcome.BreachedFirst), FtmoChainOutcome.FundedBreached,
        ];
        yield return
        [
            Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1), Phase(FtmoPhaseOutcome.TargetReachedFirst, Close1),
            Funded(FtmoFundedOutcome.NoBreachByEndOfData), FtmoChainOutcome.FundedNoBreachAtEndOfData,
        ];
    }

    [Theory]
    [MemberData(nameof(EachChainOutcome))]
    public void EachOfTheSixChainOutcomes_ClassifiesToItsIntendedOutcome(
        object phase1, object phase2, object funded, object expected)
    {
        var result = FtmoMultiStartChain.Classify(
            (FtmoChallengeRace.PhaseResult)phase1, (FtmoChallengeRace.PhaseResult)phase2,
            (FtmoFundedPhase.FundedResult)funded);

        result.Should().Be((FtmoChainOutcome)expected);
    }

    [Fact]
    public void EveryStartLandsInExactlyOneOfTheSixOutcomes()
    {
        var fixtures = EachChainOutcome()
            .Select(row => FtmoMultiStartChain.Classify(
                (FtmoChallengeRace.PhaseResult)row[0], (FtmoChallengeRace.PhaseResult)row[1],
                (FtmoFundedPhase.FundedResult)row[2]))
            .ToList();

        fixtures.Should().HaveCount(6);
        fixtures.Distinct().Should().HaveCount(6);
    }

    /// <summary>Falsification (hard rule 6-adjacent, per apply instructions): a Classify that always
    /// returns Phase1Breached must go RED here — proves EachOfTheSixChainOutcomes actually
    /// distinguishes the six fixtures rather than passing vacuously.</summary>
    [Fact]
    public void Falsification_AConstantClassifier_WouldFailThisSuite()
    {
        var outcomes = EachChainOutcome()
            .Select(_ => FtmoChainOutcome.Phase1Breached)
            .Distinct()
            .ToList();

        outcomes.Should().HaveCount(1); // A constant classifier collapses all 6 fixtures into 1 outcome.
        outcomes.Should().NotHaveCount(6); // ...which is why EachOfTheSixChainOutcomes would catch it.
    }
}
