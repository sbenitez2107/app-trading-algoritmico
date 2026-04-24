using AppTradingAlgoritmico.Application.DTOs.Trades;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IMtStatementParserService
{
    /// <summary>
    /// Parses a Darwinex MT4 HTML "Detailed Statement" and extracts trades and account summary.
    /// Returns null if the stream is empty or contains no recognised section markers.
    /// </summary>
    Task<ParsedMtStatementDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default);
}
