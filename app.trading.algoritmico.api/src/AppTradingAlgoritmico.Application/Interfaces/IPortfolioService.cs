using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.DTOs.Trades;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Platform-level portfolios of strategies. CRUD over membership + weights; all analytics
/// (combined KPIs, equity curve, monthly returns) are computed on demand from member trades.
/// </summary>
public interface IPortfolioService
{
    Task<PagedResult<PortfolioDto>> GetAllAsync(string? broker = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<PortfolioDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PortfolioDto> CreateAsync(CreatePortfolioDto dto, CancellationToken ct = default);
    Task<PortfolioDto> UpdateAsync(Guid id, UpdatePortfolioDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Membership
    Task<PortfolioDto> AddMemberAsync(Guid portfolioId, AddPortfolioMemberDto dto, CancellationToken ct = default);
    Task<PortfolioDto> UpdateMemberWeightAsync(Guid portfolioId, Guid strategyId, decimal weight, CancellationToken ct = default);
    Task<PortfolioDto> RemoveMemberAsync(Guid portfolioId, Guid strategyId, CancellationToken ct = default);

    // Analytics (computed on demand)
    Task<PortfolioAnalyticsDto> GetAnalyticsAsync(Guid portfolioId, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyReturnDto>> GetMonthlyReturnsAsync(Guid portfolioId, CancellationToken ct = default);
    Task<IReadOnlyList<PortfolioEquityPointDto>> GetEquityCurveAsync(Guid portfolioId, CancellationToken ct = default);
    Task<PortfolioRiskDto> GetRiskAsync(Guid portfolioId, CancellationToken ct = default);
}
