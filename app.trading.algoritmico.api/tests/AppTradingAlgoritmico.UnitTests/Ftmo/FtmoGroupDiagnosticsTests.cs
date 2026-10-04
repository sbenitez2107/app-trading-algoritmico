using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B3.1 (design.md D6) — the pure group diagnostics. The attribution and peak tests
/// hand-build the merged series and the run's starts, so every case (a tie, a funded breach, the phase-subset
/// filter) is exact; the engine-driven tests at the bottom prove the same shapes through the UNEDITED evaluator.
/// </summary>
public class FtmoGroupDiagnosticsTests
{
    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid IdC = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    private static readonly IReadOnlyList<(Guid, string)> Abc = [(IdA, "A"), (IdB, "B"), (IdC, "C")];
    private static readonly GroupWindow Wide = new(At(1, 0), At(28, 0));

    private static DateTime At(int day, int hour, int minute = 0) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int row, DateTime open, DateTime close, decimal? net = 10m, ResizeOutcome? outcome = null) =>
        new(row, open, close, net, outcome ?? (net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget), net is null ? 0m : 1m);

    private static MemberSeries Series(int order, params ProjectedTrade[] trades) => new(order, trades, trades);

    private static MergedSeries Merged(params MemberSeries[] members) => Merge(members, Wide);

    // ---- run builders (only Starts matters to the diagnostics) ----

    private static readonly FtmoChallengeRulesDto Rules = new(0.10m, 0.05m, 4, TimeLimitDays: null);

    private static FtmoChallengePhaseDto Phase(FtmoPhaseOutcome outcome, DateTime? close = null) =>
        new(outcome, StartSourceOpen: null, FirstTargetTouchSourceClose: null, MinTradingDaysMetFtmoDay: null, close,
            BreachLimit: null, BreachPointClass: null, CalendarDaysElapsed: null, FtmoTradingDaysElapsed: null, FxBandEnd: null);

    private static FtmoFundedPhaseDto Funded(FtmoFundedOutcome outcome, DateTime? close = null) =>
        new(outcome, StartSourceOpen: null, close, BreachLimit: null, BreachPointClass: null, null, null, null, null);

    private static FtmoMultiStartRowDto Start(
        int index, DateTime startOpen, FtmoChallengePhaseDto phase1, FtmoChallengePhaseDto? phase2 = null, FtmoFundedPhaseDto? funded = null) =>
        new(index, startOpen, DateOnly.FromDateTime(startOpen), new DateOnly(2026, 1, 1), FtmoChainOutcome.Phase1Breached,
            IsCensored: false, RunwayCalendarDays: null, CalendarDaysToBothTargets: null, phase1,
            phase2 ?? Phase(FtmoPhaseOutcome.NotStarted), funded ?? Funded(FtmoFundedOutcome.NotStarted),
            FtmoFxBandEnd.FxLow, FxRoundingSensitive: false);

    private static FtmoMultiStartRunDto Run(params FtmoMultiStartRowDto[] starts) =>
        new(Guid.Empty, BacktestRunKind.Deploy, BacktestSegment.InSample, FtmoSimulationStatus.Evaluated, null, null, null,
            FtmoStartGrain.Monthly, Rules, starts, Summary: null, MonthsWithoutStart: [], Start1DiffersFromSingleStartAnchor: false,
            FxLow: 1m, FxHigh: 1m, UnscalableCount: 0, NotModelled: [], Disclosure: string.Empty);

    private static FtmoGroupDiagnosticsDto Diagnose(MergedSeries merged, params FtmoMultiStartRowDto[] starts) =>
        FtmoGroupDiagnostics.Compute(Abc, merged, Run(starts));

    private static FtmoGroupMemberAttributionDto Of(FtmoGroupDiagnosticsDto d, Guid id) =>
        d.Attribution.Members.Single(m => m.StrategyId == id);

    // ---- B3.1.1: per-member contribution ----

    [Fact]
    public void Contribution_IsPerMemberOverTheWindow_AndSumsToTheMergedSeriesAtEachEnd()
    {
        var aLow = new[]
        {
            Trade(0, At(2, 8), At(2, 9), 100m),
            Trade(1, At(12, 8), At(12, 9), 50m, ResizeOutcome.RaisedToMinimum),
            Trade(2, At(13, 8), At(13, 9), net: null),
            Trade(3, At(14, 8), At(14, 9), 100m),
        };
        var aHigh = new[]
        {
            Trade(0, At(2, 8), At(2, 9), 110m),
            Trade(1, At(12, 8), At(12, 9), 55m, ResizeOutcome.RaisedToMinimum),
            Trade(2, At(13, 8), At(13, 9), net: null),
            Trade(3, At(14, 8), At(14, 9), 110m),
        };
        var bLow = new[] { Trade(0, At(12, 10), At(12, 11), -20m, ResizeOutcome.CappedAtMaximum), Trade(1, At(15, 8), At(15, 9), 5m) };
        var bHigh = new[] { Trade(0, At(12, 10), At(12, 11), -22m, ResizeOutcome.CappedAtMaximum), Trade(1, At(15, 8), At(15, 9), 6m) };

        // The window starts at Jan 12 08:00: A's Jan 2 row is trimmed and must not count.
        var window = new GroupWindow(At(12, 8), At(15, 9));
        var merged = Merge([new MemberSeries(0, aLow, aHigh), new MemberSeries(1, bLow, bHigh)], window);

        var d = Diagnose(merged);

        d.Contributions.Single(c => c.StrategyId == IdA).Should().Be(
            new FtmoGroupMemberContributionDto(
                IdA, "A", InWindowTrades: 3, ScalableTrades: 2, NetLow: 150m, NetHigh: 165m,
                RaisedToMinimum: 1, CappedAtMaximum: 0, Unscalable: 1));
        d.Contributions.Single(c => c.StrategyId == IdB).Should().Be(
            new FtmoGroupMemberContributionDto(
                IdB, "B", InWindowTrades: 2, ScalableTrades: 2, NetLow: -15m, NetHigh: -16m,
                RaisedToMinimum: 0, CappedAtMaximum: 1, Unscalable: 0));

        d.Contributions.Sum(c => c.InWindowTrades).Should().Be(merged.Low.Count);
        d.Contributions.Sum(c => c.NetLow).Should().Be(merged.Low.Sum(t => t.Net ?? 0m));
        d.Contributions.Sum(c => c.NetHigh).Should().Be(merged.High.Sum(t => t.Net ?? 0m));
    }

    [Fact]
    public void Contribution_ListsEveryMemberInMemberOrder_EvenOneWithNoRows()
    {
        var merged = Merged(Series(0, Trade(0, At(12, 8), At(12, 9))), Series(1), Series(2, Trade(0, At(13, 8), At(13, 9))));

        var d = Diagnose(merged);

        d.Contributions.Select(c => c.StrategyId).Should().Equal(IdA, IdB, IdC);
        d.Contributions[1].InWindowTrades.Should().Be(0);
        d.Contributions[1].NetLow.Should().Be(0m);
    }

    // ---- B3.1.2: first-breach attribution ----

    [Fact]
    public void Attribution_ABreachClosedByOneMember_IsASoleContributorStartOfThatMemberForPhase1()
    {
        var merged = Merged(
            Series(0, Trade(0, At(13, 8), At(13, 9), -600m)),
            Series(1, Trade(0, At(14, 8), At(14, 9))));

        var d = Diagnose(merged, Start(0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(13, 9))));

        var a = Of(d, IdA);
        a.Phase1Starts.Should().Be(1);
        a.SoleContributorStarts.Should().Be(1);
        a.SharedCloseStarts.Should().Be(0);
        Of(d, IdB).Should().Be(new FtmoGroupMemberAttributionDto(IdB, "B", 0, 0, 0, 0, 0));
        d.Attribution.DecidingBreachStarts.Should().Be(1);
        d.Attribution.SharedCloseStarts.Should().Be(0);
        d.Attribution.UnattributedStarts.Should().Be(0);
    }

    [Fact]
    public void Attribution_ASameInstantCloseByTwoMembers_CreditsBothAndCountsASharedCloseStart()
    {
        var merged = Merged(
            Series(0, Trade(0, At(13, 8), At(13, 9), -600m)),
            Series(1, Trade(0, At(13, 8, 30), At(13, 9))));

        var d = Diagnose(merged, Start(0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(13, 9))));

        foreach (var id in new[] { IdA, IdB })
        {
            var m = Of(d, id);
            m.Phase1Starts.Should().Be(1, "every member closing at the breach instant is credited, none is picked");
            m.SharedCloseStarts.Should().Be(1);
            m.SoleContributorStarts.Should().Be(0);
        }

        d.Attribution.SharedCloseStarts.Should().Be(1, "the kind counts the start ONCE, not once per member");
        d.Attribution.DecidingBreachStarts.Should().Be(1);
    }

    [Fact]
    public void Attribution_APhase2Breach_IsCreditedForPhase2AndTheSubsetRuleExcludesARowOpenedBeforePhase1Closed()
    {
        // Phase 1 reaches its target at Jan 13 09:00 (T). Phase 2 breaches at Jan 14 09:00. Member A's row closes at
        // that instant too but OPENED before T, so it is not in phase 2's replayed subset and must not be credited.
        var merged = Merged(
            Series(0, Trade(0, At(12, 12), At(14, 9)), Trade(1, At(12, 8), At(12, 9))),
            Series(1, Trade(0, At(13, 8), At(13, 9)), Trade(1, At(14, 8), At(14, 9), -600m)));

        var d = Diagnose(
            merged,
            Start(
                0, At(12, 8), Phase(FtmoPhaseOutcome.TargetReachedFirst, At(13, 9)), Phase(FtmoPhaseOutcome.BreachedFirst, At(14, 9))));

        var b = Of(d, IdB);
        b.Phase2Starts.Should().Be(1);
        b.Phase1Starts.Should().Be(0);
        b.SoleContributorStarts.Should().Be(1);
        Of(d, IdA).Should().Be(new FtmoGroupMemberAttributionDto(IdA, "A", 0, 0, 0, 0, 0));
        d.Attribution.SharedCloseStarts.Should().Be(0);
    }

    [Fact]
    public void Attribution_AFundedBreach_IsCreditedForTheFundedPhase()
    {
        var merged = Merged(
            Series(0, Trade(0, At(20, 8), At(20, 9), -600m)),
            Series(1, Trade(0, At(13, 8), At(13, 9)), Trade(1, At(16, 8), At(16, 9))));

        var d = Diagnose(
            merged,
            Start(
                0, At(13, 8), Phase(FtmoPhaseOutcome.TargetReachedFirst, At(13, 9)), Phase(FtmoPhaseOutcome.TargetReachedFirst, At(16, 9)),
                Funded(FtmoFundedOutcome.BreachedFirst, At(20, 9))));

        var a = Of(d, IdA);
        a.FundedStarts.Should().Be(1);
        a.Phase1Starts.Should().Be(0);
        a.Phase2Starts.Should().Be(0);
        a.SoleContributorStarts.Should().Be(1);
    }

    [Fact]
    public void Attribution_TheDecidingBreachIsTheEarliestPhaseThatBreached_NeverALaterOne()
    {
        // A start cannot normally have both, but the rule is "Phase 1, else Phase 2, else Funded": it must stop at the first.
        var merged = Merged(
            Series(0, Trade(0, At(13, 8), At(13, 9), -600m)),
            Series(1, Trade(0, At(14, 8), At(14, 9), -600m)));

        var d = Diagnose(
            merged,
            Start(
                0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(13, 9)), Phase(FtmoPhaseOutcome.BreachedFirst, At(14, 9))));

        Of(d, IdA).Phase1Starts.Should().Be(1);
        Of(d, IdB).Phase2Starts.Should().Be(0);
        d.Attribution.DecidingBreachStarts.Should().Be(1);
    }

    [Fact]
    public void Attribution_ARowOpenedBeforeTheStartOrWithNoNet_IsNotACandidate()
    {
        // B opened before this start's anchor (Jan 13 08:00) and closes at the breach instant; C is Unscalable
        // (null Net) and closes then too. Only A is credited.
        var merged = Merged(
            Series(0, Trade(0, At(13, 8), At(13, 9), -600m)),
            Series(1, Trade(0, At(12, 8), At(13, 9))),
            Series(2, Trade(0, At(13, 8, 30), At(13, 9), net: null)));

        var d = Diagnose(merged, Start(0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(13, 9))));

        Of(d, IdA).SoleContributorStarts.Should().Be(1);
        Of(d, IdB).Phase1Starts.Should().Be(0);
        Of(d, IdC).Phase1Starts.Should().Be(0);
        d.Attribution.SharedCloseStarts.Should().Be(0);
    }

    [Fact]
    public void Attribution_AStartWithNoBreach_IsCreditedToNoOne()
    {
        var merged = Merged(Series(0, Trade(0, At(13, 8), At(13, 9))), Series(1, Trade(0, At(14, 8), At(14, 9))));

        var d = Diagnose(
            merged,
            Start(0, At(13, 8), Phase(FtmoPhaseOutcome.NeitherByEndOfData)),
            Start(1, At(14, 8), Phase(FtmoPhaseOutcome.TargetReachedFirst, At(14, 9)), Phase(FtmoPhaseOutcome.NeitherByEndOfData)));

        d.Attribution.DecidingBreachStarts.Should().Be(0);
        d.Attribution.SharedCloseStarts.Should().Be(0);
        d.Attribution.UnattributedStarts.Should().Be(0);
        d.Attribution.Members.Should().OnlyContain(m =>
            m.Phase1Starts == 0 && m.Phase2Starts == 0 && m.FundedStarts == 0 && m.SoleContributorStarts == 0 && m.SharedCloseStarts == 0);
    }

    [Fact]
    public void Attribution_SoleStartsOfAllMembersPlusSharedCloseStarts_EqualTheDecidingBreachStarts()
    {
        var merged = Merged(
            Series(0, Trade(0, At(13, 8), At(13, 9), -600m), Trade(1, At(15, 8), At(15, 9), -600m)),
            Series(1, Trade(0, At(14, 8), At(14, 9), -600m), Trade(1, At(15, 8, 30), At(15, 9))));

        var d = Diagnose(
            merged,
            Start(0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(13, 9))),
            Start(1, At(14, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(14, 9))),
            Start(2, At(15, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(15, 9))),
            Start(3, At(16, 8), Phase(FtmoPhaseOutcome.NeitherByEndOfData)));

        d.Attribution.DecidingBreachStarts.Should().Be(3, "the no-breach start is not a deciding start");
        d.Attribution.Members.Sum(m => m.SoleContributorStarts).Should().Be(2);
        d.Attribution.SharedCloseStarts.Should().Be(1);
        (d.Attribution.Members.Sum(m => m.SoleContributorStarts) + d.Attribution.SharedCloseStarts + d.Attribution.UnattributedStarts)
            .Should().Be(d.Attribution.DecidingBreachStarts);
    }

    [Fact]
    public void Attribution_ABreachCloseWithNoRowInTheMergedSeries_IsReportedAsUnattributedNotSilentlyDropped()
    {
        var merged = Merged(Series(0, Trade(0, At(13, 8), At(13, 9))));

        var d = Diagnose(merged, Start(0, At(13, 8), Phase(FtmoPhaseOutcome.BreachedFirst, At(20, 9))));

        d.Attribution.DecidingBreachStarts.Should().Be(1);
        d.Attribution.UnattributedStarts.Should().Be(1);
        d.Attribution.Members.Should().OnlyContain(m => m.Phase1Starts == 0 && m.SoleContributorStarts == 0);
    }

    // ---- B3.1.3: peak concurrent open positions ----

    [Fact]
    public void Peak_ThreeOverlappingMembers_IsThreeAtTheFirstInstantItIsReached_WithThoseMembers()
    {
        var merged = Merged(
            Series(0, Trade(0, At(12, 10), At(12, 12))),
            Series(1, Trade(0, At(12, 11), At(12, 13))),
            Series(2, Trade(0, At(12, 11, 30), At(12, 11, 45))));

        var peak = Diagnose(merged).Peak;

        peak.PeakConcurrentOpen.Should().Be(3);
        peak.FirstReachedSource.Should().Be(At(12, 11, 30));
        peak.MemberIdsAtPeak.Should().Equal(IdA, IdB, IdC);
    }

    [Fact]
    public void Peak_ACloseAndAnOpenAtTheSameInstant_AreNotConcurrent()
    {
        var merged = Merged(Series(0, Trade(0, At(12, 10), At(12, 12))), Series(1, Trade(0, At(12, 12), At(12, 13))));

        var peak = Diagnose(merged).Peak;

        peak.PeakConcurrentOpen.Should().Be(1);
        peak.FirstReachedSource.Should().Be(At(12, 10));
    }

    [Fact]
    public void Peak_ZeroDurationAndUnscalableRows_AreIgnored()
    {
        var merged = Merged(
            Series(0, Trade(0, At(12, 10), At(12, 10)), Trade(1, At(12, 10), At(12, 12), net: null)),
            Series(1, Trade(0, At(12, 10), At(12, 11))));

        var peak = Diagnose(merged).Peak;

        peak.PeakConcurrentOpen.Should().Be(1);
        peak.MemberIdsAtPeak.Should().Equal(IdB);
    }

    [Fact]
    public void Peak_TwoPositionsOfOneMember_CountTwoButListTheMemberOnce()
    {
        var merged = Merged(Series(0, Trade(0, At(12, 10), At(12, 12)), Trade(1, At(12, 11), At(12, 13))));

        var peak = Diagnose(merged).Peak;

        peak.PeakConcurrentOpen.Should().Be(2);
        peak.MemberIdsAtPeak.Should().Equal(IdA);
    }

    [Fact]
    public void Peak_WhenTwoMomentsReachTheSamePeak_ReportsTheEarlierOne()
    {
        var merged = Merged(
            Series(0, Trade(0, At(12, 10), At(12, 11)), Trade(1, At(12, 14), At(12, 15))),
            Series(1, Trade(0, At(12, 10, 30), At(12, 11, 30)), Trade(1, At(12, 14, 30), At(12, 15, 30))));

        var peak = Diagnose(merged).Peak;

        peak.PeakConcurrentOpen.Should().Be(2);
        peak.FirstReachedSource.Should().Be(At(12, 10, 30));
    }

    [Fact]
    public void Peak_WithNoCountableRows_IsZeroWithNoInstantAndNoMembers()
    {
        var peak = Diagnose(Merged(Series(0, Trade(0, At(12, 10), At(12, 10))), Series(1))).Peak;

        peak.PeakConcurrentOpen.Should().Be(0);
        peak.FirstReachedSource.Should().BeNull();
        peak.MemberIdsAtPeak.Should().BeEmpty();
    }

    // ---- B3.1.5: wiring through ComputeGroup, and the engine-driven shapes ----

    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static GroupMemberKindInput Ok(Guid id, int order, params ProjectedTrade[] trades) =>
        new(id, $"member-{order}", order, Guid.NewGuid(), null,
            new FtmoSimulationInputs.RunProjection(
                null, BacktestSegment.InSample, [.. trades], [.. trades], 0, 0, trades.Count(t => t.Outcome == ResizeOutcome.Unscalable)));

    private static GroupParams Params() => new(Jerusalem, Berlin, 10_000m, 0.05m, 0.10m, null, (1m, 1m), Rules);

    private static FtmoGroupKindResultDto Compute(params GroupMemberKindInput[] members) =>
        ComputeGroup(BacktestRunKind.Deploy, members, Params(), CancellationToken.None);

    [Fact]
    public void ComputeGroup_ASuccessfulKind_CarriesDiagnostics_AndARefusedKindCarriesNone()
    {
        var a = Ok(IdA, 0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(13, 8), At(13, 9)), Trade(2, At(15, 8), At(15, 9)));
        var b = Ok(IdB, 1, Trade(0, At(12, 10), At(12, 11)), Trade(1, At(14, 8), At(14, 9)), Trade(2, At(16, 8), At(16, 9)));
        var missing = new GroupMemberKindInput(IdB, "member-1", 1, RunId: null, Refusal: null, Projection: null);
        var disjoint = Ok(IdB, 1, Trade(0, At(20, 8), At(20, 9)), Trade(1, At(22, 8), At(22, 9)));
        var emptied = Ok(IdA, 0, Trade(0, At(2, 8), At(28, 9)));

        var ok = Compute(a, b);

        ok.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        ok.Diagnostics.Should().NotBeNull();
        ok.Diagnostics!.Contributions.Select(c => c.StrategyId).Should().Equal(IdA, IdB);
        ok.Diagnostics.Contributions.Sum(c => c.InWindowTrades).Should().Be(ok.Coverage.Sum(c => c.InWindowTrades));

        Compute(a, missing).Diagnostics.Should().BeNull("MemberMissingKind carries no diagnostics");
        Compute(a, disjoint).Diagnostics.Should().BeNull("NoCommonWindow carries no diagnostics");
        Compute(emptied, b).Diagnostics.Should().BeNull("MemberHasNoTradesInWindow carries no diagnostics");
    }

    [Fact]
    public void ComputeGroup_ASoleContributorBreach_IsAttributedToThatMember_AndANoBreachStartToNoOne()
    {
        var a = Ok(IdA, 0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(13, 8), At(13, 9), -600m), Trade(2, At(15, 8), At(15, 9)));
        var b = Ok(IdB, 1, Trade(0, At(12, 10), At(12, 11)), Trade(1, At(14, 8), At(14, 9)), Trade(2, At(16, 8), At(16, 9)));

        var result = Compute(a, b);

        var start = result.Run!.Starts.Should().ContainSingle().Subject;
        start.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        start.Phase1.OutcomeSourceClose.Should().Be(At(13, 9));
        var attribution = result.Diagnostics!.Attribution;
        attribution.Members.Single(m => m.StrategyId == IdA).Should().Be(new FtmoGroupMemberAttributionDto(IdA, "member-0", 1, 0, 0, 1, 0));
        attribution.Members.Single(m => m.StrategyId == IdB).Should().Be(new FtmoGroupMemberAttributionDto(IdB, "member-1", 0, 0, 0, 0, 0));

        var calm = Compute(
            Ok(IdA, 0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(13, 8), At(13, 9)), Trade(2, At(15, 8), At(15, 9))), b);
        calm.Run!.Starts.Should().OnlyContain(s => s.Phase1.Outcome != FtmoPhaseOutcome.BreachedFirst);
        calm.Diagnostics!.Attribution.DecidingBreachStarts.Should().Be(0);
    }

    [Fact]
    public void ComputeGroup_ATwoMemberTieAtTheBreachInstant_CreditsBothThroughTheEvaluator()
    {
        var a = Ok(IdA, 0, Trade(0, At(12, 7, 30), At(12, 7, 45)), Trade(1, At(13, 8), At(13, 9), -600m), Trade(2, At(15, 8), At(15, 9)));
        var b = Ok(IdB, 1, Trade(0, At(12, 7), At(12, 7, 15)), Trade(1, At(13, 8, 30), At(13, 9)), Trade(2, At(16, 8), At(16, 9)));

        var result = Compute(a, b);

        result.Run!.Starts.Should().ContainSingle().Which.Phase1.OutcomeSourceClose.Should().Be(At(13, 9));
        var attribution = result.Diagnostics!.Attribution;
        attribution.SharedCloseStarts.Should().Be(1);
        attribution.DecidingBreachStarts.Should().Be(1);
        attribution.Members.Should().OnlyContain(m => m.Phase1Starts == 1 && m.SharedCloseStarts == 1 && m.SoleContributorStarts == 0);
    }

    [Fact]
    public void ComputeGroup_ABreachInTheFundedPhase_IsAttributedForTheFundedPhaseThroughTheEvaluator()
    {
        // Phase 1: +10% over four Berlin days (T1 = Jan 15 09:00). Phase 2: +6% over four more days (T2 = Jan 19 09:00).
        // Funded: A loses 600 (> 5% daily) on Jan 20. Window end is B's Jan 21 close, so A's Jan 22 row is trimmed.
        var a = Ok(
            IdA, 0,
            Trade(0, At(12, 8), At(12, 9), 300m), Trade(1, At(14, 8), At(14, 9), 300m), Trade(2, At(16, 8), At(16, 9), 150m),
            Trade(3, At(18, 8), At(18, 9), 150m), Trade(4, At(20, 8), At(20, 9), -600m), Trade(5, At(22, 8), At(22, 9), 10m));
        var b = Ok(
            IdB, 1,
            Trade(0, At(12, 8), At(12, 8, 30), 10m), Trade(1, At(13, 8), At(13, 9), 300m), Trade(2, At(15, 8), At(15, 9), 300m),
            Trade(3, At(17, 8), At(17, 9), 150m), Trade(4, At(19, 8), At(19, 9), 150m), Trade(5, At(21, 8), At(21, 9), 10m));

        var result = Compute(a, b);

        var start = result.Run!.Starts.Should().ContainSingle().Subject;
        start.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        start.Phase2.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        start.Funded.Outcome.Should().Be(FtmoFundedOutcome.BreachedFirst);
        start.Funded.OutcomeSourceClose.Should().Be(At(20, 9));
        var attribution = result.Diagnostics!.Attribution;
        attribution.Members.Single(m => m.StrategyId == IdA).Should().Be(new FtmoGroupMemberAttributionDto(IdA, "member-0", 0, 0, 1, 1, 0));
        attribution.Members.Single(m => m.StrategyId == IdB).FundedStarts.Should().Be(0);
    }
}
