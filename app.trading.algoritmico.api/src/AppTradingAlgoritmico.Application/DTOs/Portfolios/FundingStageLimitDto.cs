namespace AppTradingAlgoritmico.Application.DTOs.Portfolios;

/// <summary>One stage of an Axi <see cref="Domain.Enums.GuardrailKind.StagedLossLimits"/> rulebook.</summary>
public sealed record FundingStageLimitDto(
    int StageOrdinal,
    string StageName,
    decimal MaxLossLimitPct,
    decimal? ProfitTargetPct);

/// <summary>Write payload for one stage — replace-upserted as a whole collection per parent row.</summary>
public sealed record UpsertFundingStageLimitDto(
    int StageOrdinal,
    string StageName,
    decimal MaxLossLimitPct,
    decimal? ProfitTargetPct);
