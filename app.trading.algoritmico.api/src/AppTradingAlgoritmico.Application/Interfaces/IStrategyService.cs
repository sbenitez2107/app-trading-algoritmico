using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IStrategyService
{
    Task<PagedResult<StrategyDto>> GetByStageAsync(Guid batchId, Guid stageId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<StrategyDto> UpdateKpisAsync(Guid strategyId, UpdateStrategyKpisDto dto, CancellationToken ct = default);
}

public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
