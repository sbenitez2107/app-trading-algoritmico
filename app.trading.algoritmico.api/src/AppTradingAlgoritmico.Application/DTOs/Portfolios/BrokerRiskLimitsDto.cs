using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>
/// Prop-firm risk limits configured for a broker (USER-SOURCED). Percentages are decimals.
/// <see cref="Kind"/> determines which field set is valid: <see cref="GuardrailKind.LossLimits"/>
/// uses <see cref="DailyLossLimitPct"/>/<see cref="MaxLossLimitPct"/>/<see cref="ProfitTargetPct"/>/
/// <see cref="DrawdownModel"/>; <see cref="GuardrailKind.VarTarget"/> uses
/// <see cref="TargetVarPct"/>/<see cref="VarFloorPct"/>. No horizon field — the 30-day monthly VaR
/// horizon is a vendor constant, not user-sourced (see design's reconciliation note).
/// </summary>
public sealed record BrokerRiskLimitsDto(
    Guid Id,
    string Broker,
    FundingService FundingService,
    GuardrailKind Kind,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel DrawdownModel,
    decimal? TargetVarPct,
    decimal? VarFloorPct,
    bool Verified);

/// <summary>Create-or-update the limits for a broker (keyed by <see cref="Broker"/>).</summary>
public sealed record UpsertBrokerRiskLimitsDto(
    string Broker,
    FundingService FundingService,
    GuardrailKind Kind,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel DrawdownModel,
    decimal? TargetVarPct,
    decimal? VarFloorPct,
    bool Verified);
