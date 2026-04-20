using AppTradingAlgoritmico.Application.DTOs.GridPresets;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IGridPresetService
{
    Task<IEnumerable<GridPresetDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates a new preset for the user. Throws ArgumentException if name is already taken.</summary>
    Task<GridPresetDto> CreateAsync(Guid userId, CreateGridPresetDto dto, CancellationToken ct = default);

    /// <summary>Overwrites an existing preset's visible columns and order. Name is preserved. Throws KeyNotFoundException if not found or not owned by the user.</summary>
    Task<GridPresetDto> UpdateAsync(Guid userId, Guid presetId, UpdateGridPresetDto dto, CancellationToken ct = default);

    /// <summary>Deletes a preset scoped to the user. Throws KeyNotFoundException if not found.</summary>
    Task DeleteAsync(Guid userId, Guid presetId, CancellationToken ct = default);
}
