namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record ImportedStrategyDto(
    string Name,
    string? Pseudocode,
    ParsedReportDto? Report
);
