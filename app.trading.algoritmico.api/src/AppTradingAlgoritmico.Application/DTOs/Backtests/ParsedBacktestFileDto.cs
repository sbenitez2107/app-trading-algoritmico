namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// Result of parsing one SQX/AlgoWizard trade-list CSV file. Zero EF references — this DTO is
/// produced entirely without a database. When <see cref="IsRejected"/> is true, the whole file
/// failed a file-level guard (wrong delimiter, missing column, unparseable date, multiple
/// symbols) and <see cref="Trades"/>/<see cref="RejectedRows"/> are both empty; individual
/// malformed rows are represented in <see cref="RejectedRows"/> instead, and do not reject the
/// rest of the file.
/// </summary>
public sealed record ParsedBacktestFileDto(
    string FileName,
    string? Symbol,
    bool IsRejected,
    string? RejectionReason,
    IReadOnlyList<ParsedBacktestTradeDto> Trades,
    IReadOnlyList<RejectedBacktestRowDto> RejectedRows);
