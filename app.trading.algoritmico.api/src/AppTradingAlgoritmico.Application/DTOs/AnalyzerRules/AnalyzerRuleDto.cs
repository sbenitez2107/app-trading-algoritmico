namespace AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;

public record AnalyzerRuleDto(
    Guid Id,
    string Name,
    string Description,
    int Priority,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
