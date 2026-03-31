using AppTradingAlgoritmico.Application.DTOs.Auth;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Auth;

public class AuthServiceTests : TestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userManagerMock = CreateMockUserManager();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _sut = new AuthService(
            _userManagerMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var user = CreateUser(email: "sbenitez2107@gmail.com");
        var request = new LoginRequestDto("sbenitez2107@gmail.com", "Trading@2024!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);
        _userManagerMock.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(["Admin"]);
        _tokenServiceMock.Setup(t => t.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>()))
            .Returns("mocked.jwt.token");
        _tokenServiceMock.Setup(t => t.ExpiresInSeconds).Returns(3600);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("mocked.jwt.token");
        result.TokenType.Should().Be("Bearer");
        result.Email.Should().Be(user.Email);
        result.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsUnauthorized()
    {
        // Arrange
        var request = new LoginRequestDto("notfound@trading.local", "AnyPass@1");

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = async () => await _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        // Arrange
        var user = CreateUser();
        var request = new LoginRequestDto(user.Email!, "WrongPass@1");

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsUnauthorized()
    {
        // Arrange
        var user = CreateUser(isActive: false);
        var request = new LoginRequestDto(user.Email!, "Trading@2024!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        // Act
        var act = async () => await _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Account is disabled.");
    }
}
