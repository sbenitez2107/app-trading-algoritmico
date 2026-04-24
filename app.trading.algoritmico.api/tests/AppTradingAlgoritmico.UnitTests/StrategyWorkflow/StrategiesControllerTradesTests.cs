using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Unit tests for the strategy trades query endpoint — spec: mt-trade-import R12.
/// Route: GET api/strategies/{id}/trades.
/// Covers routing, status codes, and paged projection only.
/// Service logic is covered by TradeImportServiceTests.
/// </summary>
public class StrategiesControllerTradesTests
{
    private static StrategyTradeDto MakeTradeDto() => new(
        Id: Guid.NewGuid(),
        Ticket: 123456L,
        OpenTime: new DateTime(2026, 1, 1),
        CloseTime: new DateTime(2026, 1, 2),
        Type: "buy",
        Size: 0.10m,
        Item: "EURUSD",
        OpenPrice: 1.1000m,
        ClosePrice: 1.1050m,
        StopLoss: 1.0950m,
        TakeProfit: 1.1100m,
        Commission: -1m,
        Taxes: 0m,
        Swap: -0.5m,
        Profit: 50m,
        CloseReason: "TP",
        IsOpen: false);

    private static PagedResult<StrategyTradeDto> MakePagedTrades() =>
        new([MakeTradeDto(), MakeTradeDto()], TotalCount: 2, Page: 1, PageSize: 50);

    [Fact]
    public async Task GetTrades_ExistingStrategy_Returns200WithPagedResult()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var pagedResult = MakePagedTrades();

        var tradeMock = new Mock<ITradeImportService>();
        tradeMock
            .Setup(s => s.GetByStrategyAsync(
                strategyId,
                TradeStatusFilter.All,
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var sut = new StrategiesController(Mock.Of<IStrategyService>(), tradeMock.Object);

        // Act — spec R12 scenario 1
        var result = await sut.GetTrades(strategyId, "all", 1, 50, default);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var dto = okResult.Value as PagedResult<StrategyTradeDto>;
        dto!.TotalCount.Should().Be(2);
    }
}
