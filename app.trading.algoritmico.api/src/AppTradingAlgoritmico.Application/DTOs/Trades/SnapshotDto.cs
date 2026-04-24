namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// Account equity snapshot data as returned in the import response.
/// Mirrors the fields of ParsedSummaryDto; included in TradeImportResultDto.
/// </summary>
public sealed record SnapshotDto(
    DateTime ReportTime,
    decimal Balance,
    decimal Equity,
    decimal FloatingPnL,
    decimal Margin,
    decimal FreeMargin,
    decimal ClosedTradePnL,
    string Currency);
