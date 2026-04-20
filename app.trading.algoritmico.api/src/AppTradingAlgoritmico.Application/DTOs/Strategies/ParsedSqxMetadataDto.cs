namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

/// <summary>
/// Rich metadata extracted from a single .sqx file (settings.xml inside the ZIP).
/// Replaces the previous single-value pseudocode-only extraction path.
/// </summary>
public record ParsedSqxMetadataDto(
    string? Pseudocode,
    string? EntryIndicators,
    string? PriceIndicators,
    string? IndicatorParameters
);
