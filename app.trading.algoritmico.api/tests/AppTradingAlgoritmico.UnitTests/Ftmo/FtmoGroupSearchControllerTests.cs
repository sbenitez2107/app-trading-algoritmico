using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2b - the controller (direct instantiation, like <c>FtmoSimulationsControllerTests</c>) and the DI
/// wiring. The 401 is the framework answer to <c>[Authorize]</c>, pinned by reflection. Validation precedes any start.
/// </summary>
public class FtmoGroupSearchControllerTests
{
    private readonly Mock<IFtmoGroupSearchJobs> _jobs = new();

    private FtmoGroupSearchController CreateSut() => new(_jobs.Object);

    private static FtmoGroupSearchRequest Valid() => new(
        TradingAccountId: Guid.NewGuid(), MinMembers: 2, MaxMembers: 4, MaxPerInstrument: 2,
        IncludeIdenticalDeployEval: false, OnePercentRule: false, Broker: "FTMO", InitialCapital: 10000m,
        TargetRiskPerTrade: 100m, SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 100m);

    private static readonly FtmoGroupSearchRequest SnapshotRequest = Valid();

    private static FtmoGroupSearchJobDto Snapshot(Guid id) => new(
        id, FtmoGroupSearchStatus.Running,
        new FtmoGroupSearchProgressDto(FtmoGroupSearchStage.Loading, 0, 0, 0, 0, 150, new FtmoGroupSearchFunnelDto(0, 0, 0, 0, 0, 0, 0)),
        FtmoGroupSearchStopReason.None, 0, [], [], [], null, SnapshotRequest);

    [Fact]
    public void Controller_IsAuthorized_OnTheRoute_AndNoActionIsAnonymous()
    {
        var type = typeof(FtmoGroupSearchController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull("an unauthenticated call must get 401");
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/ftmo-simulations/group-search");
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null);
    }

    [Fact]
    public void Start_Valid_Is202_WithLocationJobIdAndInitialSnapshot()
    {
        var id = Guid.NewGuid();
        _jobs.Setup(j => j.TryStart(It.IsAny<FtmoGroupSearchRequest>())).Returns(new FtmoGroupSearchStartResult(true, id));
        _jobs.Setup(j => j.Get(id)).Returns(Snapshot(id));

        var result = CreateSut().Start(Valid());

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Location.Should().Be($"/api/ftmo-simulations/group-search/{id}");
        accepted.Value.Should().BeEquivalentTo(new { jobId = id, job = Snapshot(id) });
        _jobs.Verify(j => j.TryStart(It.IsAny<FtmoGroupSearchRequest>()), Times.Once);
    }

    [Fact]
    public void Start_ThenGetCurrent_ReturnsTheSubmittedRequest_WithRiskAndCapitalIntact()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var sut = new FtmoGroupSearchController(registry);
        var request = Valid() with { EliminationCeiling = 0.07m, FxLow = 0.9m, FxHigh = 1.1m };

        var accepted = sut.Start(request).Should().BeOfType<AcceptedResult>().Subject;
        var posted = (FtmoGroupSearchJobDto)accepted.Value!.GetType().GetProperty("job")!.GetValue(accepted.Value)!;
        var current = sut.GetCurrent().Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<FtmoGroupSearchJobDto>().Subject;
        var byId = sut.Get(posted.JobId).Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<FtmoGroupSearchJobDto>().Subject;

        posted.Request.Should().Be(request);
        current.Request.Should().Be(request);
        byId.Request.Should().Be(request);
        current.Request.TargetRiskPerTrade.Should().Be(100m);
        current.Request.InitialCapital.Should().Be(10000m);
    }

    [Fact]
    public void Start_WhileAJobRuns_Is409_CarryingTheRunningJobId()
    {
        var running = Guid.NewGuid();
        _jobs.Setup(j => j.TryStart(It.IsAny<FtmoGroupSearchRequest>())).Returns(new FtmoGroupSearchStartResult(false, running));

        var result = CreateSut().Start(Valid());

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeEquivalentTo(new { runningJobId = running });
    }

    public static TheoryData<string, Func<FtmoGroupSearchRequest, FtmoGroupSearchRequest>> InvalidRequests() => new()
    {
        { "null body", _ => null! },
        { "missing account", r => r with { TradingAccountId = null } },
        { "empty account", r => r with { TradingAccountId = Guid.Empty } },
        { "missing broker", r => r with { Broker = null } },
        { "blank broker", r => r with { Broker = " " } },
        { "missing capital", r => r with { InitialCapital = null } },
        { "zero capital", r => r with { InitialCapital = 0m } },
        { "negative capital", r => r with { InitialCapital = -1m } },
        { "missing risk", r => r with { TargetRiskPerTrade = null } },
        { "zero risk", r => r with { TargetRiskPerTrade = 0m } },
        { "negative risk", r => r with { TargetRiskPerTrade = -5m } },
        { "missing minMembers", r => r with { MinMembers = null } },
        { "missing maxMembers", r => r with { MaxMembers = null } },
        { "minMembers 1", r => r with { MinMembers = 1 } },
        { "minMembers 0", r => r with { MinMembers = 0 } },
        { "maxMembers above the cap", r => r with { MaxMembers = FtmoGroupSimulationLimits.MaxMembers + 1 } },
        { "min greater than max", r => r with { MinMembers = 4, MaxMembers = 3 } },
        { "missing maxPerInstrument", r => r with { MaxPerInstrument = null } },
        { "maxPerInstrument 0", r => r with { MaxPerInstrument = 0 } },
        { "missing includeIdenticalDeployEval", r => r with { IncludeIdenticalDeployEval = null } },
        { "missing onePercentRule", r => r with { OnePercentRule = null } },
        { "missing sizeDecimals", r => r with { SizeDecimals = null } },
        { "negative sizeDecimals", r => r with { SizeDecimals = -1 } },
        { "missing step", r => r with { Step = null } },
        { "zero step", r => r with { Step = 0m } },
        { "missing minLot", r => r with { MinLot = null } },
        { "zero minLot", r => r with { MinLot = 0m } },
        { "missing maxLots", r => r with { MaxLots = null } },
        { "maxLots below minLot", r => r with { MinLot = 1m, MaxLots = 0.5m } },
        { "only fxLow", r => r with { FxLow = 0.9m } },
        { "only fxHigh", r => r with { FxHigh = 1.1m } },
        { "fx inverted", r => r with { FxLow = 1.2m, FxHigh = 1.1m } },
        { "ceiling negative", r => r with { EliminationCeiling = -0.01m } },
        { "ceiling above 1", r => r with { EliminationCeiling = 1.01m } },
        { "budget sims 0", r => r with { MaxFullSimulations = 0 } },
        { "budget sims above ceiling", r => r with { MaxFullSimulations = FtmoGroupSearchLimits.MaxFullSimulationsCeiling + 1 } },
        { "budget wall clock 0", r => r with { MaxWallClockSeconds = 0 } },
        { "budget wall clock above ceiling", r => r with { MaxWallClockSeconds = FtmoGroupSearchLimits.MaxWallClockSecondsCeiling + 1 } },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Start_Invalid_Is400_WithMessage_AndNothingStarts(string _, Func<FtmoGroupSearchRequest, FtmoGroupSearchRequest> mutate)
    {
        var result = CreateSut().Start(mutate(Valid()));

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().BeOfType<string>().Which.Should().NotBeNullOrWhiteSpace();
        _jobs.Verify(j => j.TryStart(It.IsAny<FtmoGroupSearchRequest>()), Times.Never);
    }

    public static TheoryData<string, Func<FtmoGroupSearchRequest, FtmoGroupSearchRequest>> LegalRequests() => new()
    {
        { "sizeDecimals 0 (whole lots)", r => r with { SizeDecimals = 0, Step = 1m, MinLot = 1m } },
        { "ceiling 0", r => r with { EliminationCeiling = 0m } },
        { "ceiling 1", r => r with { EliminationCeiling = 1m } },
        { "min = max = 2", r => r with { MinMembers = 2, MaxMembers = 2 } },
        { "max = cap", r => r with { MaxMembers = FtmoGroupSimulationLimits.MaxMembers } },
        { "maxPerInstrument 1", r => r with { MaxPerInstrument = 1 } },
        { "budgets at their ceilings", r => r with { MaxFullSimulations = FtmoGroupSearchLimits.MaxFullSimulationsCeiling, MaxWallClockSeconds = FtmoGroupSearchLimits.MaxWallClockSecondsCeiling } },
        { "budgets at 1", r => r with { MaxFullSimulations = 1, MaxWallClockSeconds = 1 } },
        { "fx band", r => r with { FxLow = 0.9m, FxHigh = 1.1m } },
    };

    [Theory]
    [MemberData(nameof(LegalRequests))]
    public void Start_LegalEdgeValues_AreAccepted(string _, Func<FtmoGroupSearchRequest, FtmoGroupSearchRequest> mutate)
    {
        var id = Guid.NewGuid();
        _jobs.Setup(j => j.TryStart(It.IsAny<FtmoGroupSearchRequest>())).Returns(new FtmoGroupSearchStartResult(true, id));
        _jobs.Setup(j => j.Get(id)).Returns(Snapshot(id));

        CreateSut().Start(mutate(Valid())).Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public void Get_Known_Is200_Unknown_Is404()
    {
        var id = Guid.NewGuid();
        _jobs.Setup(j => j.Get(id)).Returns(Snapshot(id));

        CreateSut().Get(id).Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(Snapshot(id));
        CreateSut().Get(Guid.NewGuid()).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetCurrent_WithAJob_Is200_WithNone_Is204()
    {
        var id = Guid.NewGuid();
        _jobs.SetupSequence(j => j.GetCurrent()).Returns(Snapshot(id)).Returns((FtmoGroupSearchJobDto?)null);

        CreateSut().GetCurrent().Should().BeOfType<OkObjectResult>();
        CreateSut().GetCurrent().Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void Routes_GuidConstraintKeepsCurrentUnambiguous()
    {
        var type = typeof(FtmoGroupSearchController);
        type.GetMethod(nameof(FtmoGroupSearchController.GetCurrent))!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("current");
        type.GetMethod(nameof(FtmoGroupSearchController.Get))!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{jobId:guid}");
        type.GetMethod(nameof(FtmoGroupSearchController.Cancel))!.GetCustomAttribute<HttpDeleteAttribute>()!.Template.Should().Be("{jobId:guid}");
    }

    [Fact]
    public void Cancel_Known_Is204_Unknown_Is404()
    {
        var known = Guid.NewGuid();
        _jobs.Setup(j => j.Cancel(known)).Returns(true);

        CreateSut().Cancel(known).Should().BeOfType<NoContentResult>();
        CreateSut().Cancel(Guid.NewGuid()).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Di_ResolvesTheController_TheWorkerAndTheRunner_WithTheRegistryAsASingleton()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddTransient<FtmoGroupSearchController>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<FtmoGroupSearchController>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(s => s is FtmoGroupSearchWorker);
        provider.GetRequiredService<IFtmoGroupSearchRunner>().Should().BeOfType<FtmoGroupSearchRunner>();
        scope.ServiceProvider.GetRequiredService<IFtmoGroupSearchJobs>().Should().BeSameAs(provider.GetRequiredService<IFtmoGroupSearchJobs>());
    }
}
