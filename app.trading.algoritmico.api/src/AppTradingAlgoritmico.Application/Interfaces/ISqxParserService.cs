using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface ISqxParserService
{
    Task<IList<ParsedStrategyDto>> ParseZipAsync(Stream zipStream, CancellationToken ct = default);
    Task<string?> ParseSqbConfigAsync(Stream sqbStream, CancellationToken ct = default);
}
