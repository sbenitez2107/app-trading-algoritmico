using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IStrategyService
{
    Task<PagedResult<StrategyDto>> GetByStageAsync(Guid batchId, Guid stageId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<StrategyDto> UpdateKpisAsync(Guid strategyId, UpdateStrategyKpisDto dto, CancellationToken ct = default);
    Task<PagedResult<StrategyDto>> GetByAccountAsync(Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<StrategyDto> AddToAccountAsync(Guid accountId, string name, Stream sqxStream, Stream htmlStream, int? magicNumber = null, CancellationToken ct = default);

    /// <summary>
    /// Assigns a magic number to a strategy that belongs to the given trading account.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Strategy not found, or it does not belong to the account.</exception>
    /// <exception cref="InvalidOperationException">
    /// The strategy already has a different magic number, or the magic number is already used by
    /// another strategy in the same account.
    /// </exception>
    Task<StrategyDto> AssignMagicNumberAsync(Guid accountId, Guid strategyId, int magicNumber, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<StrategyCommentDto>> GetCommentsAsync(Guid strategyId, CancellationToken ct = default);
    Task<StrategyCommentDto> AddCommentAsync(Guid strategyId, string content, string? userId, CancellationToken ct = default);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
