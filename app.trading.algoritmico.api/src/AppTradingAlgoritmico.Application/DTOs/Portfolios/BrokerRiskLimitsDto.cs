using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Domain.Guardrails;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>
/// Prop-firm risk limits configured for a broker (USER-SOURCED). Percentages are decimals.
/// <see cref="Kind"/> determines which field set is valid: <see cref="GuardrailKind.LossLimits"/>
/// uses <see cref="DailyLossLimitPct"/>/<see cref="MaxLossLimitPct"/>/<see cref="ProfitTargetPct"/>/
/// <see cref="DrawdownModel"/>; <see cref="GuardrailKind.VarTarget"/> uses
/// <see cref="TargetVarPct"/>/<see cref="VarFloorPct"/>; <see cref="GuardrailKind.StagedLossLimits"/>
/// uses <see cref="Stages"/> only. No horizon field — the 30-day monthly VaR horizon is a vendor
/// constant, not user-sourced (see design's reconciliation note).
/// </summary>
/// <param name="RulebookMismatch">
/// COMPUTED, never persisted — true when <see cref="Kind"/> does not match the rulebook
/// <see cref="GuardrailShape.RequiredKindFor"/> demands for <see cref="FundingService"/>, or when
/// <see cref="FundingService"/> is <see cref="FundingService.Ftmo"/> with no <see cref="FtmoProduct"/>.
/// A computed member cannot be omitted or defaulted wrongly at a call site
/// (`funding-guardrails` spec — "Kind Bound to FundingService", "FtmoProduct Discriminator").
/// </param>
public sealed record BrokerRiskLimitsDto(
    Guid Id,
    string Broker,
    FundingService FundingService,
    GuardrailKind Kind,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel? DrawdownModel,
    decimal? TargetVarPct,
    decimal? VarFloorPct,
    bool Verified,
    FtmoProduct? FtmoProduct = null,
    IReadOnlyList<FundingStageLimitDto>? Stages = null)
{
    public bool RulebookMismatch
    {
        get
        {
            if (!GuardrailShape.IsShapeConsistent(FundingService, Kind))
                return true;

            return FundingService == FundingService.Ftmo && FtmoProduct is null;
        }
    }
}

/// <summary>
/// Create-or-update the limits for a broker (keyed by <see cref="Broker"/>).
/// </summary>
/// <param name="DrawdownModel">
/// Nullable so "absent" is expressible per kind: REQUIRED (non-null) for
/// <see cref="GuardrailKind.LossLimits"/>, REJECTED (must be null) for
/// <see cref="GuardrailKind.StagedLossLimits"/>, and normalized away for
/// <see cref="GuardrailKind.VarTarget"/> (`funding-guardrails` spec — "Kind Determines Valid
/// Field Set"). While this was non-nullable every payload was obliged to carry a value, so a
/// "reject when set" rule on a non-LossLimits kind would have fired on every payload and made
/// Axi unconfigurable.
/// </param>
public sealed record UpsertBrokerRiskLimitsDto(
    string Broker,
    FundingService FundingService,
    GuardrailKind Kind,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel? DrawdownModel,
    decimal? TargetVarPct,
    decimal? VarFloorPct,
    bool Verified,
    FtmoProduct? FtmoProduct = null,
    IReadOnlyList<UpsertFundingStageLimitDto>? Stages = null);
