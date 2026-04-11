using AppTradingAlgoritmico.Application.DTOs.UserPreferences;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.UserPreferences;

public class UserPreferencesServiceTests : TestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly UserPreferencesService _sut;

    public UserPreferencesServiceTests()
    {
        _userManagerMock = CreateMockUserManager();
        _sut = new UserPreferencesService(_userManagerMock.Object);
    }

    [Fact]
    public async Task GetAsync_UserWithNoPreferences_ReturnsDefaults()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetAsync(user.Id);

        // Assert
        result.Language.Should().Be("es");
        result.Theme.Should().Be("dark");
    }

    [Fact]
    public async Task GetAsync_UserWithPreferences_ReturnsStoredValues()
    {
        // Arrange
        var user = CreateUser(preferredLanguage: "en", preferredTheme: "light");
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetAsync(user.Id);

        // Assert
        result.Language.Should().Be("en");
        result.Theme.Should().Be("light");
    }

    [Fact]
    public async Task GetAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _sut.GetAsync(userId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ValidLanguage_UpdatesAndReturns()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var dto = new UpdateUserPreferencesDto(Language: "en", Theme: null);

        // Act
        var result = await _sut.UpdateAsync(user.Id, dto);

        // Assert
        result.Language.Should().Be("en");
        result.Theme.Should().Be("dark");
        user.PreferredLanguage.Should().Be("en");
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ValidTheme_UpdatesAndReturns()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var dto = new UpdateUserPreferencesDto(Language: null, Theme: "light");

        // Act
        var result = await _sut.UpdateAsync(user.Id, dto);

        // Assert
        result.Theme.Should().Be("light");
        result.Language.Should().Be("es");
    }

    [Fact]
    public async Task UpdateAsync_BothFields_UpdatesBoth()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var dto = new UpdateUserPreferencesDto(Language: "en", Theme: "light");

        // Act
        var result = await _sut.UpdateAsync(user.Id, dto);

        // Assert
        result.Language.Should().Be("en");
        result.Theme.Should().Be("light");
    }

    [Fact]
    public async Task UpdateAsync_InvalidLanguage_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        var dto = new UpdateUserPreferencesDto(Language: "fr", Theme: null);

        // Act
        var act = async () => await _sut.UpdateAsync(user.Id, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid language*");
    }

    [Fact]
    public async Task UpdateAsync_InvalidTheme_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateUser();
        _userManagerMock.Setup(m => m.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        var dto = new UpdateUserPreferencesDto(Language: null, Theme: "blue");

        // Act
        var act = async () => await _sut.UpdateAsync(user.Id, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid theme*");
    }

    [Fact]
    public async Task UpdateAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var dto = new UpdateUserPreferencesDto(Language: "en", Theme: null);

        // Act
        var act = async () => await _sut.UpdateAsync(userId, dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
