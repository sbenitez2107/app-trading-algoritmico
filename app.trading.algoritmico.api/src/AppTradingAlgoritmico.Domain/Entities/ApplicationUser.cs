using Microsoft.AspNetCore.Identity;

namespace AppTradingAlgoritmico.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PreferredTheme { get; set; }
}
