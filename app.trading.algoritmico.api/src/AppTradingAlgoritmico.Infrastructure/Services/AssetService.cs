using AppTradingAlgoritmico.Application.DTOs.Assets;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class AssetService(AppDbContext db) : IAssetService
{
    public async Task<IEnumerable<AssetDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Assets
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AssetDto(x.Id, x.Name, x.Symbol, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AssetDto> CreateAsync(CreateAssetDto dto, CancellationToken ct = default)
    {
        var exists = await db.Assets.AnyAsync(x => x.Symbol == dto.Symbol, ct);
        if (exists)
            throw new ArgumentException($"Asset with symbol '{dto.Symbol}' already exists.");

        var entity = new Asset
        {
            Name = dto.Name,
            Symbol = dto.Symbol,
            CreatedAt = DateTime.UtcNow
        };

        db.Assets.Add(entity);
        await db.SaveChangesAsync(ct);

        return new AssetDto(entity.Id, entity.Name, entity.Symbol, entity.CreatedAt);
    }
}
