namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Account summary data parsed from the MT4 HTML statement Summary section.
/// </summary>
public sealed record ParsedSummaryDto(
    DateTime ReportTime,
    decimal Balance,
    decimal Equity,
    decimal FloatingPnL,
    decimal Margin,
    decimal FreeMargin,
    decimal ClosedTradePnL,
    string Currency);
