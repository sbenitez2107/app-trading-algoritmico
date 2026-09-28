using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Controller-layer tests for the strategy-scoped import surface (SBI-1, WF-1). Direct
/// instantiation + mocked services; service behaviour is covered by
/// <see cref="BacktestImportServiceTests"/> and <see cref="WalkForwardImportServiceTests"/>.
/// <para>
/// The two security-relevant surfaces of this change live here: the server-side extension
/// whitelist, and the run kind arriving as a ROUTE SEGMENT that is rejected before the service or
/// the file is touched.
/// </para>
/// </summary>
public class StrategyBacktestsControllerTests
{
    private readonly Mock<IBacktestImportService> _importMock = new();
    private readonly Mock<IWalkForwardImportService> _wfMock = new();
    private readonly Mock<IBacktestReadService> _readMock = new();
    private readonly Mock<IDemoBacktestComparabilityReadService> _comparabilityMock = new();
    private readonly Mock<ICostDecompositionReadService> _costDecompositionMock = new();
    private readonly Mock<IFtmoBreachSimulationReadService> _ftmoBreachMock = new();
    private readonly Mock<IFtmoMultiStartReadService> _ftmoMultiStartMock = new();

    private StrategyBacktestsController CreateSut()
        => new(
            _importMock.Object, _wfMock.Object, _readMock.Object, _comparabilityMock.Object,
            _costDecompositionMock.Object, _ftmoBreachMock.Object, _ftmoMultiStartMock.Object);

    private static Mock<IFormFile> MockFile(string name, string content = "x")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(name);
        mock.Setup(f => f.Length).Returns(bytes.Length);
        mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
        return mock;
    }

    private void SetupImportOk()
        => _importMock
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, BacktestRunKind _, BacktestFileUploadDto f, PlatformType? _, CancellationToken _) =>
                new BacktestImportResultDto(f.FileName, BacktestImportOutcome.Imported, 329, 0, null));

    // ---- The kind is a route segment, validated before anything else happens ----

    [Fact]
    public async Task ImportTradeList_UnknownKind_Returns400WithoutOpeningTheFileOrCallingTheService()
    {
        SetupImportOk();
        var file = MockFile("ListOfTrades_XAUUSD_H1_IST.csv");

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "bogus", file.Object, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        file.Verify(f => f.OpenReadStream(), Times.Never, "the file must never be opened for a kind that does not exist");
        _importMock.Verify(
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportTradeList_NumericKind_IsAlsoRejected()
    {
        // Binding the segment straight onto the enum would accept "1" and even "0" — and 0 is not
        // a declared member, so it would produce a run in a slot that does not exist.
        SetupImportOk();

        var byNumber = await CreateSut().ImportTradeList(Guid.NewGuid(), "1", MockFile("f.csv").Object, default);
        var byZero = await CreateSut().ImportTradeList(Guid.NewGuid(), "0", MockFile("f.csv").Object, default);

        byNumber.Result.Should().BeOfType<BadRequestObjectResult>();
        byZero.Result.Should().BeOfType<BadRequestObjectResult>();
        _importMock.Verify(
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("deploy", BacktestRunKind.Deploy)]
    [InlineData("evaluation", BacktestRunKind.Evaluation)]
    [InlineData("DEPLOY", BacktestRunKind.Deploy)]
    public async Task ImportTradeList_KnownKind_RoutesToTheMatchingSlot(string segment, BacktestRunKind expected)
    {
        BacktestRunKind? captured = null;
        Guid? capturedStrategyId = null;
        var strategyId = Guid.NewGuid();
        _importMock
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, PlatformType?, CancellationToken>((id, kind, _, _, _) =>
            {
                capturedStrategyId = id;
                captured = kind;
            })
            .ReturnsAsync(new BacktestImportResultDto("f.csv", BacktestImportOutcome.Imported, 329, 0, null));

        var result = await CreateSut().ImportTradeList(strategyId, segment, MockFile("f.csv").Object, default);

        (result.Result as OkObjectResult)!.StatusCode.Should().Be(200);
        captured.Should().Be(expected);
        capturedStrategyId.Should().Be(strategyId, "attribution comes from the route, never from the file");
    }

    // ---- sourcePlatform: caller-declared provenance, guarded against undeclared numerals (D1) ----

    [Fact]
    public async Task ImportTradeList_SourcePlatformAbsent_Returns200AndForwardsNull()
    {
        PlatformType? captured = PlatformType.MT4; // non-null sentinel — proves it flips to null
        _importMock
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, PlatformType?, CancellationToken>((_, _, _, sp, _) => captured = sp)
            .ReturnsAsync(new BacktestImportResultDto("f.csv", BacktestImportOutcome.Imported, 1, 0, null));

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", MockFile("f.csv").Object, default);

        (result.Result as OkObjectResult)!.StatusCode.Should().Be(200);
        captured.Should().BeNull("an omitted sourcePlatform must forward null, never default to a platform");
    }

    [Theory]
    [InlineData(PlatformType.MT4)]
    [InlineData(PlatformType.MT5)]
    public async Task ImportTradeList_SourcePlatformDeclaredMT4OrMT5_ForwardsVerbatim(PlatformType platform)
    {
        PlatformType? captured = null;
        _importMock
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, PlatformType?, CancellationToken>((_, _, _, sp, _) => captured = sp)
            .ReturnsAsync(new BacktestImportResultDto("f.csv", BacktestImportOutcome.Imported, 1, 0, null));

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", MockFile("f.csv").Object, default, platform);

        (result.Result as OkObjectResult)!.StatusCode.Should().Be(200);
        captured.Should().Be(platform);
    }

    [Fact]
    public async Task ImportTradeList_SourcePlatformIsUndeclaredNumeral_Returns400AndServiceIsNeverCalled()
    {
        var result = await CreateSut().ImportTradeList(
            Guid.NewGuid(), "deploy", MockFile("f.csv").Object, default, (PlatformType)7);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _importMock.Verify(
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportTradeList_SourcePlatformIsUndeclaredNumeral_NeverOpensTheFileBeforeTheGuardRuns()
    {
        var file = MockFile("f.csv");

        await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", file.Object, default, (PlatformType)7);

        file.Verify(f => f.OpenReadStream(), Times.Never, "the Enum.IsDefined guard must run before OpenReadStream");
    }

    // ---- Server-side extension whitelist and filename sanitisation ----

    [Fact]
    public async Task ImportTradeList_NonCsvFile_IsRejectedServerSideWithoutCallingTheService()
    {
        SetupImportOk();

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", MockFile("payload.exe").Object, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _importMock.Verify(
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportTradeList_MissingFile_Returns400()
    {
        SetupImportOk();

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", file: null, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportTradeList_PathTraversalInFileName_ArrivesAtTheServiceAsABareFileName()
    {
        BacktestFileUploadDto? captured = null;
        _importMock
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<PlatformType?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, PlatformType?, CancellationToken>((_, _, f, _, _) => captured = f)
            .ReturnsAsync(new BacktestImportResultDto("x", BacktestImportOutcome.Imported, 1, 0, null));

        await CreateSut().ImportTradeList(
            Guid.NewGuid(), "deploy", MockFile("..\\..\\evil\\ListOfTrades_XAUUSD_H1_IST.csv").Object, default);

        captured.Should().NotBeNull();
        captured!.FileName.Should().Be("ListOfTrades_XAUUSD_H1_IST.csv");
    }

    // ---- Walk-forward endpoint ----

    [Fact]
    public async Task ImportWalkForward_ValidCsv_CallsTheWalkForwardServiceWithTheRouteStrategy()
    {
        var strategyId = Guid.NewGuid();
        Guid? captured = null;
        _wfMock
            .Setup(s => s.ImportAsync(It.IsAny<Guid>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestFileUploadDto, CancellationToken>((id, _, _) => captured = id)
            .ReturnsAsync(new WalkForwardImportResultDto(
                "WFParamsExport_XAUUSD_H1.csv", BacktestImportOutcome.Imported, 6, new DateTime(2025, 5, 26), null));

        var result = await CreateSut().ImportWalkForward(strategyId, MockFile("WFParamsExport_XAUUSD_H1.csv").Object, default);

        var body = (result.Result as OkObjectResult)!.Value as WalkForwardImportResultDto;
        body!.WindowCount.Should().Be(6);
        body.OosFromDate.Should().Be(new DateTime(2025, 5, 26));
        captured.Should().Be(strategyId);
    }

    [Fact]
    public async Task ImportWalkForward_NonCsvFile_IsRejectedServerSide()
    {
        var result = await CreateSut().ImportWalkForward(Guid.NewGuid(), MockFile("export.xlsx").Object, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _wfMock.Verify(
            s => s.ImportAsync(It.IsAny<Guid>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---- Read endpoint ----

    [Fact]
    public async Task GetBacktests_ReturnsBothSlotsAndTheExport()
    {
        var strategyId = Guid.NewGuid();
        _readMock
            .Setup(s => s.GetByStrategyAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategyBacktestsDto(
                strategyId,
                new BacktestRunSummaryDto(Guid.NewGuid(), "deploy.csv", "XAUUSD_M1_UTC02", BacktestRunKind.Deploy, 329, DateTime.UtcNow, PlatformType.MT4),
                null,
                new WalkForwardExportSummaryDto(
                    Guid.NewGuid(), "WFParamsExport_XAUUSD_H1.csv", new DateTime(2025, 5, 26), 6,
                    "TEMAPeriod1=32,", "TEMAPeriod1=35,", DateTime.UtcNow)));

        var result = await CreateSut().GetBacktests(strategyId, default);

        var body = (result.Result as OkObjectResult)!.Value as StrategyBacktestsDto;
        body!.Deploy!.TradeCount.Should().Be(329);
        body.Evaluation.Should().BeNull("the evaluation slot is genuinely empty, not an empty run");
        body.WalkForwardExport!.OosFromDate.Should().Be(new DateTime(2025, 5, 26));
        body.WalkForwardExport.WindowCount.Should().Be(6);
    }

    // ---- Comparability endpoint (cost-reconciliation-divergence, slice A) ----

    [Fact]
    public async Task GetComparability_MissingKind_Returns400()
    {
        var result = await CreateSut().GetComparability(Guid.NewGuid(), kind: null, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _comparabilityMock.Verify(
            s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetComparability_ValidRequest_Returns200WithBasis()
    {
        var strategyId = Guid.NewGuid();
        var dto = new PriceOffsetComparabilityDto(
            strategyId, BacktestRunKind.Deploy, ComparabilityReadoutStatus.Measured,
            24, 23, 18, 0, 0, []);
        _comparabilityMock
            .Setup(s => s.GetAsync(strategyId, BacktestRunKind.Deploy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetComparability(strategyId, BacktestRunKind.Deploy, default);

        var body = (result.Result as OkObjectResult)!.Value as PriceOffsetComparabilityDto;
        body!.Basis.Should().Be(ComparabilityBasis.PairedOpensOnly);
        body.PairedCount.Should().Be(24);
    }

    [Fact]
    public async Task GetCostDecomposition_WhenKindMissing_Returns400()
    {
        var result = await CreateSut().GetCostDecomposition(Guid.NewGuid(), kind: null, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _costDecompositionMock.Verify(
            s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCostDecomposition_WhenValid_Returns200WithCoverageComponentOnlyStatus()
    {
        var strategyId = Guid.NewGuid();
        var comparability = new PriceOffsetComparabilityDto(
            strategyId, BacktestRunKind.Deploy, ComparabilityReadoutStatus.Measured, 1, 0, 0, 0, 0, []);
        var dto = new CostDecompositionDto(
            strategyId,
            BacktestRunKind.Deploy,
            CostDecompositionStatus.CoverageComponentOnly,
            comparability,
            new CoverageComponentDto([]));
        _costDecompositionMock
            .Setup(s => s.GetAsync(strategyId, BacktestRunKind.Deploy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetCostDecomposition(strategyId, BacktestRunKind.Deploy, default);

        var body = (result.Result as OkObjectResult)!.Value as CostDecompositionDto;
        body!.Status.Should().Be(CostDecompositionStatus.CoverageComponentOnly);
        body.Coverage.CoverageBasis.Should().Be(CoverageBasis.PresumedFromBacktestTradeAbsence);
    }

    [Fact]
    public async Task GetCostDecomposition_WhenValid_Returns200CarryingBothBases()
    {
        var strategyId = Guid.NewGuid();
        var comparability = new PriceOffsetComparabilityDto(
            strategyId, BacktestRunKind.Deploy, ComparabilityReadoutStatus.Measured, 1, 0, 0, 0, 0, []);
        var dto = new CostDecompositionDto(
            strategyId,
            BacktestRunKind.Deploy,
            CostDecompositionStatus.Decomposed,
            comparability,
            new CoverageComponentDto([]),
            new SwapComponentDto(0m, 0, 1),
            new EmbeddedCostComponentDto(EmbeddedCostAvailability.NoCalibrationRow, null, null, null, null),
            new ExecutionResidualDto(null, null, null, null, null));
        _costDecompositionMock
            .Setup(s => s.GetAsync(strategyId, BacktestRunKind.Deploy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetCostDecomposition(strategyId, BacktestRunKind.Deploy, default);

        var body = (result.Result as OkObjectResult)!.Value as CostDecompositionDto;
        body!.Status.Should().Be(CostDecompositionStatus.Decomposed);
        body.Coverage.CoverageBasis.Should().Be(CoverageBasis.PresumedFromBacktestTradeAbsence);
        body.Residual!.Basis.Should().Be(ResidualBasis.PairedSubsetAfterSwapAndEmbeddedCost);
    }

    // ---- PR P4: GET ftmo-breach ----

    [Fact]
    public async Task GetFtmoBreachSimulation_MissingRequiredQueryParameters_Returns400WithoutCallingTheService()
    {
        var strategyId = Guid.NewGuid();

        var result = await CreateSut().GetFtmoBreachSimulation(
            strategyId, broker: null, sqxSymbol: null, initialCapital: null, targetRiskPerTrade: null,
            fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _ftmoBreachMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFtmoBreachSimulation_Valid_ReturnsTheDtoFromTheReadService()
    {
        var strategyId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var dto = new FtmoBreachSimulationDto(
            strategyId,
            [
                new FtmoRunSimulationResultDto(
                    runId, BacktestRunKind.Deploy, BacktestSegment.InSample, FtmoSimulationStatus.Evaluated, null,
                    new FtmoLimitFindingDto(FtmoBreachVerdict.NoBreachObserved, [], "no breach") { FirstBreach = null, FirstCleanBreach = null },
                    new FtmoLimitFindingDto(FtmoBreachVerdict.NoBreachObserved, [], "no breach") { FirstBreach = null, FirstCleanBreach = null },
                    0, 0, 0, null, null,
                    FtmoRunSimulationResultDto.DefaultNotModelled,
                    FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
                { FirstLimitBreach = null, ReplayStartSourceTime = null, ReplayStartFtmoDay = null, ChallengeRace = null },
            ]);
        _ftmoBreachMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetFtmoBreachSimulation(
            strategyId, broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null,
            sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m, default);

        var body = (result.Result as OkObjectResult)!.Value as FtmoBreachSimulationDto;
        body!.StrategyId.Should().Be(strategyId);
        body.Runs.Single().RunId.Should().Be(runId);
    }

    /// <summary>
    /// A refused run's reason is forwarded VERBATIM through the controller — no translation, no
    /// swallowing, no downgrade to a generic error.
    /// </summary>
    [Fact]
    public async Task GetFtmoBreachSimulation_RefusedRun_ForwardsTheRefusalReasonVerbatim()
    {
        var strategyId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var dto = new FtmoBreachSimulationDto(
            strategyId,
            [
                new FtmoRunSimulationResultDto(
                    runId, BacktestRunKind.Deploy, BacktestSegment.Unknown, FtmoSimulationStatus.Refused,
                    FtmoSimulationRefusal.FxRateNotDeclared,
                    null, null, 0, 0, 0, null, null,
                    FtmoRunSimulationResultDto.DefaultNotModelled,
                    FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
                { FirstLimitBreach = null, ReplayStartSourceTime = null, ReplayStartFtmoDay = null, ChallengeRace = null },
            ]);
        _ftmoBreachMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetFtmoBreachSimulation(
            strategyId, broker: "FTMO", sqxSymbol: "DEUIDXEUR_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 50m, fxLow: null, fxHigh: null,
            sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 1000m, default);

        var body = (result.Result as OkObjectResult)!.Value as FtmoBreachSimulationDto;
        body!.Runs.Single().Refusal.Should().Be(FtmoSimulationRefusal.FxRateNotDeclared);
    }

    // ---- RELIABILITY-001: the SOURCE lot grid is declared by the caller and never substituted ----

    /// <summary>
    /// Calls the endpoint with every non-grid parameter valid and the four grid values as given,
    /// capturing the request the controller hands to the read service.
    /// </summary>
    private async Task<FtmoBreachSimulationRequest> CaptureFtmoRequestAsync(
        int sizeDecimals, decimal step, decimal minLot, decimal maxLots)
    {
        FtmoBreachSimulationRequest? captured = null;
        _ftmoBreachMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FtmoBreachSimulationRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new FtmoBreachSimulationDto(Guid.NewGuid(), []));

        var result = await CreateSut().GetFtmoBreachSimulation(
            Guid.NewGuid(), broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null,
            sizeDecimals: sizeDecimals, step: step, minLot: minLot, maxLots: maxLots, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull("an explicitly declared grid must reach the read service");
        return captured!;
    }

    /// <summary>
    /// A whole-lot grid (<c>sizeDecimals=0</c>, step 1) is legitimate. It must reach the service
    /// verbatim — rewriting the 0 to 2 would turn a valid grid into one <c>LotGrid</c> rejects.
    /// </summary>
    [Fact]
    public async Task GetFtmoBreachSimulation_ExplicitWholeLotGrid_SizeDecimalsZeroIsPreservedVerbatim()
    {
        var request = await CaptureFtmoRequestAsync(sizeDecimals: 0, step: 1m, minLot: 1m, maxLots: 100m);

        request.SizeDecimals.Should().Be(0);
        request.Step.Should().Be(1m);
        request.MinLot.Should().Be(1m);
        request.MaxLots.Should().Be(100m);
        request.TryBuildSourceGrid().Should().NotBeNull("a whole-lot grid is valid");
    }

    /// <summary>
    /// An explicitly INVALID grid value is passed through untouched so that
    /// <see cref="FtmoBreachSimulationRequest.TryBuildSourceGrid"/> — the single validation surface —
    /// rejects it and the service refuses with <c>InvalidRequest</c>. Rewriting it to a "valid"
    /// value would simulate a grid the caller never declared.
    /// </summary>
    [Theory]
    [InlineData(2, 0, 0.01, 10)] // step = 0
    [InlineData(2, 0.01, 0, 10)] // minLot = 0
    [InlineData(2, 0.01, 0.01, 0)] // maxLots = 0
    public async Task GetFtmoBreachSimulation_ExplicitZeroGridValue_IsPassedThroughUntouched(
        int sizeDecimals, double step, double minLot, double maxLots)
    {
        var request = await CaptureFtmoRequestAsync(sizeDecimals, (decimal)step, (decimal)minLot, (decimal)maxLots);

        request.SizeDecimals.Should().Be(sizeDecimals);
        request.Step.Should().Be((decimal)step);
        request.MinLot.Should().Be((decimal)minLot);
        request.MaxLots.Should().Be((decimal)maxLots);
        request.TryBuildSourceGrid().Should().BeNull("the declared grid is invalid and must be refused, not repaired");
    }

    /// <summary>
    /// An OMITTED grid parameter is a 400 at the controller, exactly like an omitted
    /// <c>broker</c>/<c>sqxSymbol</c>/<c>initialCapital</c>/<c>targetRiskPerTrade</c>: presence is
    /// the controller's concern, value validity is the service's. The service is never called with
    /// a grid the caller did not declare.
    /// </summary>
    [Theory]
    [InlineData("sizeDecimals")]
    [InlineData("step")]
    [InlineData("minLot")]
    [InlineData("maxLots")]
    public async Task GetFtmoBreachSimulation_GridParameterOmitted_Returns400WithoutCallingTheService(string omitted)
    {
        var result = await CreateSut().GetFtmoBreachSimulation(
            Guid.NewGuid(), broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null,
            sizeDecimals: omitted == "sizeDecimals" ? null : 2,
            step: omitted == "step" ? null : 0.01m,
            minLot: omitted == "minLot" ? null : 0.01m,
            maxLots: omitted == "maxLots" ? null : 10m,
            default);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain(omitted);
        _ftmoBreachMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---- PR4 follow-up (task 4.3.7): before/after pin of the single-start ftmo-breach endpoint ----

    /// <summary>
    /// Verbatim copy of the SHIPPED <c>GetFtmoBreachSimulation</c> body at commit <c>368cd6b</c> (the
    /// HEAD this PR4 batch started from), captured via <c>git show 368cd6b:...StrategyBacktestsController.cs</c>
    /// BEFORE this session's <c>TryValidateFtmoBreachQuery</c> extraction. Kept test-local as the "before"
    /// oracle for spec.md "The single-start endpoint is untouched" — this pins BEHAVIOUR, not the
    /// extracted implementation, so a future refactor of the shared helper cannot silently change the
    /// single-start endpoint's contract without this test catching it.
    /// </summary>
    private static async Task<ActionResult<FtmoBreachSimulationDto>> Pre368cd6bGetFtmoBreachSimulation(
        IFtmoBreachSimulationReadService service, Guid strategyId, string? broker, string? sqxSymbol,
        decimal? initialCapital, decimal? targetRiskPerTrade, decimal? fxLow, decimal? fxHigh,
        int? sizeDecimals, decimal? step, decimal? minLot, decimal? maxLots, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(broker) || string.IsNullOrWhiteSpace(sqxSymbol)
            || initialCapital is null || targetRiskPerTrade is null
            || sizeDecimals is null || step is null || minLot is null || maxLots is null)
        {
            return new BadRequestObjectResult(new
            {
                message = "The 'broker', 'sqxSymbol', 'initialCapital', 'targetRiskPerTrade', 'sizeDecimals', "
                    + "'step', 'minLot' and 'maxLots' query parameters are required. There is no default.",
            });
        }

        var request = new FtmoBreachSimulationRequest(
            strategyId, broker, sqxSymbol, initialCapital.Value, targetRiskPerTrade.Value, fxLow, fxHigh,
            sizeDecimals.Value, step.Value, minLot.Value, maxLots.Value);

        return new OkObjectResult(await service.SimulateAsync(request, ct));
    }

    [Fact]
    public async Task GetFtmoBreachSimulation_MissingParams_MatchesThePre368cd6bResponseVerbatim()
    {
        var strategyId = Guid.NewGuid();

        var before = await Pre368cd6bGetFtmoBreachSimulation(
            _ftmoBreachMock.Object, strategyId, broker: null, sqxSymbol: null, initialCapital: null,
            targetRiskPerTrade: null, fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m,
            maxLots: 10m, default);
        var after = await CreateSut().GetFtmoBreachSimulation(
            strategyId, broker: null, sqxSymbol: null, initialCapital: null, targetRiskPerTrade: null,
            fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m, default);

        var beforeBody = before.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var afterBody = after.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        afterBody.Value.Should().BeEquivalentTo(beforeBody.Value, "the 400 path must be byte-identical before and after the TryValidateFtmoBreachQuery extraction");
        _ftmoBreachMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFtmoBreachSimulation_Valid_MatchesThePre368cd6bResponseVerbatim()
    {
        var strategyId = Guid.NewGuid();
        var dto = new FtmoBreachSimulationDto(strategyId, []);
        _ftmoBreachMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var before = await Pre368cd6bGetFtmoBreachSimulation(
            _ftmoBreachMock.Object, strategyId, broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m,
            maxLots: 10m, default);
        var after = await CreateSut().GetFtmoBreachSimulation(
            strategyId, broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m,
            maxLots: 10m, default);

        var beforeOk = before.Result.Should().BeOfType<OkObjectResult>().Subject;
        var afterOk = after.Result.Should().BeOfType<OkObjectResult>().Subject;
        afterOk.Value.Should().BeEquivalentTo(beforeOk.Value, "the 200 path (request construction + service passthrough) must be byte-identical before and after the extraction");
    }

    // ---- PR4: GET ftmo-breach/multi-start ----

    [Fact]
    public async Task MultiStart_ReturnsTheServiceResult()
    {
        var strategyId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var dto = new FtmoMultiStartDto(
            strategyId,
            [
                new FtmoMultiStartRunDto(
                    runId, BacktestRunKind.Deploy, BacktestSegment.InSample, FtmoSimulationStatus.Evaluated, null,
                    null, null, FtmoStartGrain.Monthly,
                    new FtmoChallengeRulesDto(0.10m, 0.05m, 4, null),
                    [], null, [], false, null, null, 0,
                    FtmoRunSimulationResultDto.DefaultNotModelled, "disclosure"),
            ]);
        _ftmoMultiStartMock
            .Setup(s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateSut().GetFtmoMultiStart(
            strategyId, broker: "FTMO", sqxSymbol: "XAUUSD_M1_UTC02", initialCapital: 10_000m,
            targetRiskPerTrade: 100m, fxLow: null, fxHigh: null,
            sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m, default);

        var body = (result.Result as OkObjectResult)!.Value as FtmoMultiStartDto;
        body!.StrategyId.Should().Be(strategyId);
        body.Runs.Single().RunId.Should().Be(runId);
    }

    [Fact]
    public async Task MultiStart_InvalidRequestReturns400()
    {
        var strategyId = Guid.NewGuid();

        var result = await CreateSut().GetFtmoMultiStart(
            strategyId, broker: null, sqxSymbol: null, initialCapital: null, targetRiskPerTrade: null,
            fxLow: null, fxHigh: null, sizeDecimals: 2, step: 0.01m, minLot: 0.01m, maxLots: 10m, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _ftmoMultiStartMock.Verify(
            s => s.SimulateAsync(It.IsAny<FtmoBreachSimulationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
