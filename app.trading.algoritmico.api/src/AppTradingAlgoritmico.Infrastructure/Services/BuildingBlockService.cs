using AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class BuildingBlockService(AppDbContext db, ISqxParserService parser) : IBuildingBlockService
{
    public async Task<IEnumerable<BuildingBlockDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.BuildingBlocks
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .Select(x => new BuildingBlockDto(x.Id, x.Name, x.Description, x.Type, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<BuildingBlockDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bb = await db.BuildingBlocks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"BuildingBlock {id} not found.");

        return new BuildingBlockDetailDto(bb.Id, bb.Name, bb.Description, bb.Type, bb.XmlConfig, bb.CreatedAt);
    }

    public async Task<BuildingBlockDto> CreateAsync(CreateBuildingBlockDto dto, Stream? sqbFile, CancellationToken ct = default)
    {
        string? xmlConfig = null;
        if (sqbFile is not null)
            xmlConfig = await parser.ParseSqbConfigAsync(sqbFile, ct);

        var entity = new BuildingBlock
        {
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            XmlConfig = xmlConfig,
            CreatedAt = DateTime.UtcNow
        };

        db.BuildingBlocks.Add(entity);
        await db.SaveChangesAsync(ct);

        return new BuildingBlockDto(entity.Id, entity.Name, entity.Description, entity.Type, entity.CreatedAt);
    }

    public async Task<BuildingBlockDto> UpdateAsync(Guid id, CreateBuildingBlockDto dto, Stream? sqbFile, CancellationToken ct = default)
    {
        var entity = await db.BuildingBlocks.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"BuildingBlock {id} not found.");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.UpdatedAt = DateTime.UtcNow;

        if (sqbFile is not null)
            entity.XmlConfig = await parser.ParseSqbConfigAsync(sqbFile, ct);

        await db.SaveChangesAsync(ct);

        return new BuildingBlockDto(entity.Id, entity.Name, entity.Description, entity.Type, entity.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.BuildingBlocks.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"BuildingBlock {id} not found.");

        db.BuildingBlocks.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
