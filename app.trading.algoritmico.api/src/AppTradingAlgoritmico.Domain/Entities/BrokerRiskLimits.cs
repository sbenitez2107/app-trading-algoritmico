using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// Prop-firm risk limits for a funding service, keyed by broker name. Configured ONCE per broker
/// and reused by every portfolio whose members trade on that broker. All percentages are decimals
/// (0.05 = 5%) and are USER-SOURCED — never hardcoded. <see cref="Verified"/> records whether the
/// user confirmed the numbers against the firm's live terms.
/// </summary>
public class BrokerRiskLimits : BaseEntity
{
    /// <summary>The broker name these limits apply to (matches <c>TradingAccount.Broker</c>).</summary>
    public string Broker { get; set; } = string.Empty;

    public FundingService FundingService { get; set; } = FundingService.Other;

    /// <summary>Max daily loss as a fraction of capital (e.g. 0.05 = 5%). Null = not set.</summary>
    public decimal? DailyLossLimitPct { get; set; }

    /// <summary>Max overall loss / drawdown as a fraction of capital (e.g. 0.10 = 10%). Null = not set.</summary>
    public decimal? MaxLossLimitPct { get; set; }

    /// <summary>Profit target as a fraction of capital (e.g. 0.10 = 10%). Null = not set.</summary>
    public decimal? ProfitTargetPct { get; set; }

    public DrawdownModel DrawdownModel { get; set; } = DrawdownModel.Static;

    /// <summary>
    /// Discriminates the rulebook this row follows. Defaults to <see cref="GuardrailKind.LossLimits"/>
    /// so every row created before this discriminator existed (and every migrated row) keeps today's
    /// breach-style behaviour unchanged.
    /// </summary>
    public GuardrailKind Kind { get; set; } = GuardrailKind.LossLimits;

    /// <summary>
    /// VarTarget only: monthly VaR-target ceiling as a fraction (e.g. 0.065 = 6.5%, Darwinex Zero's
    /// stated maximum — KB §2). Null for <see cref="GuardrailKind.LossLimits"/> rows.
    /// </summary>
    public decimal? TargetVarPct { get; set; }

    /// <summary>
    /// VarTarget only: monthly VaR-target floor as a fraction (e.g. 0.0325 = 3.25%, Darwinex Zero's
    /// stated operating-range floor — KB §2). Null for <see cref="GuardrailKind.LossLimits"/> rows.
    /// </summary>
    public decimal? VarFloorPct { get; set; }

    /// <summary>True once the user confirmed these numbers against the firm's live rulebook.</summary>
    public bool Verified { get; set; }
}
