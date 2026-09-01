using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Controller-layer tests for the READ-ONLY backtest endpoints — direct instantiation + a mocked
/// <see cref="IBacktestReadService"/>. Import moved to
/// <see cref="StrategyBacktestsControllerTests"/> when attribution became a route parameter, so
/// what is left here is the query surface: paging defaults reach the service unchanged, and the
/// optional segment filter is forwarded rather than dropped.
/// </summary>
public class BacktestsControllerTests
{
    private static BacktestsController CreateSut(Mock<IBacktestReadService> mock) => new(mock.Object);

    [Fact]
    public async Task GetRuns_ReturnsThePageTheServiceProduced()
    {
        var run = new BacktestRunDto(
            Guid.NewGuid(), "ListOfTrades_XAUUSD_H1_IST.csv", "XAUUSD_M1_UTC02",
            Guid.NewGuid(), "My Strategy", BacktestRunKind.Deploy, 329, DateTime.UtcNow);
        var serviceMock = new Mock<IBacktestReadService>();
        serviceMock
            .Setup(s => s.GetRunsAsync(2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunDto>([run], 41, 2, 20));

        var result = await CreateSut(serviceMock).GetRuns(page: 2, pageSize: 20);

        var body = (result.Result as OkObjectResult)!.Value as PagedResult<BacktestRunDto>;
        body!.TotalCount.Should().Be(41);
        body.Items.Should().ContainSingle();
        body.Items[0].StrategyName.Should().Be("My Strategy");
        body.Items[0].Kind.Should().Be(BacktestRunKind.Deploy);
        body.Items[0].TradeCount.Should().Be(329);
    }

    [Fact]
    public async Task GetTrades_ForwardsTheSegmentFilterAndTheRunId()
    {
        var runId = Guid.NewGuid();
        Guid? capturedRunId = null;
        BacktestSegment? capturedSegment = null;
        var serviceMock = new Mock<IBacktestReadService>();
        serviceMock
            .Setup(s => s.GetTradesByRunAsync(It.IsAny<Guid>(), It.IsAny<BacktestSegment?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestSegment?, int, int, CancellationToken>((id, segment, _, _, _) =>
            {
                capturedRunId = id;
                capturedSegment = segment;
            })
            .ReturnsAsync(new PagedResult<BacktestTradeDto>([], 0, 1, 50));

        await CreateSut(serviceMock).GetTrades(runId, BacktestSegment.OutOfSample);

        capturedRunId.Should().Be(runId);
        capturedSegment.Should().Be(BacktestSegment.OutOfSample, "the segment filter must not be silently dropped");
    }

    [Fact]
    public async Task GetTrades_WithNoSegment_PassesNullSoEverySegmentIsReturned()
    {
        BacktestSegment? capturedSegment = BacktestSegment.InSample;
        var serviceMock = new Mock<IBacktestReadService>();
        serviceMock
            .Setup(s => s.GetTradesByRunAsync(It.IsAny<Guid>(), It.IsAny<BacktestSegment?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, BacktestSegment?, int, int, CancellationToken>((_, segment, _, _, _) => capturedSegment = segment)
            .ReturnsAsync(new PagedResult<BacktestTradeDto>([], 0, 1, 50));

        await CreateSut(serviceMock).GetTrades(Guid.NewGuid(), segment: null);

        capturedSegment.Should().BeNull();
    }

    [Fact]
    public async Task GetCalibrations_ReturnsEveryCalibrationWithItsEvidence()
    {
        var calibration = new SymbolCalibrationDto(
            "XAUUSD_M1_UTC02", 100.000m, 90, 100.000m, 100.000m, CalibrationStatus.Calibrated, DateTime.UtcNow);
        var serviceMock = new Mock<IBacktestReadService>();
        serviceMock
            .Setup(s => s.GetCalibrationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([calibration]);

        var result = await CreateSut(serviceMock).GetCalibrations(default);

        var body = (result.Result as OkObjectResult)!.Value as IReadOnlyList<SymbolCalibrationDto>;
        body!.Should().ContainSingle();
        body[0].SampleCount.Should().Be(90);
        body[0].PointValue.Should().Be(100.000m);
    }
}
