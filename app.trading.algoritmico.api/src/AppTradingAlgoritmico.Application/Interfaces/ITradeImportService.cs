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

    /// <summary>
    /// Returns aggregated KPIs across every imported trade of <paramref name="strategyId"/>.
    /// Independent of the frontend pagination window — computed in SQL.
    /// </summary>
    Task<StrategyTradeSummaryDto> GetSummaryByStrategyAsync(Guid strategyId, CancellationToken ct);

    /// <summary>
    /// Returns the full performance analytics block for a strategy (returns, drawdown,
    /// risk-adjusted metrics, streaks, etc.). Pulls every trade from DB once and runs the
    /// pure <see cref="Infrastructure.Services.StrategyAnalyticsCalculator"/> over them.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Strategy does not exist.</exception>
    Task<StrategyAnalyticsDto> GetAnalyticsByStrategyAsync(Guid strategyId, CancellationToken ct);

    /// <summary>
    /// Returns the month-by-month compounding return series for a strategy, ordered chronologically.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Strategy does not exist.</exception>
    Task<IReadOnlyList<MonthlyReturnDto>> GetMonthlyReturnsByStrategyAsync(Guid strategyId, CancellationToken ct);
}
