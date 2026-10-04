using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.2 (design.md D4) — the pure per-kind group computation. Every member is given
/// the per-run <c>RowIndex</c> 0..n-1 its own run would have, so the collisions the merger must resolve are
/// present in every fixture. The shipped engine (<c>ComputeRun</c> and below) runs UNEDITED.
/// </summary>
public class FtmoGroupComputationTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid IdC = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    // Outside any DST-mismatch window for Jerusalem/Berlin.
    private static DateTime At(int day, int hour, int minute = 0) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(int row, DateTime open, DateTime close, decimal? net = 10m) =>
        new(row, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, net is null ? 0m : 1m);

    private static FtmoSimulationInputs.RunProjection Projection(
        BacktestSegment segment, IReadOnlyList<ProjectedTrade> low, IReadOnlyList<ProjectedTrade> high) =>
        new(null, segment, [.. low], [.. high], 0, 0, low.Count(t => t.Outcome == ResizeOutcome.Unscalable));

    /// <summary>A member whose low and high FX ends are identical (a USD member).</summary>
    private static GroupMemberKindInput Ok(
        Guid id, int order, BacktestSegment segment, params ProjectedTrade[] trades) =>
        new(id, $"member-{order}", order, Guid.NewGuid(), null, Projection(segment, trades, trades));

    private static GroupMemberKindInput Ok(Guid id, int order, params ProjectedTrade[] trades) =>
        Ok(id, order, BacktestSegment.InSample, trades);

    private static GroupMemberKindInput Missing(Guid id, int order) =>
        new(id, $"member-{order}", order, RunId: null, Refusal: null, Projection: null);

    private static GroupMemberKindInput Refused(Guid id, int order, FtmoSimulationRefusal reason) =>
        new(id, $"member-{order}", order, Guid.NewGuid(), reason, Projection: null);

    private static readonly FtmoChallengeRulesDto Rules = new(0.10m, 0.05m, 4, TimeLimitDays: null);

    private static GroupParams Params(decimal? profitTargetPct = null, decimal maxPct = 0.10m) =>
        new(Jerusalem, Berlin, 10_000m, 0.05m, maxPct, profitTargetPct, (1m, 1m), Rules);

    private static FtmoGroupKindResultDto Compute(BacktestRunKind kind, GroupParams p, params GroupMemberKindInput[] members) =>
        ComputeGroup(kind, members, p, CancellationToken.None);

    private static FtmoGroupKindResultDto Compute(BacktestRunKind kind, params GroupMemberKindInput[] members) =>
        Compute(kind, Params(), members);

    /// <summary>Two members whose ranges overlap on Jan 12-16, each with per-run RowIndex 0..2.</summary>
    private static (GroupMemberKindInput A, GroupMemberKindInput B) OverlappingPair() => (
        Ok(IdA, 0, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(13, 8), At(13, 9)), Trade(2, At(15, 8), At(15, 9))),
        Ok(IdB, 1, Trade(0, At(12, 10), At(12, 11)), Trade(1, At(14, 8), At(14, 9)), Trade(2, At(16, 8), At(16, 9))));

    /// <summary>Two members whose ranges do NOT overlap (A ends before B starts).</summary>
    private static (GroupMemberKindInput A, GroupMemberKindInput B) DisjointPair() => (
        Ok(IdA, 0, Trade(0, At(2, 8), At(2, 9)), Trade(1, At(5, 8), At(5, 9))),
        Ok(IdB, 1, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(14, 8), At(14, 9))));

    // ---- B2.2.1: kinds are paired strictly ----

    [Fact]
    public void ComputeGroup_OneMemberMissingTheKind_RefusesOnlyThatKindListingTheMember()
    {
        var (a, b) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, a, Missing(IdB, 1));
        var evaluation = Compute(BacktestRunKind.Evaluation, a, b);

        deploy.Status.Should().Be(FtmoSimulationStatus.Refused);
        deploy.Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
        deploy.MemberRefusals.Should().ContainSingle()
            .Which.Should().Be(new FtmoGroupMemberRefusalDto(IdB, "member-1", FtmoGroupRefusal.MemberMissingKind, null));
        evaluation.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        evaluation.Run.Should().NotBeNull();
    }

    [Fact]
    public void ComputeGroup_TwoOfThreeMembersMissingTheKind_ListsEveryMissingMember()
    {
        var (a, _) = OverlappingPair();

        var result = Compute(BacktestRunKind.Deploy, a, Missing(IdB, 1), Missing(IdC, 2));

        result.Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
        result.MemberRefusals.Select(r => r.StrategyId).Should().Equal(IdB, IdC);
    }

    [Fact]
    public void ComputeGroup_AMemberWithNoRunsAtAll_RefusesBothKinds()
    {
        var (a, _) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, a, Missing(IdB, 1));
        var evaluation = Compute(BacktestRunKind.Evaluation, a, Missing(IdB, 1));

        deploy.Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
        evaluation.Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
        deploy.MemberRefusals.Single().StrategyId.Should().Be(IdB);
        evaluation.MemberRefusals.Single().StrategyId.Should().Be(IdB);
    }

    // ---- B2.2.2: member-level refusals ----

    [Fact]
    public void ComputeGroup_SeveralFailingMembers_AreAllListedWithTheirOwnReasonAndNoneIsDropped()
    {
        var (a, _) = OverlappingPair();

        var result = Compute(
            BacktestRunKind.Deploy,
            a,
            Refused(IdB, 1, FtmoSimulationRefusal.PointValueNotCalibrated),
            Refused(IdC, 2, FtmoSimulationRefusal.RiskNotEstimable));

        result.MemberRefusals.Should().Equal(
            new FtmoGroupMemberRefusalDto(IdB, "member-1", FtmoGroupRefusal.MemberRunRefused, FtmoSimulationRefusal.PointValueNotCalibrated),
            new FtmoGroupMemberRefusalDto(IdC, "member-2", FtmoGroupRefusal.MemberRunRefused, FtmoSimulationRefusal.RiskNotEstimable));
    }

    [Fact]
    public void ComputeGroup_RiskNotEstimableInOneKind_RefusesOnlyThatKind()
    {
        var (a, b) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, a, Refused(IdB, 1, FtmoSimulationRefusal.RiskNotEstimable));
        var evaluation = Compute(BacktestRunKind.Evaluation, a, b);

        deploy.Status.Should().Be(FtmoSimulationStatus.Refused);
        deploy.MemberRefusals.Single().RunReason.Should().Be(FtmoSimulationRefusal.RiskNotEstimable);
        evaluation.Status.Should().Be(FtmoSimulationStatus.Evaluated);
    }

    [Theory]
    [InlineData(FtmoSimulationRefusal.InstrumentSpecMissing)]
    [InlineData(FtmoSimulationRefusal.PointValueNotCalibrated)]
    [InlineData(FtmoSimulationRefusal.FxRateNotDeclared)]
    [InlineData(FtmoSimulationRefusal.InvalidFxBand)]
    public void ComputeGroup_ASymbolLevelReasonOnBothKinds_RefusesBothKindsListingTheMember(FtmoSimulationRefusal reason)
    {
        var (a, _) = OverlappingPair();

        foreach (var kind in new[] { BacktestRunKind.Deploy, BacktestRunKind.Evaluation })
        {
            var result = Compute(kind, a, Refused(IdB, 1, reason));

            result.Status.Should().Be(FtmoSimulationStatus.Refused);
            result.Refusal.Should().Be(FtmoGroupRefusal.MemberRunRefused);
            result.MemberRefusals.Should().ContainSingle()
                .Which.Should().Be(new FtmoGroupMemberRefusalDto(IdB, "member-1", FtmoGroupRefusal.MemberRunRefused, reason));
        }
    }

    // ---- B2.2.3: mixed causes ----

    [Fact]
    public void ComputeGroup_MixedCauses_CarryMemberRunRefusedWithEachMembersOwnReason()
    {
        var (a, _) = OverlappingPair();

        var result = Compute(
            BacktestRunKind.Deploy, a, Missing(IdB, 1), Refused(IdC, 2, FtmoSimulationRefusal.RiskNotEstimable));

        result.Refusal.Should().Be(FtmoGroupRefusal.MemberRunRefused);
        result.MemberRefusals.Should().Equal(
            new FtmoGroupMemberRefusalDto(IdB, "member-1", FtmoGroupRefusal.MemberMissingKind, null),
            new FtmoGroupMemberRefusalDto(IdC, "member-2", FtmoGroupRefusal.MemberRunRefused, FtmoSimulationRefusal.RiskNotEstimable));
    }

    [Fact]
    public void ComputeGroup_AllFailingMembersShareOneMissingCause_CarryThatCause()
    {
        var (a, _) = OverlappingPair();

        Compute(BacktestRunKind.Deploy, a, Missing(IdB, 1), Missing(IdC, 2))
            .Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
    }

    [Fact]
    public void ComputeGroup_AllFailingMembersShareTheRunRefusedCause_CarryThatCause()
    {
        var (a, _) = OverlappingPair();

        Compute(
            BacktestRunKind.Deploy, a,
            Refused(IdB, 1, FtmoSimulationRefusal.RiskNotEstimable),
            Refused(IdC, 2, FtmoSimulationRefusal.InstrumentSpecMissing))
            .Refusal.Should().Be(FtmoGroupRefusal.MemberRunRefused);
    }

    // ---- B2.2.4: window, per kind ----

    [Fact]
    public void ComputeGroup_DeployRangesDisjointAndEvaluationRangesOverlapping_RefusesOnlyDeployWithCoverage()
    {
        var (da, db) = DisjointPair();
        var (ea, eb) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, da, db);
        var evaluation = Compute(BacktestRunKind.Evaluation, ea, eb);

        deploy.Status.Should().Be(FtmoSimulationStatus.Refused);
        deploy.Refusal.Should().Be(FtmoGroupRefusal.NoCommonWindow);
        deploy.MemberRefusals.Should().BeEmpty("NoCommonWindow blames no member");
        deploy.Window.Should().BeNull();
        deploy.Run.Should().BeNull();
        deploy.Coverage.Should().HaveCount(2);
        deploy.Coverage[0].Should().Be(new FtmoGroupMemberCoverageDto(IdA, "member-0", At(2, 8), At(5, 9), 0));
        deploy.Coverage[1].Should().Be(new FtmoGroupMemberCoverageDto(IdB, "member-1", At(12, 8), At(14, 9), 0));
        evaluation.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        evaluation.Run.Should().NotBeNull();
    }

    [Fact]
    public void ComputeGroup_DeployOverlappingAndEvaluationDisjoint_RefusesOnlyEvaluation()
    {
        var (da, db) = OverlappingPair();
        var (ea, eb) = DisjointPair();

        var deploy = Compute(BacktestRunKind.Deploy, da, db);
        var evaluation = Compute(BacktestRunKind.Evaluation, ea, eb);

        deploy.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        evaluation.Refusal.Should().Be(FtmoGroupRefusal.NoCommonWindow);
        evaluation.Kind.Should().Be(BacktestRunKind.Evaluation);
    }

    /// <summary>A's single long trade opens before the window and closes after it; B defines the window.</summary>
    private static (GroupMemberKindInput A, GroupMemberKindInput B) OneMemberEmptiedByTheTrimPair() => (
        Ok(IdA, 0, Trade(0, At(2, 8), At(28, 9))),
        Ok(IdB, 1, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(14, 8), At(14, 9))));

    [Fact]
    public void ComputeGroup_ANonEmptyWindowWithAMemberHavingNoTradesInIt_RefusesThatKindListingTheMember()
    {
        var (a, b) = OneMemberEmptiedByTheTrimPair();
        var (oa, ob) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, a, b);
        var evaluation = Compute(BacktestRunKind.Evaluation, oa, ob);

        deploy.Status.Should().Be(FtmoSimulationStatus.Refused);
        deploy.Refusal.Should().Be(FtmoGroupRefusal.MemberHasNoTradesInWindow);
        deploy.MemberRefusals.Should().ContainSingle()
            .Which.Should().Be(new FtmoGroupMemberRefusalDto(IdA, "member-0", FtmoGroupRefusal.MemberHasNoTradesInWindow, null));
        deploy.Window.Should().Be(new FtmoGroupWindowDto(At(12, 8), At(14, 9)));
        deploy.Run.Should().BeNull();
        evaluation.Status.Should().Be(FtmoSimulationStatus.Evaluated, "the other kind is independent");
    }

    // ---- B2.2.5: result shape ----

    [Fact]
    public void ComputeGroup_ARefusedKind_CarriesNoRun()
    {
        var (a, _) = OverlappingPair();

        var result = Compute(BacktestRunKind.Deploy, a, Missing(IdB, 1));

        result.Run.Should().BeNull();
        result.Window.Should().BeNull();
        result.Coverage.Should().BeEmpty();
    }

    [Fact]
    public void ComputeGroup_ASuccessfulKind_EchoesTheWindowAndOneCoverageEntryPerMember()
    {
        var (a, b) = OverlappingPair();

        var result = Compute(BacktestRunKind.Deploy, a, b);

        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        result.Refusal.Should().BeNull();
        result.MemberRefusals.Should().BeEmpty();
        result.Window.Should().Be(new FtmoGroupWindowDto(At(12, 10), At(15, 9)));
        result.Coverage.Should().Equal(
            new FtmoGroupMemberCoverageDto(IdA, "member-0", At(12, 8), At(15, 9), 2),
            new FtmoGroupMemberCoverageDto(IdB, "member-1", At(12, 10), At(16, 9), 2));
    }

    [Fact]
    public void ComputeGroup_MembersOfTheSameSegment_EchoThatSegment()
    {
        var (a, b) = OverlappingPair();

        Compute(BacktestRunKind.Deploy, a, b).Run!.Segment.Should().Be(BacktestSegment.InSample);
    }

    [Fact]
    public void ComputeGroup_MembersOfDifferentSegments_EchoUnknown()
    {
        var a = Ok(
            IdA, 0, BacktestSegment.InSample,
            Trade(0, At(12, 8), At(12, 9)), Trade(1, At(12, 12), At(12, 13)), Trade(2, At(14, 8), At(14, 9)));
        var b = Ok(IdB, 1, BacktestSegment.OutOfSample, Trade(0, At(12, 10), At(12, 11)), Trade(1, At(13, 8), At(13, 9)));

        Compute(BacktestRunKind.Deploy, a, b).Run!.Segment.Should().Be(BacktestSegment.Unknown);
    }

    [Fact]
    public void ComputeGroup_UnscalableCount_IsTheInWindowSumNotTheFullRunCount()
    {
        // B defines the window [Jan 14 08:00, Jan 16 09:00]. A has one Unscalable row BEFORE it (dropped)
        // and one INSIDE it: the full-run count is 2, the in-window count is 1.
        var a = Ok(
            IdA, 0,
            Trade(0, At(12, 8), At(12, 9), net: null),
            Trade(1, At(14, 10), At(14, 11)),
            Trade(2, At(15, 8), At(15, 9), net: null),
            Trade(3, At(16, 7), At(16, 8)));
        var b = Ok(IdB, 1, Trade(0, At(14, 8), At(14, 9)), Trade(1, At(16, 8), At(16, 9)));

        var result = Compute(BacktestRunKind.Deploy, a, b);

        a.Projection!.UnscalableCount.Should().Be(2);
        result.Run!.UnscalableCount.Should().Be(1);
    }

    [Fact]
    public void ComputeGroup_AStoredProfitTargetOtherThanTenPercent_PassesTheMismatchThroughUnchanged()
    {
        var (a, b) = OverlappingPair();

        var result = Compute(BacktestRunKind.Deploy, Params(profitTargetPct: 0.08m), a, b);

        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        result.Run!.RaceRefusal.Should().Be(FtmoChallengeRaceRefusal.ProfitTargetMismatch);
        result.Run.StoredProfitTargetPct.Should().Be(0.08m);
        result.Run.Starts.Should().BeEmpty();
    }

    [Fact]
    public void ComputeGroup_TheRunHasAnEmptyRunIdAndItsOwnKind_SoDeployAndEvaluationNeverShareAFigure()
    {
        var (a, b) = OverlappingPair();

        var deploy = Compute(BacktestRunKind.Deploy, a, b);
        var evaluation = Compute(BacktestRunKind.Evaluation, a, b);

        deploy.Run!.RunId.Should().Be(Guid.Empty);
        evaluation.Run!.RunId.Should().Be(Guid.Empty);
        deploy.Kind.Should().Be(BacktestRunKind.Deploy);
        deploy.Run.Kind.Should().Be(BacktestRunKind.Deploy);
        evaluation.Kind.Should().Be(BacktestRunKind.Evaluation);
        evaluation.Run.Kind.Should().Be(BacktestRunKind.Evaluation);
        deploy.Run.Disclosure.Should().Be(FtmoMultiStartReadService.Disclosure);
    }

    [Fact]
    public void ComputeGroup_ACancelledToken_ThrowsBeforeComputing()
    {
        var (a, b) = OverlappingPair();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => ComputeGroup(BacktestRunKind.Deploy, [a, b], Params(), cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    // ---- B2.2.6: cross-member behaviour through the UNEDITED evaluator ----

    [Fact]
    public void ComputeGroup_AMemberOpenAcrossAnotherMembersBreachingClose_IsAContingentBreach()
    {
        // A's row 1 loses 600 (> 5% daily) closing at 12:00; B's row 1 (the SAME per-run RowIndex 1) is open
        // 11:00-13:00 across it and recovers. Rows 0 and 2 only widen each member's range so the breaching
        // rows survive the window trim. Without a global renumbering the evaluator's "self" skip would hide
        // B's row from A's close and report a clean breach.
        var a = Ok(
            IdA, 0, Trade(0, At(12, 9), At(12, 9, 30)), Trade(1, At(12, 10), At(12, 12), net: -600m), Trade(2, At(12, 14), At(12, 15)));
        var b = Ok(
            IdB, 1, Trade(0, At(12, 9, 15), At(12, 9, 45)), Trade(1, At(12, 11), At(12, 13), net: 600m), Trade(2, At(12, 15), At(12, 16)));

        var result = Compute(BacktestRunKind.Deploy, Params(maxPct: 0.30m), a, b);

        var start = result.Run!.Starts.Should().ContainSingle().Subject;
        start.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        start.Phase1.BreachPointClass.Should().Be(FtmoBreachPointClass.Contingent);
    }

    [Fact]
    public void ComputeGroup_DuplicatePerRunIndicesAcrossMembers_CountEveryMembersTradingDays()
    {
        // The window is [Jan 13 08:00, Jan 16 13:00]. In it A trades on days 14 and 16 (per-run RowIndex 1, 2)
        // and B on days 13 and 15 (RowIndex 0, 1): four +300 trades on four distinct Berlin days reach the +10%
        // target with the 4-day minimum met (a later +10 row leaves phase 2 something to replay). A.1 and B.1 collide on RowIndex: a merge that kept the per-run
        // indices would let AttributeOpenDays overwrite one of the days and read the minimum as unmet.
        var a = Ok(
            IdA, 0,
            Trade(0, At(12, 8), At(12, 9), net: 300m), Trade(1, At(14, 8), At(14, 9), net: 300m), Trade(2, At(16, 8), At(16, 9), net: 300m),
            Trade(3, At(16, 12), At(16, 13), net: 10m));
        var b = Ok(
            IdB, 1,
            Trade(0, At(13, 8), At(13, 9), net: 300m), Trade(1, At(15, 8), At(15, 9), net: 300m), Trade(2, At(17, 8), At(17, 9), net: 300m));

        var result = Compute(BacktestRunKind.Deploy, Params(maxPct: 0.30m), a, b);

        result.Window.Should().Be(new FtmoGroupWindowDto(At(13, 8), At(16, 13)));
        var start = result.Run!.Starts.First();
        start.Phase1.Outcome.Should().Be(FtmoPhaseOutcome.TargetReachedFirst);
        start.Phase1.MinTradingDaysMetFtmoDay.Should().NotBeNull();
    }

    [Fact]
    public void ComputeGroup_AUsdMemberAndAnEurMemberWhoseBandFlipsAStart_FlagsFxRoundingSensitive()
    {
        // USD member: low == high. EUR member: -300 at the low end (3% < 5% daily), -600 at the high end
        // (6% > 5%), so the start flips across the band.
        var usdTrades = new[] { Trade(0, At(12, 8), At(12, 9), net: 10m), Trade(1, At(13, 8), At(13, 9), net: 10m) };
        var usd = Ok(IdA, 0, usdTrades);
        var eurLow = new[] { Trade(0, At(12, 10), At(12, 11), net: -300m), Trade(1, At(13, 10), At(13, 11), net: 10m) };
        var eurHigh = new[] { Trade(0, At(12, 10), At(12, 11), net: -600m), Trade(1, At(13, 10), At(13, 11), net: 10m) };
        var eur = new GroupMemberKindInput(
            IdB, "member-1", 1, Guid.NewGuid(), null, Projection(BacktestSegment.InSample, eurLow, eurHigh));

        var result = Compute(BacktestRunKind.Deploy, usd, eur);

        result.Run!.Starts.First().FxRoundingSensitive.Should().BeTrue();
        result.Run.Summary!.FxRoundingSensitiveCount.Should().BeGreaterThan(0);
    }
}
