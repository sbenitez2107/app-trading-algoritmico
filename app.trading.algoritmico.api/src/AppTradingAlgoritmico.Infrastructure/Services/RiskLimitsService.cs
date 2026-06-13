using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class RiskLimitsService(AppDbContext db) : IRiskLimitsService
{
    public async Task<IReadOnlyList<BrokerRiskLimitsDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.BrokerRiskLimits
            .AsNoTracking()
            .OrderBy(x => x.Broker)
            .Select(x => ToDto(x))
            .ToListAsync(ct);
    }

    public async Task<BrokerRiskLimitsDto?> GetByBrokerAsync(string broker, CancellationToken ct = default)
    {
        var entity = await db.BrokerRiskLimits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Broker == broker, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<BrokerRiskLimitsDto> UpsertAsync(UpsertBrokerRiskLimitsDto dto, CancellationToken ct = default)
    {
        var broker = dto.Broker.Trim();
        if (string.IsNullOrWhiteSpace(broker))
            throw new ArgumentException("Broker is required.", nameof(dto));

        var entity = await db.BrokerRiskLimits.FirstOrDefaultAsync(x => x.Broker == broker, ct);
        if (entity is null)
        {
            entity = new BrokerRiskLimits { Broker = broker, CreatedAt = DateTime.UtcNow };
            db.BrokerRiskLimits.Add(entity);
        }
        else
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        entity.FundingService = dto.FundingService;
        entity.DailyLossLimitPct = dto.DailyLossLimitPct;
        entity.MaxLossLimitPct = dto.MaxLossLimitPct;
        entity.ProfitTargetPct = dto.ProfitTargetPct;
        entity.DrawdownModel = dto.DrawdownModel;
        entity.Verified = dto.Verified;

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static BrokerRiskLimitsDto ToDto(BrokerRiskLimits x) => new(
        x.Id, x.Broker, x.FundingService, x.DailyLossLimitPct, x.MaxLossLimitPct,
        x.ProfitTargetPct, x.DrawdownModel, x.Verified);
}
