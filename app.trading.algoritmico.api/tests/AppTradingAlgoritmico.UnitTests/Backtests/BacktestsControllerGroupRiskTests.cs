using Microsoft.AspNetCore.Http;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Task 3.12 — the read endpoint's status mapping. Every refusal this slice can produce carries a
/// DISTINCT status, because collapsing them would make "we cannot tell which sample this is" and
/// "this member's weight would double-size it" read identically to the operator.
/// </summary>
public class BacktestsControllerGroupRiskTests
{
    private static readonly Guid StrategyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static GroupRiskAnalysisRequest Request(BacktestSegment? segment = BacktestSegment.InSampleTest)
        => new(
            StrategyIds: [StrategyId],
            InitialCapital: 10_000m,
            TargetRiskPerTrade: 199.98m,
            Segment: segment);

    private static GroupRiskAnalysisDto Analysis(
        GroupRiskAnalysisStatus status,
        GroupRiskMemberStatus memberStatus = GroupRiskMemberStatus.Resolved,
        string? refusal = null)
        => new(
            Status: status,
            Segment: BacktestSegment.InSampleTest,
            Members:
            [
                new GroupRiskMemberDto(
                    StrategyId, "Alpha", memberStatus, BacktestSegment.InSampleTest,
                    BacktestRunKind.Deploy, Guid.NewGuid(), refusal),
            ],
            Risk: null,
            Correlation: null,
            Refusal: refusal);

    private static (BacktestsController Controller, Mock<IBacktestReadService> Service) CreateSut(
        GroupRiskAnalysisDto analysis)
    {
        var mock = new Mock<IBacktestReadService>();
        mock.Setup(s => s.GetGroupRiskAnalysisAsync(
                It.IsAny<GroupRiskAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        return (new BacktestsController(mock.Object), mock);
    }

    [Fact]
    public async Task GetPortfolioRisk_WhenCompleted_Is200AndForwardsTheRequestUnchanged()
    {
        var (controller, service) = CreateSut(Analysis(GroupRiskAnalysisStatus.Completed));
        var request = Request();

        var result = await controller.GetPortfolioRisk(request);

        result.Result.Should().BeOfType<OkObjectResult>();
        service.Verify(
            s => s.GetGroupRiskAnalysisAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The INTENDED code for every status, written out once. It is the test's own statement of the
    /// contract, deliberately independent of the controller's switch: a mapping table derived from
    /// the code under test could only ever agree with it.
    /// </summary>
    private static readonly Dictionary<GroupRiskAnalysisStatus, int> ExpectedCodes = new()
    {
        [GroupRiskAnalysisStatus.Completed] = StatusCodes.Status200OK,
        [GroupRiskAnalysisStatus.SegmentNotSpecified] = StatusCodes.Status400BadRequest,
        [GroupRiskAnalysisStatus.UnknownSegmentNotSelectable] = StatusCodes.Status400BadRequest,
        [GroupRiskAnalysisStatus.NoStrategiesRequested] = StatusCodes.Status400BadRequest,
        [GroupRiskAnalysisStatus.InvalidLotGrid] = StatusCodes.Status400BadRequest,
        [GroupRiskAnalysisStatus.InvalidInitialCapital] = StatusCodes.Status400BadRequest,
        [GroupRiskAnalysisStatus.StrategyNotFound] = StatusCodes.Status404NotFound,
        [GroupRiskAnalysisStatus.RunSegmentsDisagree] = StatusCodes.Status422UnprocessableEntity,
        [GroupRiskAnalysisStatus.NoEvidenceForSegment] = StatusCodes.Status422UnprocessableEntity,
        [GroupRiskAnalysisStatus.AmbiguousRunSelection] = StatusCodes.Status422UnprocessableEntity,
        [GroupRiskAnalysisStatus.RiskNotEstimable] = StatusCodes.Status422UnprocessableEntity,
        [GroupRiskAnalysisStatus.NonUnitWeight] = StatusCodes.Status422UnprocessableEntity,
        [GroupRiskAnalysisStatus.HeterogeneousGroup] = StatusCodes.Status422UnprocessableEntity,
    };

    public static TheoryData<GroupRiskAnalysisStatus, int> RefusalMappings()
    {
        var data = new TheoryData<GroupRiskAnalysisStatus, int>();
        foreach (var (status, code) in ExpectedCodes)
            if (status != GroupRiskAnalysisStatus.Completed)
                data.Add(status, code);
        return data;
    }

    [Theory]
    [MemberData(nameof(RefusalMappings))]
    public async Task GetPortfolioRisk_MapsEachRefusalToItsOwnStatusCodeAndStillReturnsTheEvidence(
        GroupRiskAnalysisStatus status, int expectedCode)
    {
        var (controller, _) = CreateSut(Analysis(status));

        var result = await controller.GetPortfolioRisk(Request());

        var objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedCode);
        objectResult.Value.Should().BeOfType<GroupRiskAnalysisDto>(
            "a refusal still carries the per-member evidence that produced it — the operator needs "
            + "to know WHICH member and WHY, not merely that something failed");
    }

    [Fact]
    public async Task GetPortfolioRisk_OnANonUnitWeight_Is422AndTheBodyNamesTheMember()
    {
        var (controller, _) = CreateSut(Analysis(
            GroupRiskAnalysisStatus.NonUnitWeight,
            GroupRiskMemberStatus.NonUnitWeight,
            refusal: "Alpha carries weight 1.5"));

        var result = await controller.GetPortfolioRisk(Request());

        var objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var body = objectResult.Value.Should().BeOfType<GroupRiskAnalysisDto>().Subject;
        body.Members.Should().ContainSingle().Which.Label.Should().Be("Alpha");
        body.Refusal.Should().Contain("1.5");
    }

    /// <summary>
    /// The real tripwire for "a refusal was added without deciding its status code".
    /// <para>
    /// The previous version asserted only that each status produced one of the four codes the
    /// endpoint uses. The controller's `_ =>` default RETURNS one of those four (422), so a status
    /// nobody had mapped satisfied it: the test could not fail for the reason it claimed. Adding
    /// <see cref="GroupRiskAnalysisStatus.InvalidInitialCapital"/> without its `BadRequest` arm was
    /// measured to pass it while returning 422.
    /// </para>
    /// <para>
    /// What actually catches that is COVERAGE of an independently written table: a new enum member
    /// has no row in <see cref="ExpectedCodes"/>, so it fails here, and the theory above then
    /// checks that the controller agrees with the row once one is written.
    /// </para>
    /// </summary>
    [Fact]
    public async Task GetPortfolioRisk_EveryRefusalStatusHasAnIntendedCodeAndTheControllerAgrees()
    {
        var declared = Enum.GetValues<GroupRiskAnalysisStatus>();

        ExpectedCodes.Keys.Should().BeEquivalentTo(
            declared,
            "every status this endpoint can answer with needs a DECIDED code; a new one falling "
            + "through the controller's default would otherwise be published as 422 by accident");

        foreach (var status in declared)
        {
            var (controller, _) = CreateSut(Analysis(status));

            var result = await controller.GetPortfolioRisk(Request());

            var objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(
                ExpectedCodes[status], "{0} must map to its own intended code", status);
        }
    }
}
