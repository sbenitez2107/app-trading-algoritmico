using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Read model for imported backtest data. Separate from <see cref="IBacktestImportService"/> on
/// purpose: the IMPORTER is structurally isolated from live trade storage and from tracked
/// <c>Strategy</c> entities (design.md D2), while the read model legitimately joins strategies to
/// show whose run a row is. Keeping them apart means the isolation is not weakened to satisfy a
/// display concern.
/// </summary>
public interface IBacktestReadService
{
    Task<PagedResult<BacktestRunDto>> GetRunsAsync(int page, int pageSize, CancellationToken ct);

    Task<PagedResult<BacktestTradeDto>> GetTradesByRunAsync(
        Guid runId, BacktestSegment? segment, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<SymbolCalibrationDto>> GetCalibrationsAsync(CancellationToken ct);

    /// <summary>Both slots, the walk-forward export and the derived readiness for ONE strategy.</summary>
    Task<StrategyBacktestsDto> GetByStrategyAsync(Guid strategyId, CancellationToken ct);
}
