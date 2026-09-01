using AppTradingAlgoritmico.Application.DTOs.Backtests;
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

    private StrategyBacktestsController CreateSut()
        => new(_importMock.Object, _wfMock.Object, _readMock.Object);

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
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, BacktestRunKind _, BacktestFileUploadDto f, CancellationToken _) =>
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
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()),
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
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()),
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
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, CancellationToken>((id, kind, _, _) =>
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

    // ---- Server-side extension whitelist and filename sanitisation ----

    [Fact]
    public async Task ImportTradeList_NonCsvFile_IsRejectedServerSideWithoutCallingTheService()
    {
        SetupImportOk();

        var result = await CreateSut().ImportTradeList(Guid.NewGuid(), "deploy", MockFile("payload.exe").Object, default);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _importMock.Verify(
            s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()),
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
            .Setup(s => s.ImportTradeListAsync(It.IsAny<Guid>(), It.IsAny<BacktestRunKind>(), It.IsAny<BacktestFileUploadDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestRunKind, BacktestFileUploadDto, CancellationToken>((_, _, f, _) => captured = f)
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
                new BacktestRunSummaryDto(Guid.NewGuid(), "deploy.csv", "XAUUSD_M1_UTC02", BacktestRunKind.Deploy, 329, DateTime.UtcNow),
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
}
