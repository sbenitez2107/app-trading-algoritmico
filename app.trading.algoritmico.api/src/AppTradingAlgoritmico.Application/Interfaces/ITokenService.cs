using AppTradingAlgoritmico.Domain.Entities;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    int ExpiresInSeconds { get; }
}
