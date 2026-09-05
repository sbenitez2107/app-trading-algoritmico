using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Run selection (design.md D8a/D8b) as a PURE function over the two rows a strategy can have.
/// <para>
/// It is tested here rather than through the read service because the projection and the rule are
/// deliberately separate: <c>BacktestRunConfiguration</c> makes <c>(StrategyId, Kind)</c> unique, so
/// the decision is a choice among at most two rows and needs no database to state. The single-query
/// property of the projection itself is fenced separately, on real SQLite, in
/// <c>BacktestRunSegmentQueryCostTests</c>.
/// </para>
/// </summary>
public class BacktestRunSelectionTests
{
    private static readonly Guid StrategyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static BacktestRunSegmentRow Row(BacktestRunKind kind, BacktestSegment? min, BacktestSegment? max = null)
        => new(
            RunId: Guid.NewGuid(),
            StrategyId: StrategyId,
            Kind: kind,
            MinSegment: min is null ? null : (int)min.Value,
            MaxSegment: max is null ? (min is null ? null : (int)min.Value) : (int)max.Value);

    // ---------------------------------------------------------------------
    // 3.3 - deriving a run's segment from its own trades.
    // ---------------------------------------------------------------------

    [Fact]
    public void SegmentRow_WithNoTrades_YieldsNoSegmentAndIsNeverCoercedToUnknown()
    {
        var row = Row(BacktestRunKind.Deploy, min: null);

        row.State.Should().Be(BacktestRunSegmentState.NoTrades);
        row.Segment.Should().BeNull(
            "a run ROW is not evidence - its trades are; a trade-less run has NO segment, and "
            + "Unknown is a meaningful enum member the parser assigns, never a stand-in for absence");
        row.Segment.Should().NotBe(BacktestSegment.Unknown);
    }

    [Fact]
    public void SegmentRow_WhoseTradesDisagree_IsRefusedRatherThanResolvedToEither()
    {
        var row = Row(BacktestRunKind.Evaluation, BacktestSegment.InSample, BacktestSegment.OutOfSample);

        row.State.Should().Be(BacktestRunSegmentState.Disagreeing);
        row.Segment.Should().BeNull();
    }

    [Fact]
    public void SegmentRow_WhoseTradesAgree_ResolvesToThatSegment()
    {
        Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest).Segment
            .Should().Be(BacktestSegment.InSampleTest);
    }

    [Fact]
    public void Select_WithADisagreeingRun_RefusesNamingTheRun()
    {
        var disagreeing = Row(BacktestRunKind.Evaluation, BacktestSegment.InSample, BacktestSegment.OutOfSample);
        var healthy = Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest);

        var result = RunSegmentSelection.Select(
            [disagreeing, healthy], BacktestSegment.InSampleTest, runKind: null);

        result.Status.Should().Be(
            GroupRiskMemberStatus.RunSegmentsDisagree,
            "the parser rejects a file carrying two sample types, so a run that holds two is a "
            + "hand-edited database - an invariant this design DEPENDS on, checked rather than assumed");
        result.DisagreeingRunIds.Should().ContainSingle().Which.Should().Be(disagreeing.RunId);
        result.Run.Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // 3.5 / the trade-less run is NON-FATAL to the member.
    // ---------------------------------------------------------------------

    [Fact]
    public void Select_WithNoRunCarryingTheRequestedSegment_IsTheExplicitNoEvidenceState()
    {
        var result = RunSegmentSelection.Select(
            [Row(BacktestRunKind.Deploy, BacktestSegment.InSample)],
            BacktestSegment.OutOfSample,
            runKind: null);

        result.Status.Should().Be(GroupRiskMemberStatus.NoEvidenceForSegment);
        result.Run.Should().BeNull("no evidence is a STATE, not an empty series");
    }

    [Fact]
    public void Select_WithOneTradelessRunAndOneMatchingRun_ResolvesFromTheMatchingRun()
    {
        var tradeless = Row(BacktestRunKind.Deploy, min: null);
        var matching = Row(BacktestRunKind.Evaluation, BacktestSegment.InSampleTest);

        var result = RunSegmentSelection.Select(
            [tradeless, matching], BacktestSegment.InSampleTest, runKind: null);

        result.Status.Should().Be(
            GroupRiskMemberStatus.Resolved,
            "a half-populated strategy - one slot imported, the other not yet - is the NORMAL "
            + "intermediate state of the two-row constraint, not a reason to refuse the member");
        result.Run!.RunId.Should().Be(matching.RunId);
    }

    // ---------------------------------------------------------------------
    // 3.6 - the anti-shortcut row. Kind and Segment are different axes.
    // ---------------------------------------------------------------------

    [Fact]
    public void Select_DeployRunIsInSampleTestAndEvaluationRunIsOutOfSample_PicksDeployForInSampleTest()
    {
        var deploy = Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest);
        var evaluation = Row(BacktestRunKind.Evaluation, BacktestSegment.OutOfSample);

        var result = RunSegmentSelection.Select(
            [deploy, evaluation], BacktestSegment.InSampleTest, runKind: null);

        result.Status.Should().Be(GroupRiskMemberStatus.Resolved);
        result.Run!.Kind.Should().Be(
            BacktestRunKind.Deploy,
            "this is the AlgoWizard full-period export and the committed IST fixture: a Deploy run "
            + "whose trades are InSampleTest. Anything that maps Kind to Segment gets this wrong");
    }

    [Fact]
    public void Select_DeployRunIsInSampleTestAndEvaluationRunIsOutOfSample_PicksEvaluationForOutOfSample()
    {
        var deploy = Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest);
        var evaluation = Row(BacktestRunKind.Evaluation, BacktestSegment.OutOfSample);

        var result = RunSegmentSelection.Select(
            [deploy, evaluation], BacktestSegment.OutOfSample, runKind: null);

        result.Run!.Kind.Should().Be(BacktestRunKind.Evaluation);
    }

    // ---------------------------------------------------------------------
    // 3.7 - both runs match: refused, not preferred.
    // ---------------------------------------------------------------------

    [Fact]
    public void Select_WhenBothRunsCarryTheRequestedSegment_IsRefusedNamingBothKinds()
    {
        var deploy = Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest);
        var evaluation = Row(BacktestRunKind.Evaluation, BacktestSegment.InSampleTest);

        var result = RunSegmentSelection.Select(
            [deploy, evaluation], BacktestSegment.InSampleTest, runKind: null);

        result.Status.Should().Be(
            GroupRiskMemberStatus.AmbiguousRunSelection,
            "two runs carrying the same segment are two PARAMETER SETS over the same sample; "
            + "picking either makes the published figure depend on an arbitrary choice, and "
            + "preferring Evaluation would be exactly the Kind-to-Segment inference that is ruled out");
        result.Run.Should().BeNull();
        result.CandidateKinds.Should().BeEquivalentTo([BacktestRunKind.Deploy, BacktestRunKind.Evaluation]);
    }

    [Theory]
    [InlineData(BacktestRunKind.Deploy)]
    [InlineData(BacktestRunKind.Evaluation)]
    public void Select_WhenBothRunsMatchAndARunKindIsSupplied_TheDisambiguatorResolvesIt(BacktestRunKind chosen)
    {
        var deploy = Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest);
        var evaluation = Row(BacktestRunKind.Evaluation, BacktestSegment.InSampleTest);

        var result = RunSegmentSelection.Select(
            [deploy, evaluation], BacktestSegment.InSampleTest, runKind: chosen);

        result.Status.Should().Be(GroupRiskMemberStatus.Resolved);
        result.Run!.Kind.Should().Be(chosen);
    }

    [Fact]
    public void Select_WithARunKindThatMatchesNoRunOfTheRequestedSegment_IsNoEvidenceNotAmbiguity()
    {
        var result = RunSegmentSelection.Select(
            [Row(BacktestRunKind.Deploy, BacktestSegment.InSampleTest)],
            BacktestSegment.InSampleTest,
            runKind: BacktestRunKind.Evaluation);

        result.Status.Should().Be(GroupRiskMemberStatus.NoEvidenceForSegment);
    }

    // ---------------------------------------------------------------------
    // 3.4 - a run genuinely labelled Unknown is never selected. This is the
    // RUN-side rule; the REQUEST-side refusal of Unknown is a different rule
    // with different reasoning and is asserted on the read service.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(BacktestSegment.Unknown)]
    [InlineData(BacktestSegment.InSample)]
    [InlineData(BacktestSegment.OutOfSample)]
    [InlineData(BacktestSegment.InSampleTest)]
    public void Select_ARunWhoseTradesAreUnknown_IsNeverSelectedForAnyRequestedSegment(BacktestSegment requested)
    {
        var unknownRun = Row(BacktestRunKind.Deploy, BacktestSegment.Unknown);

        var result = RunSegmentSelection.Select([unknownRun], requested, runKind: null);

        result.Run.Should().BeNull(
            "an Unknown run carries a label the parser could not classify; publishing a figure over "
            + "it asserts something the data does not support - including when Unknown is what was asked for");
        result.Status.Should().Be(GroupRiskMemberStatus.NoEvidenceForSegment);
    }

    // ---------------------------------------------------------------------
    // 3.8 - the group-level disagreement rule, as a pure predicate.
    // ---------------------------------------------------------------------

    [Fact]
    public void DisagreeingSegments_WhenMembersCarryDifferentSegments_NamesEveryOneOfThem()
    {
        var disagreeing = RunSegmentSelection.DisagreeingSegments(
        [
            ("Alpha", BacktestSegment.InSampleTest),
            ("Beta", BacktestSegment.OutOfSample),
        ]);

        disagreeing.Should().HaveCount(2);
        disagreeing.Should().BeEquivalentTo(
            new[] { ("Alpha", BacktestSegment.InSampleTest), ("Beta", BacktestSegment.OutOfSample) },
            "a correlation or VaR figure implies ONE sample label, so the refusal has to say WHO "
            + "disagreed and with what - not merely that someone did");
    }

    [Fact]
    public void DisagreeingSegments_WhenEveryMemberAgrees_IsEmpty()
    {
        RunSegmentSelection.DisagreeingSegments(
        [
            ("Alpha", BacktestSegment.InSampleTest),
            ("Beta", BacktestSegment.InSampleTest),
        ]).Should().BeEmpty();
    }
}
