using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Parses a SQX/AlgoWizard trade-list CSV export. Pure — no EF/DbContext references, no
/// ambient-culture dependency. <paramref name="fileName"/> drives strategy-name/run-label
/// derivation and is sanitized internally via <see cref="Path.GetFileName(string)"/> before use.
/// </summary>
public interface ISqxTradeListParser
{
    Task<ParsedBacktestFileDto> ParseAsync(Stream csv, string fileName, CancellationToken ct);
}
