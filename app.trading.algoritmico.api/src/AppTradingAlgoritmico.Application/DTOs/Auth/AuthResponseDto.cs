namespace AppTradingAlgoritmico.Application.DTOs.Auth;

public record AuthResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Email,
    string UserName,
    IEnumerable<string> Roles
);
