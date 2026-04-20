namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record ImportedStrategyDto(
    string Name,
    string? Pseudocode,
    string? EntryIndicators,
    string? PriceIndicators,
    string? IndicatorParameters,
    ParsedReportDto? Report
);
