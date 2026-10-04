using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.5 — controller-layer tests for <c>POST api/ftmo-simulations/group</c>, following
/// the repo's direct-instantiation + mocked-service convention (<c>StrategyBacktestsControllerTests</c>). The
/// 401 is the framework's answer to <c>[Authorize]</c>, which the repo has no WebApplicationFactory suite to
/// exercise, so it is pinned by reflecting the attribute.
/// </summary>
public class FtmoSimulationsControllerTests
{
    private readonly Mock<IFtmoGroupSimulationReadService> _groupMock = new();

    private FtmoSimulationsController CreateSut() => new(_groupMock.Object);

    private static FtmoGroupSimulationRequest Valid(IReadOnlyList<Guid>? ids = null) => new(
        ids ?? [Guid.NewGuid()], "FTMO", InitialCapital: 10_000m, TargetRiskPerTrade: 100m, FxLow: null, FxHigh: null,
        SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m);

    private static FtmoGroupSimulationDto EmptyDto() => new(
        FtmoSimulationStatus.Evaluated, null, null, 0.05m, 0.10m, [], [], [], [], [], FtmoGroupSimulationLimits.Disclosures);

    private void SetupOk(Action<FtmoGroupSimulationParameters, CancellationToken>? capture = null)
        => _groupMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoGroupSimulationParameters>(), It.IsAny<CancellationToken>()))
            .Callback<FtmoGroupSimulationParameters, CancellationToken>((p, ct) => capture?.Invoke(p, ct))
            .ReturnsAsync(EmptyDto());

    private void VerifyNeverCalled()
        => _groupMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoGroupSimulationParameters>(), It.IsAny<CancellationToken>()), Times.Never);

    private static List<Guid> DistinctIds(int count)
        => [.. Enumerable.Range(1, count).Select(i => new Guid($"00000000-0000-0000-0000-{i:D12}"))];

    // ---- endpoint contract ----

    [Fact]
    public void TheController_IsAuthorized_RoutedAtApiFtmoSimulations_AndPostGroupIsNotAnonymous()
    {
        var type = typeof(FtmoSimulationsController);
        var action = type.GetMethod(nameof(FtmoSimulationsController.SimulateGroup))!;

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull("an unauthenticated call must get 401");
        type.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/ftmo-simulations");
        action.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("group");
        action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    [Fact]
    public async Task SimulateGroup_Valid_ReturnsTheDtoFromTheReadService()
    {
        SetupOk();

        var result = await CreateSut().SimulateGroup(Valid(), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<FtmoGroupSimulationDto>();
    }

    [Fact]
    public async Task SimulateGroup_PassesTheCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        SetupOk((_, ct) => seen = ct);

        await CreateSut().SimulateGroup(Valid(), cts.Token);

        seen.Should().Be(cts.Token);
    }

    // ---- 400: missing required fields ----

    public static IEnumerable<object[]> MissingFieldCases()
    {
        yield return ["memberStrategyIds", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { MemberStrategyIds = null })];
        yield return ["broker", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { Broker = null })];
        yield return ["broker", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { Broker = "  " })];
        yield return ["initialCapital", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { InitialCapital = null })];
        yield return ["targetRiskPerTrade", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { TargetRiskPerTrade = null })];
        yield return ["sizeDecimals", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { SizeDecimals = null })];
        yield return ["step", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { Step = null })];
        yield return ["minLot", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { MinLot = null })];
        yield return ["maxLots", (Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest>)(r => r with { MaxLots = null })];
    }

    [Theory]
    [MemberData(nameof(MissingFieldCases))]
    public async Task SimulateGroup_AMissingRequiredField_Returns400NamingTheFieldsAndDoesNotCompute(
        string field, Func<FtmoGroupSimulationRequest, FtmoGroupSimulationRequest> remove)
    {
        var result = await CreateSut().SimulateGroup(remove(Valid()), CancellationToken.None);

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain(field).And.Contain("There is no default");
        VerifyNeverCalled();
    }

    [Fact]
    public async Task SimulateGroup_ANullBody_Returns400()
    {
        var result = await CreateSut().SimulateGroup(null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        VerifyNeverCalled();
    }

    [Fact]
    public async Task SimulateGroup_AnEmptyMemberList_Returns400()
    {
        var result = await CreateSut().SimulateGroup(Valid([]), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        VerifyNeverCalled();
    }

    [Fact]
    public async Task SimulateGroup_AnEmptyGuid_Returns400()
    {
        var result = await CreateSut().SimulateGroup(Valid([Guid.NewGuid(), Guid.Empty]), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        VerifyNeverCalled();
    }

    // ---- accepted values ----

    [Fact]
    public async Task SimulateGroup_SizeDecimalsZero_IsAccepted_AndForwardedVerbatim()
    {
        FtmoGroupSimulationParameters? seen = null;
        SetupOk((p, _) => seen = p);

        var result = await CreateSut().SimulateGroup(
            Valid() with { SizeDecimals = 0, Step = 1m, MinLot = 1m, MaxLots = 100m }, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        seen!.SizeDecimals.Should().Be(0);
    }

    [Fact]
    public async Task SimulateGroup_AValuePresentButUnusable_IsNotA400_TheServiceRefusesIt()
    {
        SetupOk();

        var result = await CreateSut().SimulateGroup(
            Valid() with { InitialCapital = 0m, TargetRiskPerTrade = -5m }, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _groupMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoGroupSimulationParameters>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- the cap ----

    [Fact]
    public async Task SimulateGroup_AtTheCap_IsAccepted()
    {
        SetupOk();

        var result = await CreateSut().SimulateGroup(Valid(DistinctIds(FtmoGroupSimulationLimits.MaxMembers)), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SimulateGroup_OneAboveTheCap_Returns400StatingTheCap()
    {
        var result = await CreateSut().SimulateGroup(
            Valid(DistinctIds(FtmoGroupSimulationLimits.MaxMembers + 1)), CancellationToken.None);

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain(FtmoGroupSimulationLimits.MaxMembers.ToString());
        VerifyNeverCalled();
    }

    [Fact]
    public async Task SimulateGroup_TheCapCountsDistinctIds_ARepeatedIdIsNotASecondMember_AndIsForwardedForTheEcho()
    {
        var ids = DistinctIds(FtmoGroupSimulationLimits.MaxMembers);
        ids.Add(ids[0]);
        FtmoGroupSimulationParameters? seen = null;
        SetupOk((p, _) => seen = p);

        var result = await CreateSut().SimulateGroup(Valid(ids), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        seen!.MemberStrategyIds.Should().HaveCount(ids.Count, "the service, not the controller, deduplicates and echoes the removed ids");
    }
}
