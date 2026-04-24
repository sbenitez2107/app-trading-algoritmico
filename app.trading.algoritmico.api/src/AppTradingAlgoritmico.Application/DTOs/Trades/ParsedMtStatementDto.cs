namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Full parsed output from a single MT4 HTML statement upload.
/// </summary>
public sealed record ParsedMtStatementDto(
    IReadOnlyList<ParsedMtTradeDto> Trades,
    ParsedSummaryDto Summary);
