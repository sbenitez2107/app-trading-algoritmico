using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class StrategyService(AppDbContext db) : IStrategyService
{
    public async Task<PagedResult<StrategyDto>> GetByStageAsync(
        Guid batchId, Guid stageId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.Strategies
            .AsNoTracking()
            .Where(x => x.BatchStageId == stageId && x.BatchStage.BatchId == batchId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StrategyDto(
                x.Id, x.Name, x.Pseudocode,
                x.SharpeRatio, x.ReturnDrawdownRatio, x.WinRate, x.ProfitFactor,
                x.TotalTrades, x.NetProfit, x.MaxDrawdown, x.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<StrategyDto>(items, totalCount, page, pageSize);
    }

    public async Task<StrategyDto> UpdateKpisAsync(Guid strategyId, UpdateStrategyKpisDto dto, CancellationToken ct = default)
    {
        var entity = await db.Strategies.FindAsync([strategyId], ct)
            ?? throw new KeyNotFoundException($"Strategy {strategyId} not found.");

        if (dto.SharpeRatio.HasValue) entity.SharpeRatio = dto.SharpeRatio.Value;
        if (dto.ReturnDrawdownRatio.HasValue) entity.ReturnDrawdownRatio = dto.ReturnDrawdownRatio.Value;
        if (dto.WinRate.HasValue) entity.WinRate = dto.WinRate.Value;
        if (dto.ProfitFactor.HasValue) entity.ProfitFactor = dto.ProfitFactor.Value;
        if (dto.TotalTrades.HasValue) entity.TotalTrades = dto.TotalTrades.Value;
        if (dto.NetProfit.HasValue) entity.NetProfit = dto.NetProfit.Value;
        if (dto.MaxDrawdown.HasValue) entity.MaxDrawdown = dto.MaxDrawdown.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new StrategyDto(
            entity.Id, entity.Name, entity.Pseudocode,
            entity.SharpeRatio, entity.ReturnDrawdownRatio, entity.WinRate, entity.ProfitFactor,
            entity.TotalTrades, entity.NetProfit, entity.MaxDrawdown, entity.CreatedAt);
    }
}
