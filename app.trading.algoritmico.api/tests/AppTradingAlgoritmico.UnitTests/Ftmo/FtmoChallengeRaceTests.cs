using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-challenge-race, Phases 2.4–2.7, 2.9 — the target scanner, phase-1/phase-2 composition over
/// the unchanged <see cref="FtmoBreachEvaluator"/>, race-only refusal, and full per-phase timing.
/// </summary>
public class FtmoChallengeRaceTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const decimal Capital = 10_000m;
    private const decimal DailyPct = 0.05m;
    private const decimal MaxPct = 0.10m;

    // Outside any DST-mismatch window: near-constant 1h offset. Each `day` is a distinct FTMO
    // trading day when a trade opens on it.
    private static DateTime At(int day, int hour = 8, int minute = 0) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net,
        ResizeOutcome outcome = ResizeOutcome.OnTarget) =>
        new(rowIndex, open, close, net, outcome, FtmoLots: 1m);

    // ---- Phase 2.4: target scanner, close groups, flat-close check ----

    [Fact]
    public void TargetDayMinimumAndFlatBook_TogetherDecideThePhase()
    {
        // Day minimum (4) already met by day 6; balance reaches +10% exactly at day 6's close, book flat.
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            Trade(4, At(6), At(6, 9), net: 600m), // brings balance to 11,000 = +10%
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        phase1.OutcomeSourceClose.Should().Be(At(6, 9));
    }

    [Fact]
    public void AnOpenSiblingPositionDefersTheDecisionToALaterClose()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            // Reaches target at day 6's close but a sibling is still open across it.
            Trade(4, At(6), At(6, 9), net: 600m),
            Trade(5, At(6, 8), At(7, 9), net: 0m), // opens before day-6 close, closes day 7 — spans it
            // Reaches target again (book flat) at day 8's close.
            Trade(6, At(8), At(8, 9), net: 0m),
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        phase1.OutcomeSourceClose.Should().Be(At(7, 9));
        phase1.FirstTargetTouchSourceClose.Should().Be(At(6, 9));
    }

    /// <summary>
    /// Falsification-bearing (hard rule 5, close groups): two trades sharing the same close instant,
    /// day minimum already met, neither alone reaches the target but their sum does. A deliberately
    /// broken PER-ROW evaluation (checking after trade 4 alone, before trade 5's identical-instant
    /// close is folded in) must NOT report the target reached at that instant.
    /// </summary>
    [Fact]
    public void TwoTradesClosingAtTheSameInstant_AreOneEvent_TargetCrossedByThePair()
    {
        var sharedClose = At(6, 9);
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            Trade(4, At(6), sharedClose, net: 300m), // balance after just this row: 10,600 (not yet target)
            Trade(5, At(6, 1), sharedClose, net: 300m), // balance after BOTH: 10,900... need +10% = 11,000
        };

        // Bump trade 4/5 nets so the PAIR crosses 11,000 but neither alone does.
        trades =
        [
            trades[0], trades[1], trades[2], trades[3],
            Trade(4, At(6), sharedClose, net: 500m), // alone: 10,800 — not target
            Trade(5, At(6, 1), sharedClose, net: 500m), // alone (if evaluated independently, starting from 10,300): 10,800 — not target either
        ];

        var groupEvaluation = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        groupEvaluation.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        groupEvaluation.OutcomeSourceClose.Should().Be(sharedClose);

        // Falsification: a per-row (not per-group) scan would stop after row 4 (balance 10,800, no
        // target) and after row 5 alone would also read 10,800 relative to a per-row running total
        // that never actually folds both rows before checking — demonstrated by checking the target
        // against each row's OWN net in isolation from the pre-group balance.
        var preGroupBalance = Capital + 100m + 100m + 100m + 100m; // 10,400 before the shared-close group
        var row4Alone = preGroupBalance + 500m;
        var row5Alone = preGroupBalance + 500m;
        (row4Alone >= Capital * 1.10m).Should().BeFalse("row 4 alone must not cross the target");
        (row5Alone >= Capital * 1.10m).Should().BeFalse("row 5 alone must not cross the target either");
        (preGroupBalance + 500m + 500m >= Capital * 1.10m).Should().BeTrue("only the PAIR crosses the target");
    }

    [Fact]
    public void AnUnscalablePositionSpanningTheTargetClose_DoesNotBlockTheTarget()
    {
        var targetClose = At(6, 9);
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            Trade(4, At(6), targetClose, net: 600m),
            Trade(5, At(6, 8), At(7, 9), net: null, ResizeOutcome.Unscalable), // spans the close, but Unscalable
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        phase1.OutcomeSourceClose.Should().Be(targetClose);
    }

    [Fact]
    public void TheDayMinimumNotYetMet_DefersTheDecision()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 900m), // balance 11,000 on day 2 — day minimum (4) not met
            Trade(2, At(3), At(3, 9), net: 0m),
            Trade(3, At(4), At(4, 9), net: 0m),
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.FirstTargetTouchSourceClose.Should().Be(At(2, 9));
        phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        phase1.OutcomeSourceClose.Should().Be(At(4, 9));
    }

    // ---- Phase 2.5: composition — breach wins, phase handover ----

    /// <summary>
    /// Falsification-bearing (hard rule 5, breach-wins tie): a deliberate target-favoring swap
    /// (checking target-reached before breach) would report TargetReachedFirst; the real
    /// implementation must report BreachedFirst on the tie.
    /// </summary>
    [Fact]
    public void APhaseBreachAndTheTargetOnTheSameClose_BothResolveToBreachedFirst()
    {
        var sameClose = At(6, 9);
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            // Day-6 close both crosses the target AND breaches the max floor from a prior high
            // balance: prev-midnight balance 15,000 (via an earlier big gain), max floor at
            // Capital*(1-0.10)=9,000 is far below, so use the DAILY floor instead: reference
            // (previous midnight) is 10,400; -5% floor = 9,880. A big single-day loss of -5,000
            // undercuts the daily floor while the raw balance still nominally could cross target
            // once a large offsetting same-close gain is folded in the SAME group.
            Trade(4, At(6), sameClose, net: -5_000m),
            Trade(5, At(6, 1), sameClose, net: 5_600m), // net pair: +600 -> balance 11,000 (+10%), but daily floor already breached mid-group by row 4 alone: 10,400-5,000=5,400 < 9,880
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
    }

    [Fact]
    public void ACleanBreach_EndsThePhase()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: -2_000m), // 20% drop — breaches max floor (10%) cleanly, daily loose enough not to trigger
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, dailyPct: 0.50m, maxPct: MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        phase1.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Max);
        phase1.BreachPointClass.Should().Be(FtmoBreachPointClass.Clean);
    }

    [Fact]
    public void AContingentBreach_AlsoEndsThePhase()
    {
        var breachClose = At(1, 9);
        var trades = new[]
        {
            Trade(0, At(1), breachClose, net: -800m), // 8% drop breaches daily (5%)
            Trade(1, At(1, 7), At(1, 10), net: 0m), // overlaps the breach close -> contingent cause
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        phase1.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Daily);
        phase1.BreachPointClass.Should().Be(FtmoBreachPointClass.Contingent);
    }

    [Fact]
    public void ABreachAfterAPrematureTargetTouch_EndsThePhaseAsBreached()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 900m), // day 2: target touched, day minimum (4) not met
            Trade(2, At(3), At(3, 9), net: -900m), // daily floor breach on day 3
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
    }

    [Fact]
    public void ThePhaseReportsBothTheFirstTouchAndTheFinalDecision()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 900m), // day 2 touch
            Trade(2, At(3), At(3, 9), net: -900m), // day 3 breach
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.FirstTargetTouchSourceClose.Should().Be(At(2, 9));
        phase1.OutcomeSourceClose.Should().Be(At(3, 9));
    }

    [Fact]
    public void DataEndingBeforeEitherEventResolves_IsReportedHonestly()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 10m),
            Trade(1, At(2), At(2, 9), net: -10m),
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.NeitherByEndOfData);
        phase1.OutcomeSourceClose.Should().BeNull();
        phase1.BreachLimit.Should().BeNull();
    }

    [Fact]
    public void PhaseTwoStartsAtTheNextTradeOpenedAfterThePhaseOneDecision()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 20);
        trades =
        [
            .. trades,
            Trade(trades.Length, At(21), At(21, 9), net: 0m),
        ];

        var chain = FtmoChallengeRace.RunChain(trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        chain.Phase2.Outcome.Should().NotBe(FtmoPhaseOutcome.NotStarted);
        chain.Phase2.StartSourceOpen.Should().Be(At(21));
    }

    /// <summary>
    /// Orchestrator decision (point 4): phase 1 reaches its target at the LAST replayed close, with no
    /// trade opening afterwards — phase 2 genuinely started (the target WAS reached) but has no data,
    /// so it is reported as <c>NeitherByEndOfData</c>, not <c>NotStarted</c>, with a null start.
    /// </summary>
    [Fact]
    public void PhaseOneReachesItsTargetOnTheLastReplayedClose_WithNoFurtherData()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6); // no trade opens after the day-6 close

        var chain = FtmoChallengeRace.RunChain(trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        chain.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        chain.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.NeitherByEndOfData);
        chain.Phase2.StartSourceOpen.Should().BeNull();
    }

    // ---- ftmo-multi-start PR0, bug A: no post-breach touch/day-minimum readout on BreachedFirst ----

    /// <summary>
    /// RED (task 0.1.1): a phase whose balance crosses the target percentage and meets the day-4
    /// minimum only on a close AFTER the phase's breach close. The outcome stays BreachedFirst
    /// (unchanged), but both post-breach readouts must be null — a live FTMO account never reaches
    /// them once the breach has already closed the phase.
    /// </summary>
    [Fact]
    public void ABreachedPhaseWhoseBalanceLaterTouchesTheTarget_ReportsNoPostBreachTouch()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: -2_000m), // day 1: clean breach (20% drop)
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            // Day 6 (5th trading day, day-min of 4 met): balance 8,300 + 3,000 = 11,300 >= target — but this is AFTER the day-1 breach.
            Trade(4, At(6), At(6, 9), net: 3_000m),
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        phase1.OutcomeSourceClose.Should().Be(At(1, 9));
        phase1.FirstTargetTouchSourceClose.Should().BeNull();
        phase1.MinTradingDaysMetFtmoDay.Should().BeNull();
    }

    /// <summary>
    /// RED (task 0.1.2): a phase whose balance touches the target percentage on a close strictly
    /// BEFORE the phase's breach close. That earlier, pre-breach touch must still be reported
    /// unchanged.
    /// </summary>
    [Fact]
    public void ABreachedPhaseWhoseBalanceTouchedTheTargetBeforeTheBreach_StillReportsTheTouch()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 1_200m), // day 1: balance 11,200 -> target touched
            Trade(1, At(2), At(2, 9), net: -2_500m), // day 2: balance 8,700 -> breaches both floors
        };

        var phase1 = FtmoChallengeRace.RunPhase(
            trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct, FtmoChallengeRules.Phase1TargetPct);

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        phase1.OutcomeSourceClose.Should().Be(At(2, 9));
        phase1.FirstTargetTouchSourceClose.Should().Be(At(1, 9));
    }

    // ---- ftmo-multi-start PR0, bug B: handover requires Open >= T AND Close > T ----

    /// <summary>
    /// RED (task 0.2.1): an Unscalable trade whose Open is before phase 1's target-deciding close T
    /// and whose Close is strictly after T must NOT enter phase 2's subset or become its anchor.
    /// Falsification (task 0.2.4): under the current `Close > T`-only filter, this straddling row's
    /// Close sorts before the real next trade's close, so it wrongly becomes phase 2's anchor.
    /// </summary>
    [Fact]
    public void AnUnscalableRowStraddlingTheDecisionClose_DoesNotEnterPhaseTwo()
    {
        var target = BuildTargetReachingSeries(decidingDay: 20); // T = At(20, 9)
        var trades = new List<ProjectedTrade>(target)
        {
            // Straddles T: Open before T, Close after T, no other trade opens in the gap.
            Trade(target.Length, At(20, 8), At(21, 9), net: null, ResizeOutcome.Unscalable),
            Trade(target.Length + 1, At(22), At(22, 9), net: 0m),
        };

        var chain = FtmoChallengeRace.RunChain(
            trades.ToArray(), Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        chain.Phase2.Outcome.Should().NotBe(FtmoPhaseOutcome.NotStarted);
        chain.Phase2.StartSourceOpen.Should().Be(At(22));
    }

    /// <summary>
    /// (task 0.2.2): a row whose Open and Close both equal T belongs to phase 1's close group at T,
    /// not phase 2's subset — already true under `Close > T` (T is not strictly less than itself), so
    /// this pins the invariant rather than falsifying the pre-fix code.
    /// </summary>
    [Fact]
    public void AZeroDurationRowAtExactlyT_StaysInPhaseOnesGroup()
    {
        var target = BuildTargetReachingSeries(decidingDay: 20); // T = At(20, 9)
        var trades = new List<ProjectedTrade>(target)
        {
            Trade(target.Length, At(20, 9), At(20, 9), net: 0m), // zero-duration row exactly at T
            Trade(target.Length + 1, At(22), At(22, 9), net: 0m),
        };

        var chain = FtmoChallengeRace.RunChain(
            trades.ToArray(), Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        chain.Phase2.StartSourceOpen.Should().Be(At(22));
    }

    [Fact]
    public void PhaseTwoIsNotStartedWhenPhaseOneDoesNotReachItsTarget()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: -2_000m), // breach
        };

        var chain = FtmoChallengeRace.RunChain(trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        chain.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.NotStarted);
        chain.Phase2.StartSourceOpen.Should().BeNull();
        chain.Phase2.OutcomeSourceClose.Should().BeNull();
        chain.Phase2.FirstTargetTouchSourceClose.Should().BeNull();
        chain.Phase2.CalendarDaysElapsed.Should().BeNull();
        chain.Phase2.FtmoTradingDaysElapsed.Should().BeNull();
    }

    /// <summary>Builds a series where the target is reached on FTMO trading day <paramref name="decidingDay"/>.</summary>
    private static ProjectedTrade[] BuildTargetReachingSeries(int decidingDay)
    {
        return
        [
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 100m),
            Trade(2, At(3), At(3, 9), net: 100m),
            Trade(3, At(5), At(5, 9), net: 100m),
            Trade(4, At(decidingDay), At(decidingDay, 9), net: 600m),
        ];
    }

    // ---- Phase 2.7: DTO field coverage (Rules echoed) ----

    [Fact]
    public void BothPhaseTargetsAndTheDayMinimum_AreEchoedOnANonRefusedRace()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: null, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        dto.Should().NotBeNull();
        dto!.Rules.Phase1TargetPct.Should().Be(0.10m);
        dto.Rules.Phase2TargetPct.Should().Be(0.05m);
        dto.Rules.MinTradingDaysPerPhase.Should().Be(4);
        dto.Rules.TimeLimitDays.Should().BeNull();
    }

    [Fact]
    public void ATargetReachedFirstPhase_ReportsAllRequiredTimingFields()
    {
        var trades = new[]
        {
            Trade(0, At(1), At(1, 9), net: 100m),
            Trade(1, At(2), At(2, 9), net: 900m), // touch, day min not met
            Trade(2, At(3), At(3, 9), net: 0m),
            Trade(3, At(4), At(4, 9), net: 0m), // day-4 minimum met, flat, still above target
        };

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: null, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        var phase1 = dto!.Phase1!;
        phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        phase1.StartSourceOpen.Should().Be(At(1));
        phase1.FirstTargetTouchSourceClose.Should().Be(At(2, 9));
        phase1.MinTradingDaysMetFtmoDay.Should().NotBeNull();
        phase1.OutcomeSourceClose.Should().Be(At(4, 9));
        phase1.CalendarDaysElapsed.Should().NotBeNull();
        phase1.FtmoTradingDaysElapsed.Should().NotBeNull();
    }

    [Fact]
    public void EveryProducedRaceResultReports_TheFourStateEnumNeverABoolean()
    {
        typeof(FtmoPhaseOutcome).IsEnum.Should().BeTrue();
        Enum.GetUnderlyingType(typeof(FtmoPhaseOutcome)).Should().NotBe(typeof(bool));
        Enum.GetValues<FtmoPhaseOutcome>().Should().HaveCount(4);
    }

    // ---- Phase 2.6: race-only refusal ----

    [Fact]
    public void AStoredTargetOf010_DoesNotRefuse()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: 0.10m, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        dto!.Refusal.Should().BeNull();
        dto.Phase1.Should().NotBeNull();
    }

    [Fact]
    public void AStoredTargetOtherThan010_RefusesWithBothValuesEchoed()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: 0.08m, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        dto!.Refusal.Should().Be(FtmoChallengeRaceRefusal.ProfitTargetMismatch);
        dto.StoredProfitTargetPct.Should().Be(0.08m);
        dto.Rules.Phase1TargetPct.Should().Be(0.10m);
        dto.Phase1.Should().BeNull();
        dto.Phase2.Should().BeNull();
    }

    [Fact]
    public void ANullStoredTarget_UsesTheFixedRuleWithoutRefusing()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: null, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        dto!.Refusal.Should().BeNull();
    }

    // ---- Phase 2.9: no-pass-wording coverage extension ----

    [Fact]
    public void NoOutputContainsBannedSurvivalWording()
    {
        var banned = new[] { "passed", "safe", "survived", "would have passed" };
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: null, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        var disclosure = dto!.Disclosure.ToLowerInvariant();
        foreach (var word in banned)
            disclosure.Should().NotContain(word);

        foreach (var enumType in new[] { typeof(FtmoPhaseOutcome), typeof(FtmoChallengeRaceRefusal) })
        {
            Enum.GetUnderlyingType(enumType).Should().NotBe(typeof(bool));
            foreach (var name in Enum.GetNames(enumType))
            {
                var lower = name.ToLowerInvariant();
                foreach (var word in banned)
                    lower.Should().NotContain(word);
            }
        }
    }

    [Fact]
    public void TheDisclosureStatesBothBiasDirectionsWithoutSurvivalWording()
    {
        var trades = BuildTargetReachingSeries(decidingDay: 6);

        var dto = FtmoChallengeRace.Evaluate(
            profitTargetPct: null, trades, trades, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        var text = dto!.Disclosure;
        text.Should().Contain("swap");
        text.Should().Contain("optimistic");
        text.Should().Contain("strong result");
    }
}
