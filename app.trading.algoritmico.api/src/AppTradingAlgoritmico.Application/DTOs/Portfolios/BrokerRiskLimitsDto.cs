using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>Prop-firm risk limits configured for a broker (USER-SOURCED). Percentages are decimals.</summary>
public sealed record BrokerRiskLimitsDto(
    Guid Id,
    string Broker,
    FundingService FundingService,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel DrawdownModel,
    bool Verified);

/// <summary>Create-or-update the limits for a broker (keyed by <see cref="Broker"/>).</summary>
public sealed record UpsertBrokerRiskLimitsDto(
    string Broker,
    FundingService FundingService,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    decimal? ProfitTargetPct,
    DrawdownModel DrawdownModel,
    bool Verified);
