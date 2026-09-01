namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// Result of parsing one SQX Optimizer "Walk-Forward Results" export. Zero EF references — this
/// DTO is produced entirely without a database.
/// <para>
/// A WF export has no row-level rejection: every row is a window, the row order carries meaning
/// (<c>OosFromDate</c> is the second-to-last row's OOS start), and a dropped row would silently
/// move the boundary. So every guard in this parser is FILE-level — either the whole file parses
/// or the whole file is rejected.
/// </para>
/// </summary>
public sealed record ParsedWalkForwardExportDto(
    string FileName,
    bool IsRejected,
    string? RejectionReason,
    IReadOnlyList<ParsedWalkForwardWindowDto> Windows);
