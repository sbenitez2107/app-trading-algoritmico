using AppTradingAlgoritmico.Application.DTOs.Batches;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IBatchService
{
    Task<IEnumerable<BatchDto>> GetAllAsync(Guid? assetId = null, Timeframe? timeframe = null, CancellationToken ct = default);
    Task<BatchDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BatchDto> CreateAsync(CreateBatchDto dto, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default);
    /// <summary>
    /// Closes the current stage of <paramref name="batchId"/> and creates the next stage.
    /// </summary>
    /// <param name="passedCount">
    /// How many strategies are recorded as having passed the current stage (its OutputCount).
    /// Ignored when a ZIP file is provided — the file's strategy count wins.
    /// </param>
    /// <param name="nextInputCount">
    /// How many strategies enter the next stage (its InputCount). Defaults to the same as
    /// the resolved passed count when omitted.
    /// </param>
    Task<BatchDto> AdvanceAsync(
        Guid batchId,
        Stream? zipFile = null,
        int? passedCount = null,
        int? nextInputCount = null,
        CancellationToken ct = default);
    Task<BatchDto> RollbackStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default);
    Task DeleteAsync(Guid batchId, CancellationToken ct = default);
}
