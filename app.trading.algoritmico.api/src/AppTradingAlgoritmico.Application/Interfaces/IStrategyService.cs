using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IStrategyService
{
    Task<PagedResult<StrategyDto>> GetByStageAsync(Guid batchId, Guid stageId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<StrategyDto> UpdateKpisAsync(Guid strategyId, UpdateStrategyKpisDto dto, CancellationToken ct = default);
    Task<PagedResult<StrategyDto>> GetByAccountAsync(Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<StrategyDto> AddToAccountAsync(Guid accountId, string name, Stream sqxStream, Stream htmlStream, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<StrategyCommentDto>> GetCommentsAsync(Guid strategyId, CancellationToken ct = default);
    Task<StrategyCommentDto> AddCommentAsync(Guid strategyId, string content, string? userId, CancellationToken ct = default);
}

public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
