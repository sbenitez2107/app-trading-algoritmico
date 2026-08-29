using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Unit tests for the portfolio combined-trades query endpoint.
/// Route: GET api/portfolios/{id}/trades.
/// Covers routing and status codes only — service logic is covered by PortfolioServiceTradesTests.
/// </summary>
public class PortfoliosControllerTradesTests
{
    private static PortfolioTradeDto MakeTradeDto() => new(
        Id: Guid.NewGuid(),
        StrategyId: Guid.NewGuid(),
        StrategyName: "Strategy A",
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

    private static PagedResult<PortfolioTradeDto> MakePagedTrades() =>
        new([MakeTradeDto(), MakeTradeDto()], TotalCount: 2, Page: 1, PageSize: 50);

    [Fact]
    public async Task GetTrades_ExistingPortfolio_Returns200WithPagedResult()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var pagedResult = MakePagedTrades();

        var serviceMock = new Mock<IPortfolioService>();
        serviceMock
            .Setup(s => s.GetTradesAsync(
                portfolioId,
                TradeStatusFilter.All,
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var sut = new PortfoliosController(serviceMock.Object);

        // Act
        var result = await sut.GetTrades(portfolioId, "all", 1, 50, default);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var dto = okResult.Value as PagedResult<PortfolioTradeDto>;
        dto!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTrades_InvalidStatus_Returns400BadRequest()
    {
        // Arrange
        var sut = new PortfoliosController(Mock.Of<IPortfolioService>());

        // Act
        var result = await sut.GetTrades(Guid.NewGuid(), "bogus", 1, 50, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTrades_PortfolioNotFound_Returns404NotFound()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();

        var serviceMock = new Mock<IPortfolioService>();
        serviceMock
            .Setup(s => s.GetTradesAsync(
                portfolioId,
                It.IsAny<TradeStatusFilter>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Portfolio {portfolioId} not found."));

        var sut = new PortfoliosController(serviceMock.Object);

        // Act
        var result = await sut.GetTrades(portfolioId, "all", 1, 50, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
