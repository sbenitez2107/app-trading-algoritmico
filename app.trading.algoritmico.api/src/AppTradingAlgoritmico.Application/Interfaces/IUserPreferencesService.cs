using AppTradingAlgoritmico.Application.DTOs.UserPreferences;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IUserPreferencesService
{
    Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserPreferencesDto> UpdateAsync(Guid userId, UpdateUserPreferencesDto dto, CancellationToken ct = default);
}
