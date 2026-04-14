using AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class AnalyzerRuleService(AppDbContext db) : IAnalyzerRuleService
{
    public async Task<IEnumerable<AnalyzerRuleDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.AnalyzerRules
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .Select(x => new AnalyzerRuleDto(
                x.Id, x.Name, x.Description, x.Priority, x.IsActive, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<AnalyzerRuleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.AnalyzerRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"AnalyzerRule {id} not found.");

        return new AnalyzerRuleDto(
            entity.Id, entity.Name, entity.Description, entity.Priority,
            entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AnalyzerRuleDto> CreateAsync(CreateAnalyzerRuleDto dto, CancellationToken ct = default)
    {
        var entity = new AnalyzerRule
        {
            Name = dto.Name,
            Description = dto.Description,
            Priority = dto.Priority,
            CreatedAt = DateTime.UtcNow
        };

        db.AnalyzerRules.Add(entity);
        await db.SaveChangesAsync(ct);

        return new AnalyzerRuleDto(
            entity.Id, entity.Name, entity.Description, entity.Priority,
            entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AnalyzerRuleDto> UpdateAsync(Guid id, UpdateAnalyzerRuleDto dto, CancellationToken ct = default)
    {
        var entity = await db.AnalyzerRules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"AnalyzerRule {id} not found.");

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new AnalyzerRuleDto(
            entity.Id, entity.Name, entity.Description, entity.Priority,
            entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.AnalyzerRules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"AnalyzerRule {id} not found.");

        db.AnalyzerRules.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
