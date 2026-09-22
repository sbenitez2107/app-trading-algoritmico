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
    /// <para>
    /// <paramref name="sourcePlatform"/> is caller-declared provenance (slice C), never derived.
    /// It carries NO C# default value on purpose: an optional parameter would let a caller omit it
    /// by accident, which is the silent-null hazard this field exists to avoid, in a different
    /// coat. Omitting it explicitly (passing <c>null</c>) records "not declared" on the run;
    /// deriving it from anything else (e.g. the strategy's <c>TradingAccount.Platform</c>) is
    /// rejected (design.md D4).
    /// </para>
    /// </summary>
    Task<BacktestImportResultDto> ImportTradeListAsync(
        Guid strategyId, BacktestRunKind kind, BacktestFileUploadDto file, PlatformType? sourcePlatform, CancellationToken ct);
}
