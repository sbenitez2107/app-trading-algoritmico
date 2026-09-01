using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// Read projection of one run. The owning strategy is a plain FK join — there is no attribution
/// status to report, because a run cannot exist without a strategy.
/// </summary>
public sealed record BacktestRunDto(
    Guid Id,
    string SourceFileName,
    string? Symbol,
    Guid StrategyId,
    string StrategyName,
    BacktestRunKind Kind,
    int TradeCount,
    DateTime CreatedAt);
