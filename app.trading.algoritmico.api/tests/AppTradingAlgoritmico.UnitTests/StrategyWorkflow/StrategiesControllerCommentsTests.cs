using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategiesController comment endpoints — bitácora trackable feature.
/// </summary>
public class StrategiesControllerCommentsTests
{
    private static StrategiesController CreateSut(Mock<IStrategyService> serviceMock, string? userId = null)
    {
        var sut = new StrategiesController(serviceMock.Object);
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return sut;
    }

    private static StrategyCommentDto MakeCommentDto() => new(
        Guid.NewGuid(), "Some insight about this strategy", DateTime.UtcNow, "user-1");

    // --- GetComments ---

    [Fact]
    public async Task GetComments_ExistingStrategy_ReturnsOkWithList()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var comments = new[] { MakeCommentDto() };
        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetCommentsAsync(strategyId, default))
                   .ReturnsAsync(comments);

        var sut = CreateSut(serviceMock);

        // Act
        var result = await sut.GetComments(strategyId, default);

        // Assert
        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(comments);
    }

    [Fact]
    public async Task GetComments_NonExistentStrategy_Returns404()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.GetCommentsAsync(strategyId, default))
                   .ThrowsAsync(new KeyNotFoundException("Strategy not found."));

        var sut = CreateSut(serviceMock);

        // Act
        var result = await sut.GetComments(strategyId, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // --- AddComment ---

    [Fact]
    public async Task AddComment_Valid_Returns201WithDto()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var userId = "user-1";
        var dto = new CreateStrategyCommentDto("Great strategy!");
        var created = MakeCommentDto();

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddCommentAsync(strategyId, dto.Content, userId, default))
                   .ReturnsAsync(created);

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.AddComment(strategyId, dto, default);

        // Assert
        var createdResult = result.Result as CreatedResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task AddComment_EmptyContent_Returns400()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var userId = "user-1";
        var dto = new CreateStrategyCommentDto("");

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddCommentAsync(strategyId, dto.Content, userId, default))
                   .ThrowsAsync(new ArgumentException("Content cannot be empty."));

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.AddComment(strategyId, dto, default);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddComment_NonExistentStrategy_Returns404()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var userId = "user-1";
        var dto = new CreateStrategyCommentDto("Valid content");

        var serviceMock = new Mock<IStrategyService>();
        serviceMock.Setup(s => s.AddCommentAsync(strategyId, dto.Content, userId, default))
                   .ThrowsAsync(new KeyNotFoundException("Strategy not found."));

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.AddComment(strategyId, dto, default);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
