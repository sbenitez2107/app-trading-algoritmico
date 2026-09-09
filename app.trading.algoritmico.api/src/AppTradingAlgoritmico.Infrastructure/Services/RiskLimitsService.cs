using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Domain.Guardrails;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class RiskLimitsService(AppDbContext db) : IRiskLimitsService
{
    public async Task<IReadOnlyList<BrokerRiskLimitsDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await db.BrokerRiskLimits
            .AsNoTracking()
            .Include(x => x.Stages)
            .OrderBy(x => x.Broker)
            .ToListAsync(ct);

        return entities.Select(ToDto).ToList();
    }

    public async Task<BrokerRiskLimitsDto?> GetByBrokerAsync(string broker, CancellationToken ct = default)
    {
        var entity = await db.BrokerRiskLimits.AsNoTracking()
            .Include(x => x.Stages)
            .FirstOrDefaultAsync(x => x.Broker == broker, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<BrokerRiskLimitsDto> UpsertAsync(UpsertBrokerRiskLimitsDto dto, CancellationToken ct = default)
    {
        var broker = dto.Broker.Trim();
        if (string.IsNullOrWhiteSpace(broker))
            throw new ArgumentException("Broker is required.", nameof(dto));

        if (!GuardrailShape.IsShapeConsistent(dto.FundingService, dto.Kind))
            throw new ArgumentException(
                $"{dto.FundingService} requires {GuardrailShape.RequiredKindFor(dto.FundingService)}, got {dto.Kind}.",
                nameof(dto));

        if (!GuardrailShape.IsFtmoProductConsistent(dto.FtmoProduct, dto.DrawdownModel))
            throw new ArgumentException(
                $"FtmoProduct {dto.FtmoProduct} requires a different DrawdownModel than {dto.DrawdownModel}.",
                nameof(dto));

        ValidateKindFields(dto);

        // D4: for VarTarget, DrawdownModel is NORMALIZED to null rather than rejected. The
        // asymmetry against StagedLossLimits (which ValidateKindFields rejects outright) is
        // deliberate, not an oversight: `RiskLimitsServiceTests.VarTargetDto` passes
        // `DrawdownModel.Static` unconditionally and
        // `UpsertAsync_VarTargetCarryingDrawdownModelTrailing_PersistsNull` pins normalization as
        // the observable behaviour, so turning VarTarget into a rejection would break that fence.
        // Do not "make this consistent" — the two kinds differ on purpose.
        var normalizedDrawdownModel = dto.Kind == GuardrailKind.LossLimits ? dto.DrawdownModel : (DrawdownModel?)null;

        var entity = await db.BrokerRiskLimits
            .Include(x => x.Stages)
            .FirstOrDefaultAsync(x => x.Broker == broker, ct);
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
        entity.DrawdownModel = normalizedDrawdownModel;
        entity.FtmoProduct = dto.FtmoProduct;
        entity.TargetVarPct = dto.TargetVarPct;
        entity.VarFloorPct = dto.VarFloorPct;
        entity.Verified = dto.Verified;

        // Replace-upsert: the write path is the single source of truth for the stage collection.
        entity.Stages.Clear();
        if (dto.Stages is { Count: > 0 })
        {
            foreach (var s in dto.Stages)
            {
                entity.Stages.Add(new FundingStageLimit
                {
                    BrokerRiskLimitsId = entity.Id,
                    StageOrdinal = s.StageOrdinal,
                    StageName = s.StageName,
                    MaxLossLimitPct = s.MaxLossLimitPct,
                    ProfitTargetPct = s.ProfitTargetPct,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>
    /// Kind-aware validation (`funding-guardrails` spec — "Kind Determines Valid Field Set" and
    /// "VarTarget Percentage Validation" and "StagedLossLimits Stage Rulebook"). A payload may only
    /// carry the fields its own kind defines, a VarTarget payload must supply a valid, ordered
    /// percentage pair, and a StagedLossLimits payload must supply a non-empty, ordinal-unique stage
    /// collection while leaving every parent scalar null.
    /// </summary>
    private static void ValidateKindFields(UpsertBrokerRiskLimitsDto dto)
    {
        if (dto.Kind == GuardrailKind.LossLimits)
        {
            if (dto.TargetVarPct is not null || dto.VarFloorPct is not null)
                throw new ArgumentException("VarTarget fields are not valid for a LossLimits guardrail.", nameof(dto));
            if (dto.Stages is { Count: > 0 })
                throw new ArgumentException("Stages are not valid for a LossLimits guardrail.", nameof(dto));
            if (dto.DrawdownModel is null)
                throw new ArgumentException("DrawdownModel is required for a LossLimits guardrail.", nameof(dto));
            return;
        }

        if (dto.Kind == GuardrailKind.StagedLossLimits)
        {
            if (dto.DailyLossLimitPct is not null || dto.MaxLossLimitPct is not null || dto.ProfitTargetPct is not null
                || dto.TargetVarPct is not null || dto.VarFloorPct is not null)
                throw new ArgumentException("Only the stage collection is valid for a StagedLossLimits guardrail.", nameof(dto));

            // Rejected, not normalized — unlike VarTarget above. No fence test supplies a
            // DrawdownModel on a StagedLossLimits payload, so the spec's rejection is reachable
            // here ("DrawdownModel rejected on a StagedLossLimits payload").
            if (dto.DrawdownModel is not null)
                throw new ArgumentException("DrawdownModel is not valid for a StagedLossLimits guardrail.", nameof(dto));

            if (dto.Stages is not { Count: > 0 })
                throw new ArgumentException("At least one stage is required for a StagedLossLimits guardrail.", nameof(dto));

            foreach (var stage in dto.Stages)
            {
                // Same (0, 1] fraction bound the VarTarget pair enforces below — a stage written
                // as `7` instead of `0.07` is a 700% limit that could never be breached. Bounding
                // MaxLossLimitPct alone would just relocate the hole to ProfitTargetPct.
                if (stage.MaxLossLimitPct is <= 0 or > 1)
                    throw new ArgumentException("Stage MaxLossLimitPct must be a fraction in (0, 1].", nameof(dto));
                if (stage.ProfitTargetPct is not null && stage.ProfitTargetPct is <= 0 or > 1)
                    throw new ArgumentException("Stage ProfitTargetPct must be a fraction in (0, 1].", nameof(dto));
            }

            var duplicateOrdinal = dto.Stages
                .GroupBy(s => s.StageOrdinal)
                .Any(g => g.Count() > 1);
            if (duplicateOrdinal)
                throw new ArgumentException("StageOrdinal must be unique within a StagedLossLimits guardrail.", nameof(dto));

            return;
        }

        // VarTarget
        if (dto.DailyLossLimitPct is not null || dto.MaxLossLimitPct is not null || dto.ProfitTargetPct is not null)
            throw new ArgumentException("LossLimits fields are not valid for a VarTarget guardrail.", nameof(dto));

        // Without this the unconditional stage write in UpsertAsync would parent Axi stage rows to
        // a Darwinex Zero guardrail (`BrokerRiskLimits.Stages` — "Empty for every other kind").
        if (dto.Stages is { Count: > 0 })
            throw new ArgumentException("Stages are not valid for a VarTarget guardrail.", nameof(dto));

        if (dto.TargetVarPct is null || dto.VarFloorPct is null)
            throw new ArgumentException("TargetVarPct and VarFloorPct are both required for a VarTarget guardrail.", nameof(dto));

        if (dto.TargetVarPct is <= 0 or > 1 || dto.VarFloorPct is <= 0 or > 1)
            throw new ArgumentException("VarTarget percentages must be fractions in (0, 1].", nameof(dto));

        if (dto.VarFloorPct > dto.TargetVarPct)
            throw new ArgumentException("VarFloorPct cannot exceed TargetVarPct.", nameof(dto));
    }

    private static BrokerRiskLimitsDto ToDto(BrokerRiskLimits x) => new(
        x.Id, x.Broker, x.FundingService, x.Kind, x.DailyLossLimitPct, x.MaxLossLimitPct,
        x.ProfitTargetPct, x.DrawdownModel, x.TargetVarPct, x.VarFloorPct, x.Verified,
        x.FtmoProduct,
        x.Stages
            .OrderBy(s => s.StageOrdinal)
            .Select(s => new FundingStageLimitDto(s.StageOrdinal, s.StageName, s.MaxLossLimitPct, s.ProfitTargetPct))
            .ToList());
}
