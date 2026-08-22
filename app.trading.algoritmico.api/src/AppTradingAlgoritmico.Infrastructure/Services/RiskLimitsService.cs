using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
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

        ValidateKindFields(dto);

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

        entity.Kind = dto.Kind;
        entity.FundingService = dto.FundingService;
        entity.DailyLossLimitPct = dto.DailyLossLimitPct;
        entity.MaxLossLimitPct = dto.MaxLossLimitPct;
        entity.ProfitTargetPct = dto.ProfitTargetPct;
        entity.DrawdownModel = dto.DrawdownModel;
        entity.TargetVarPct = dto.TargetVarPct;
        entity.VarFloorPct = dto.VarFloorPct;
        entity.Verified = dto.Verified;

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>
    /// Kind-aware validation (`funding-guardrails` spec — "Kind Determines Valid Field Set" and
    /// "VarTarget Percentage Validation"). A payload may only carry the fields its own kind defines,
    /// and a VarTarget payload must supply a valid, ordered percentage pair.
    /// </summary>
    private static void ValidateKindFields(UpsertBrokerRiskLimitsDto dto)
    {
        if (dto.Kind == GuardrailKind.LossLimits)
        {
            if (dto.TargetVarPct is not null || dto.VarFloorPct is not null)
                throw new ArgumentException("VarTarget fields are not valid for a LossLimits guardrail.", nameof(dto));
            return;
        }

        // VarTarget
        if (dto.DailyLossLimitPct is not null || dto.MaxLossLimitPct is not null || dto.ProfitTargetPct is not null)
            throw new ArgumentException("LossLimits fields are not valid for a VarTarget guardrail.", nameof(dto));

        if (dto.TargetVarPct is null || dto.VarFloorPct is null)
            throw new ArgumentException("TargetVarPct and VarFloorPct are both required for a VarTarget guardrail.", nameof(dto));

        if (dto.TargetVarPct is <= 0 or > 1 || dto.VarFloorPct is <= 0 or > 1)
            throw new ArgumentException("VarTarget percentages must be fractions in (0, 1].", nameof(dto));

        if (dto.VarFloorPct > dto.TargetVarPct)
            throw new ArgumentException("VarFloorPct cannot exceed TargetVarPct.", nameof(dto));
    }

    private static BrokerRiskLimitsDto ToDto(BrokerRiskLimits x) => new(
        x.Id, x.Broker, x.FundingService, x.Kind, x.DailyLossLimitPct, x.MaxLossLimitPct,
        x.ProfitTargetPct, x.DrawdownModel, x.TargetVarPct, x.VarFloorPct, x.Verified);
}
