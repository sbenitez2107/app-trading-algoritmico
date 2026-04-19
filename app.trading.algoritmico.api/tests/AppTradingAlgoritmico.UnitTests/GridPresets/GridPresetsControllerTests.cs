using AppTradingAlgoritmico.Application.DTOs.GridPresets;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.WebAPI.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace AppTradingAlgoritmico.UnitTests.GridPresets;

/// <summary>
/// Tests for GridPresetsController — #4 column preset endpoints.
/// </summary>
public class GridPresetsControllerTests
{
    private static GridPresetsController CreateSut(Mock<IGridPresetService> serviceMock, Guid? userId = null)
    {
        var sut = new GridPresetsController(serviceMock.Object);
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return sut;
    }

    private static GridPresetDto MakePreset() => new(
        Guid.NewGuid(), "Performance", ["totalProfit"], ["totalProfit"], DateTime.UtcNow);

    [Fact]
    public async Task GetPresets_AuthenticatedUser_Returns200WithList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var presets = new[] { MakePreset() };
        var serviceMock = new Mock<IGridPresetService>();
        serviceMock.Setup(s => s.GetByUserAsync(userId, default))
                   .ReturnsAsync(presets);

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.GetPresets(default);

        // Assert
        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetPresets_NoUserId_Returns401()
    {
        // Arrange
        var serviceMock = new Mock<IGridPresetService>();
        var sut = CreateSut(serviceMock, null); // no user claim

        // Act
        var result = await sut.GetPresets(default);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreatePreset_ValidDto_Returns201()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var preset = MakePreset();
        var dto = new CreateGridPresetDto("Performance", ["totalProfit"], ["totalProfit"]);

        var serviceMock = new Mock<IGridPresetService>();
        serviceMock.Setup(s => s.CreateAsync(userId, dto, default))
                   .ReturnsAsync(preset);

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.CreatePreset(dto, default);

        // Assert
        var created = result.Result as CreatedAtActionResult;
        created.Should().NotBeNull();
        created!.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreatePreset_DuplicateName_Returns409Conflict()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new CreateGridPresetDto("Performance", ["totalProfit"], ["totalProfit"]);

        var serviceMock = new Mock<IGridPresetService>();
        serviceMock.Setup(s => s.CreateAsync(userId, dto, default))
                   .ThrowsAsync(new ArgumentException("A preset named 'Performance' already exists."));

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.CreatePreset(dto, default);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task DeletePreset_ExistingPreset_Returns204()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var presetId = Guid.NewGuid();
        var serviceMock = new Mock<IGridPresetService>();
        serviceMock.Setup(s => s.DeleteAsync(userId, presetId, default))
                   .Returns(Task.CompletedTask);

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.DeletePreset(presetId, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeletePreset_NotFound_Returns404()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var presetId = Guid.NewGuid();
        var serviceMock = new Mock<IGridPresetService>();
        serviceMock.Setup(s => s.DeleteAsync(userId, presetId, default))
                   .ThrowsAsync(new KeyNotFoundException("not found"));

        var sut = CreateSut(serviceMock, userId);

        // Act
        var result = await sut.DeletePreset(presetId, default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
