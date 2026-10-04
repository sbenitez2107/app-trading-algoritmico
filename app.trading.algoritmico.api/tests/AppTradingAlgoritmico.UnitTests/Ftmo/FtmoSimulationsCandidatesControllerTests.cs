using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B3.2 — controller-layer tests for <c>GET api/ftmo-simulations/candidates</c>, in the
/// same direct-instantiation style as <c>FtmoSimulationsControllerTests</c> (which is not edited: the candidates
/// service reaches the action through <c>[FromServices]</c>, so the controller constructor is unchanged). The 401
/// is the framework's answer to <c>[Authorize]</c>, pinned by reflecting the attribute.
/// </summary>
public class FtmoSimulationsCandidatesControllerTests
{
    private readonly Mock<IFtmoGroupSimulationReadService> _groupMock = new();
    private readonly Mock<IFtmoGroupCandidatesReadService> _candidatesMock = new();

    private FtmoSimulationsController CreateSut() => new(_groupMock.Object);

    private static FtmoGroupCandidatesDto Dto(Guid account) => new(account, FtmoGroupSimulationLimits.MaxMembers, []);

    [Fact]
    public void GetCandidates_IsGetAtCandidates_OnTheAuthorizedController_AndNotAnonymous()
    {
        var type = typeof(FtmoSimulationsController);
        var action = type.GetMethod(nameof(FtmoSimulationsController.GetCandidates))!;

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull("an unauthenticated call must get 401");
        action.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("candidates");
        action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    [Fact]
    public async Task GetCandidates_AMissingAccountId_Is400_AndTheServiceIsNeverCalled()
    {
        var result = await CreateSut().GetCandidates(tradingAccountId: null, _candidatesMock.Object, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _candidatesMock.Verify(s => s.GetCandidatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCandidates_AnEmptyGuid_Is400_AndTheServiceIsNeverCalled()
    {
        var result = await CreateSut().GetCandidates(Guid.Empty, _candidatesMock.Object, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _candidatesMock.Verify(s => s.GetCandidatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCandidates_AValidAccount_ReturnsTheServiceResult_AndForwardsTheIdAndTheToken()
    {
        var account = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _candidatesMock
            .Setup(s => s.GetCandidatesAsync(account, cts.Token))
            .ReturnsAsync(Dto(account));

        var result = await CreateSut().GetCandidates(account, _candidatesMock.Object, cts.Token);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<FtmoGroupCandidatesDto>().Which.TradingAccountId.Should().Be(account);
        _candidatesMock.Verify(s => s.GetCandidatesAsync(account, cts.Token), Times.Once);
    }
}
