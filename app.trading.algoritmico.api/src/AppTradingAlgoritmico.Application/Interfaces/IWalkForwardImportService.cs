using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IWalkForwardImportService
{
    /// <summary>
    /// Imports ONE SQX Optimizer walk-forward export for ONE strategy. A strategy has at most one
    /// export, so a re-import REPLACES the previous one and its windows, and recomputes
    /// <c>OosFromDate</c> — a stale boundary is worse than none.
    /// </summary>
    Task<WalkForwardImportResultDto> ImportAsync(Guid strategyId, BacktestFileUploadDto file, CancellationToken ct);
}
