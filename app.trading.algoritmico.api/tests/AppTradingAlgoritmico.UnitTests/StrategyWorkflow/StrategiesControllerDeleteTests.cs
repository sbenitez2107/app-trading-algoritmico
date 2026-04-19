using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategiesController.Delete — #3 hard delete endpoint.
/// </summary>
public class StrategiesControllerDeleteTests
{
    [Fact]
    public async Task Delete_ExistingStrategy_Returns204NoContent()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.DeleteAsync(strategyId, default))
                   .Returns(Task.CompletedTask);

        var sut = new StrategiesController(serviceMock.Object);

        // Act
        var result = await sut.Delete(strategyId, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        ((NoContentResult)result).StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task Delete_NonExistentStrategy_Returns404NotFound()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.DeleteAsync(strategyId, default))
                   .ThrowsAsync(new KeyNotFoundException("Strategy not found."));

        var sut = new StrategiesController(serviceMock.Object);

        // Act
        var result = await sut.Delete(strategyId, default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
