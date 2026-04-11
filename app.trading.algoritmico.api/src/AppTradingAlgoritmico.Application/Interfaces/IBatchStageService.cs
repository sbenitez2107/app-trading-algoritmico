using AppTradingAlgoritmico.Application.DTOs.BatchStages;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IBatchStageService
{
    Task<BatchStageDetailDto> GetDetailAsync(Guid batchId, Guid stageId, CancellationToken ct = default);
    Task<BatchStageDetailDto> UpdateAsync(Guid batchId, Guid stageId, UpdateBatchStageDto dto, CancellationToken ct = default);
}
