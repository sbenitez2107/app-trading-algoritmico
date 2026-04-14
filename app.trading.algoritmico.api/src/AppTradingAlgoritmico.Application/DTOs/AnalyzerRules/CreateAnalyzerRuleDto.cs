using System.ComponentModel.DataAnnotations;

namespace AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;

public record CreateAnalyzerRuleDto(
    [Required] string Name,
    [Required] string Description,
    int Priority = 0
);
