using AppTradingAlgoritmico.Application.DTOs.GridPresets;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IGridPresetService
{
    Task<IEnumerable<GridPresetDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates a new preset for the user. Throws ArgumentException if name is already taken.</summary>
    Task<GridPresetDto> CreateAsync(Guid userId, CreateGridPresetDto dto, CancellationToken ct = default);

    /// <summary>Deletes a preset scoped to the user. Throws KeyNotFoundException if not found.</summary>
    Task DeleteAsync(Guid userId, Guid presetId, CancellationToken ct = default);
}
