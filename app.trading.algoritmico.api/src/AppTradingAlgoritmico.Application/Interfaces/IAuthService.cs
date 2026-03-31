using AppTradingAlgoritmico.Application.DTOs.Auth;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
}
