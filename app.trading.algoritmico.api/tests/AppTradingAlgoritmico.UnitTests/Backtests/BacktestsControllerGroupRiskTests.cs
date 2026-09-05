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

    [Theory]
    [InlineData(GroupRiskAnalysisStatus.SegmentNotSpecified, StatusCodes.Status400BadRequest)]
    [InlineData(GroupRiskAnalysisStatus.UnknownSegmentNotSelectable, StatusCodes.Status400BadRequest)]
    [InlineData(GroupRiskAnalysisStatus.NoStrategiesRequested, StatusCodes.Status400BadRequest)]
    [InlineData(GroupRiskAnalysisStatus.InvalidLotGrid, StatusCodes.Status400BadRequest)]
    [InlineData(GroupRiskAnalysisStatus.StrategyNotFound, StatusCodes.Status404NotFound)]
    [InlineData(GroupRiskAnalysisStatus.RunSegmentsDisagree, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(GroupRiskAnalysisStatus.NoEvidenceForSegment, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(GroupRiskAnalysisStatus.AmbiguousRunSelection, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(GroupRiskAnalysisStatus.RiskNotEstimable, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(GroupRiskAnalysisStatus.NonUnitWeight, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(GroupRiskAnalysisStatus.HeterogeneousGroup, StatusCodes.Status422UnprocessableEntity)]
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

    [Fact]
    public async Task GetPortfolioRisk_EveryRefusalStatusHasItsOwnMapping()
    {
        // A rename tripwire: adding a refusal without mapping it would otherwise silently fall
        // through to whatever the default branch happens to be.
        foreach (var status in Enum.GetValues<GroupRiskAnalysisStatus>())
        {
            var (controller, _) = CreateSut(Analysis(status));

            var result = await controller.GetPortfolioRisk(Request());

            var objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
            objectResult.StatusCode.Should().NotBeNull($"{status} must map to an explicit status code");
            objectResult.StatusCode.Should().BeOneOf(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity);
        }
    }
}
