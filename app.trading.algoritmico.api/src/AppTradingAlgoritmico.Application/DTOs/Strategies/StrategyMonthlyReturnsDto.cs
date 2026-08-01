using AppTradingAlgoritmico.Application.DTOs.Trades;

namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

/// <summary>
/// Monthly compounding returns of one strategy, for the account-level
/// "monthly returns per strategy" view. Returns are computed from imported
/// live trades against the account's initial balance; strategies without
/// imported trades carry an empty list.
/// </summary>
public sealed record StrategyMonthlyReturnsDto(
    Guid StrategyId,
    string Name,
    string? Symbol,
    IReadOnlyList<MonthlyReturnDto> Returns);
