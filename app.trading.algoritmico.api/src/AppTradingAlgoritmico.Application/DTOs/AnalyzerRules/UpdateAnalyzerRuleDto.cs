namespace AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;

public record UpdateAnalyzerRuleDto(
    string? Name,
    string? Description,
    int? Priority,
    bool? IsActive
);
