namespace AppTradingAlgoritmico.Application.DTOs.Users;

public record UserDto(
    Guid Id,
    string Email,
    string UserName,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles
);
