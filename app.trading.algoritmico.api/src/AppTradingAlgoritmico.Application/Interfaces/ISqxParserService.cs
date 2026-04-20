using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface ISqxParserService
{
    /// <summary>
    /// Extracts rich metadata from a single .sqx file stream:
    /// pseudocode, entry indicators, price indicators, and indicator parameters.
    /// </summary>
    Task<ParsedSqxMetadataDto?> ExtractStrategyMetadataAsync(Stream sqxStream, CancellationToken ct = default);

    /// <summary>
    /// Thin wrapper — extracts only the human-readable pseudocode string.
    /// Prefer <see cref="ExtractStrategyMetadataAsync"/> for full metadata.
    /// </summary>
    Task<string?> ExtractPseudocodeAsync(Stream sqxStream, CancellationToken ct = default);

    /// <summary>Parses a .sqb (Building Block) file and extracts the raw XML config.</summary>
    Task<string?> ParseSqbConfigAsync(Stream sqbStream, CancellationToken ct = default);
}
