using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Reads the demo-vs-backtest entry-price comparability for one strategy and ONE caller-named
/// <see cref="BacktestRunKind"/> slot. <paramref name="kind"/> of <see cref="GetAsync"/> carries no
/// default: the caller must name it, because a strategy with no run in the requested slot answers
/// <c>NoRunForKind</c> and never silently falls back to the other slot (design.md D3).
/// </summary>
public interface IDemoBacktestComparabilityReadService
{
    Task<PriceOffsetComparabilityDto> GetAsync(Guid strategyId, BacktestRunKind kind, CancellationToken ct);
}
