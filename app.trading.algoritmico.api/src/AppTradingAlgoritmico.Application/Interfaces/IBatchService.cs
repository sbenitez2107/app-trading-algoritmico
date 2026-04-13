using AppTradingAlgoritmico.Application.DTOs.Batches;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IBatchService
{
    Task<IEnumerable<BatchDto>> GetAllAsync(Guid? assetId = null, Timeframe? timeframe = null, CancellationToken ct = default);
    Task<BatchDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BatchDto> CreateAsync(CreateBatchDto dto, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default);
    Task<BatchDto> AdvanceAsync(Guid batchId, Stream? zipFile = null, int? strategyCount = null, CancellationToken ct = default);
    Task<BatchDto> RollbackStageAsync(Guid batchId, Guid stageId, CancellationToken ct = default);
}
