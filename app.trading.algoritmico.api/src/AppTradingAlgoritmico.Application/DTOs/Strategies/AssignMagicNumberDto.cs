namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

/// <summary>
/// Request body for POST /api/trading-accounts/{accountId}/strategies/{strategyId}/magic-number.
/// </summary>
public sealed record AssignMagicNumberDto(int MagicNumber);
