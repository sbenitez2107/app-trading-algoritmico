namespace AppTradingAlgoritmico.Application.Interfaces;

public interface ISqxParserService
{
    /// <summary>Extracts human-readable pseudocode from a single .sqx file stream.</summary>
    Task<string?> ExtractPseudocodeAsync(Stream sqxStream, CancellationToken ct = default);

    /// <summary>Parses a .sqb (Building Block) file and extracts the raw XML config.</summary>
    Task<string?> ParseSqbConfigAsync(Stream sqbStream, CancellationToken ct = default);
}
