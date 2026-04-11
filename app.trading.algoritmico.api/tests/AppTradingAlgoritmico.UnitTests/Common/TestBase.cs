using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.Common;

/// <summary>
/// Base class with shared builder helpers for all unit tests.
/// </summary>
public abstract class TestBase
{
    protected static ApplicationUser CreateUser(
        string email = "test@trading.local",
        string firstName = "Test",
        string lastName = "User",
        bool isActive = true,
        string? preferredLanguage = null,
        string? preferredTheme = null)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            IsActive = isActive,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            PreferredLanguage = preferredLanguage,
            PreferredTheme = preferredTheme
        };
    }

    protected static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
