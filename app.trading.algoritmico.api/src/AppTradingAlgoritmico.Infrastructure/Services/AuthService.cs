using AppTradingAlgoritmico.Application.DTOs.Auth;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled.");

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateAccessToken(user, roles);

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        return new AuthResponseDto(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresIn: _tokenService.ExpiresInSeconds,
            Email: user.Email!,
            UserName: user.UserName!,
            Roles: roles
        );
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;

        // Invalidate refresh token on logout
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} logged out.", userId);
    }
}
