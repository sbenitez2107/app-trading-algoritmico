using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IBacktestImportService
{
    /// <summary>
    /// Imports ONE SQX/AlgoWizard trade-list CSV into ONE slot of ONE strategy. The strategy and
    /// the kind come from the caller (the import route), never from the file — there is no batch
    /// and no attribution step. A persistence failure is reported as
    /// <see cref="BacktestImportOutcome.Rejected"/> carrying the provider's own diagnosis rather
    /// than propagating, so the caller always gets an answer naming the file (design.md D6).
    /// </summary>
    Task<BacktestImportResultDto> ImportTradeListAsync(
        Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file, CancellationToken ct);
}
