using AppTradingAlgoritmico.Application.DTOs.UserPreferences;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class UserPreferencesService(UserManager<ApplicationUser> userManager) : IUserPreferencesService
{
    private static readonly HashSet<string> ValidLanguages = ["en", "es"];
    private static readonly HashSet<string> ValidThemes = ["light", "dark"];

    public async Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        return new UserPreferencesDto(
            Language: user.PreferredLanguage ?? "es",
            Theme: user.PreferredTheme ?? "dark"
        );
    }

    public async Task<UserPreferencesDto> UpdateAsync(
        Guid userId,
        UpdateUserPreferencesDto dto,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (dto.Language is not null)
        {
            if (!ValidLanguages.Contains(dto.Language))
                throw new ArgumentException($"Invalid language '{dto.Language}'. Allowed: {string.Join(", ", ValidLanguages)}");

            user.PreferredLanguage = dto.Language;
        }

        if (dto.Theme is not null)
        {
            if (!ValidThemes.Contains(dto.Theme))
                throw new ArgumentException($"Invalid theme '{dto.Theme}'. Allowed: {string.Join(", ", ValidThemes)}");

            user.PreferredTheme = dto.Theme;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return new UserPreferencesDto(
            Language: user.PreferredLanguage ?? "es",
            Theme: user.PreferredTheme ?? "dark"
        );
    }
}
