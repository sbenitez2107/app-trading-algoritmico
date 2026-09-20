using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Reads the cost decomposition for one strategy and ONE caller-named <see cref="BacktestRunKind"/>
/// slot. <paramref name="kind"/> of <see cref="GetAsync"/> carries no default — mirroring slice A's
/// <see cref="IDemoBacktestComparabilityReadService"/> (design "BacktestRunKind required").
/// </summary>
public interface ICostDecompositionReadService
{
    Task<CostDecompositionDto> GetAsync(Guid strategyId, BacktestRunKind kind, CancellationToken ct);
}
