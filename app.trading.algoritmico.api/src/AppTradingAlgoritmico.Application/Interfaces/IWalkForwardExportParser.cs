using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Parses SQX Optimizer "Walk-Forward Results" exports. Deliberately a SEPARATE contract from
/// <see cref="ISqxTradeListParser"/>: the two files use inverted decimal conventions
/// (comma vs dot) and different date formats (<c>dd.MM.yyyy</c> vs <c>yyyy.MM.dd HH:mm:ss</c>), so
/// sharing a parsing policy between them would corrupt one side or the other. See design.md D9.
/// </summary>
public interface IWalkForwardExportParser
{
    Task<ParsedWalkForwardExportDto> ParseAsync(Stream csv, string fileName, CancellationToken ct);
}
