using AppTradingAlgoritmico.Application.DTOs.GridPresets;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class GridPresetService(AppDbContext db) : IGridPresetService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IEnumerable<GridPresetDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var presets = await db.StrategyGridPresets
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return presets.Select(ToDto);
    }

    public async Task<GridPresetDto> CreateAsync(Guid userId, CreateGridPresetDto dto, CancellationToken ct = default)
    {
        var nameExists = await db.StrategyGridPresets
            .AnyAsync(p => p.UserId == userId && p.Name == dto.Name, ct);

        if (nameExists)
            throw new ArgumentException($"A preset named '{dto.Name}' already exists for this user.");

        var entity = new StrategyGridPreset
        {
            Name = dto.Name,
            UserId = userId,
            VisibleColumnsJson = JsonSerializer.Serialize(dto.VisibleColumns, JsonOpts),
            ColumnOrderJson = JsonSerializer.Serialize(dto.ColumnOrder, JsonOpts),
            CreatedAt = DateTime.UtcNow
        };

        db.StrategyGridPresets.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<GridPresetDto> UpdateAsync(
        Guid userId, Guid presetId, UpdateGridPresetDto dto, CancellationToken ct = default)
    {
        var entity = await db.StrategyGridPresets
            .FirstOrDefaultAsync(p => p.Id == presetId && p.UserId == userId, ct)
            ?? throw new KeyNotFoundException($"Grid preset {presetId} not found for this user.");

        entity.VisibleColumnsJson = JsonSerializer.Serialize(dto.VisibleColumns, JsonOpts);
        entity.ColumnOrderJson = JsonSerializer.Serialize(dto.ColumnOrder, JsonOpts);
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid userId, Guid presetId, CancellationToken ct = default)
    {
        var entity = await db.StrategyGridPresets
            .FirstOrDefaultAsync(p => p.Id == presetId && p.UserId == userId, ct)
            ?? throw new KeyNotFoundException($"Grid preset {presetId} not found for this user.");

        db.StrategyGridPresets.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static GridPresetDto ToDto(StrategyGridPreset entity)
    {
        var visibleColumns = JsonSerializer.Deserialize<string[]>(entity.VisibleColumnsJson, JsonOpts) ?? [];
        var columnOrder = JsonSerializer.Deserialize<string[]>(entity.ColumnOrderJson, JsonOpts) ?? [];
        return new GridPresetDto(entity.Id, entity.Name, visibleColumns, columnOrder, entity.CreatedAt);
    }
}
