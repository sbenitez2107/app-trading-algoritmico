using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>One run occupying one of a strategy's two slots.</summary>
public sealed record BacktestRunSummaryDto(
    Guid Id,
    string SourceFileName,
    string? Symbol,
    BacktestRunKind Kind,
    int TradeCount,
    DateTime CreatedAt);

/// <summary>
/// A strategy's walk-forward export. <c>DeployParameters</c>/<c>EvaluationParameters</c> are
/// surfaced verbatim because they are the ONLY cross-check available against a run's declared
/// kind — nothing in a trade-list file identifies the parameters that produced it (design.md D11).
/// </summary>
public sealed record WalkForwardExportSummaryDto(
    Guid Id,
    string SourceFileName,
    DateTime OosFromDate,
    int WindowCount,
    string DeployParameters,
    string EvaluationParameters,
    DateTime CreatedAt);

/// <summary>
/// Everything imported for one strategy: the two slots and the walk-forward export.
/// <para>
/// Deliberately carries NO readiness marker. Readiness is derived once, server-side, for a whole
/// page of the strategies grid (design.md D12); deriving it a second time here would be a second
/// definition of the same rule, free to disagree with the first. This endpoint reports what
/// EXISTS; the grid reports what it MEANS.
/// </para>
/// </summary>
public sealed record StrategyBacktestsDto(
    Guid StrategyId,
    BacktestRunSummaryDto? Deploy,
    BacktestRunSummaryDto? Evaluation,
    WalkForwardExportSummaryDto? WalkForwardExport);
