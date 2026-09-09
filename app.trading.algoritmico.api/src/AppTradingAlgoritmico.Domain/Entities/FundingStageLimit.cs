using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One stage of an Axi <see cref="Enums.GuardrailKind.StagedLossLimits"/> rulebook. Carries breach
/// fields ONLY — stage membership, transitions, and progression columns belong to a future
/// `axi-stage-tracking` capability, not here (`funding-guardrails` spec — "StagedLossLimits Stage
/// Rulebook"). Axi's breach consequence is quarantine with stage demotion, never termination —
/// that is rendered by the caller, never stored as a column on this entity.
/// </summary>
public class FundingStageLimit : BaseEntity
{
    /// <summary>The parent <see cref="BrokerRiskLimits"/> row this stage belongs to.</summary>
    public Guid BrokerRiskLimitsId { get; set; }

    public BrokerRiskLimits? BrokerRiskLimits { get; set; }

    /// <summary>1-based stage position. Unique within a given <see cref="BrokerRiskLimitsId"/>.</summary>
    public int StageOrdinal { get; set; }

    /// <summary>Human-readable stage label (e.g. "Stage 1", "Master").</summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>Max loss limit for this stage as a fraction of capital (e.g. 0.06 = 6%). Required.</summary>
    public decimal MaxLossLimitPct { get; set; }

    /// <summary>Profit target for this stage as a fraction of capital. Null when the stage has none.</summary>
    public decimal? ProfitTargetPct { get; set; }
}
