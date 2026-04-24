using AppTradingAlgoritmico.Application.DTOs.Trades;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Filter applied when listing trades for a strategy.
/// </summary>
public enum TradeStatusFilter
{
    All,
    Open,
    Closed
}

public interface ITradeImportService
{
    /// <summary>
    /// Parses an MT4 HTML statement and upserts trades for the given trading account.
    /// Throws <see cref="KeyNotFoundException"/> when the account does not exist.
    /// Throws <see cref="ArgumentException"/> when the parser returns null (empty or unrecognised HTML).
    /// </summary>
    Task<TradeImportResultDto> ImportAsync(Guid accountId, Stream html, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of trades for a strategy, filtered by status and ordered by CloseTime DESC / OpenTime DESC.
    /// </summary>
    Task<PagedResult<StrategyTradeDto>> GetByStrategyAsync(
        Guid strategyId,
        TradeStatusFilter status,
        int page,
        int pageSize,
        CancellationToken ct);
}
