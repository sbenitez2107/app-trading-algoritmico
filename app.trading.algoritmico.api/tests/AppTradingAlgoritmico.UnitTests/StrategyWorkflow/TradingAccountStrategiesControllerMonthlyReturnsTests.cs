using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for TradingAccountStrategiesController.GetMonthlyReturns — controller-level
/// concerns only (status codes, error mapping). Computation is covered by
/// StrategyServiceMonthlyReturnsByAccountTests.
/// </summary>
public class TradingAccountStrategiesControllerMonthlyReturnsTests
{
    [Fact]
    public async Task GetMonthlyReturns_ExistingAccount_Returns200WithRows()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var rows = new List<StrategyMonthlyReturnsDto>
        {
            new(Guid.NewGuid(), "Alpha", "EURUSD",
                [new MonthlyReturnDto(2026, 1, 10_000m, 10_150m, 150m, 0.015m, 2)])
        };

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetMonthlyReturnsByAccountAsync(accountId, default))
                   .ReturnsAsync(rows);

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act
        var result = await sut.GetMonthlyReturns(accountId, default);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var dto = okResult.Value as IReadOnlyList<StrategyMonthlyReturnsDto>;
        dto.Should().HaveCount(1);
        dto![0].Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetMonthlyReturns_AccountNotFound_Returns404()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetMonthlyReturnsByAccountAsync(accountId, default))
                   .ThrowsAsync(new KeyNotFoundException("not found"));

        var sut = new TradingAccountStrategiesController(serviceMock.Object);

        // Act
        var result = await sut.GetMonthlyReturns(accountId, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
