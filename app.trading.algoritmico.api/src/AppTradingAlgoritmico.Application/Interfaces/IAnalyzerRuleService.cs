using AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IAnalyzerRuleService
{
    Task<IEnumerable<AnalyzerRuleDto>> GetAllAsync(CancellationToken ct = default);
    Task<AnalyzerRuleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AnalyzerRuleDto> CreateAsync(CreateAnalyzerRuleDto dto, CancellationToken ct = default);
    Task<AnalyzerRuleDto> UpdateAsync(Guid id, UpdateAnalyzerRuleDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
