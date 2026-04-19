using AppTradingAlgoritmico.Application.DTOs.Strategies;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IHtmlReportParserService
{
    /// <summary>
    /// Parses a StrategyQuant X HTML report and extracts KPIs, backtest metadata and monthly performance.
    /// Returns null if the stream is not a valid SQX report.
    /// </summary>
    Task<ParsedReportDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default);
}
