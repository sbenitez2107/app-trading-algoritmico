using AppTradingAlgoritmico.Application.DTOs.Portfolios;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>User-configured prop-firm risk limits, keyed by broker. Numbers are never hardcoded.</summary>
public interface IRiskLimitsService
{
    Task<IReadOnlyList<BrokerRiskLimitsDto>> GetAllAsync(CancellationToken ct = default);
    Task<BrokerRiskLimitsDto?> GetByBrokerAsync(string broker, CancellationToken ct = default);
    Task<BrokerRiskLimitsDto> UpsertAsync(UpsertBrokerRiskLimitsDto dto, CancellationToken ct = default);
}
