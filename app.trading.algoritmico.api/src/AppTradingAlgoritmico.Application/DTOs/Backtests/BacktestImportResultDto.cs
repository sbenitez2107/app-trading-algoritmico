namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// Outcome of importing ONE file into ONE slot. Run identity is the pair
/// <c>(StrategyId, Kind)</c>, so the previous revision's five outcomes collapse to three plus a
/// rejection: <c>Reattributed</c> and <c>Conflict</c> existed only because attribution was inferred
/// from the file name and could therefore be wrong. See design.md D3.
/// </summary>
public enum BacktestImportOutcome
{
    /// <summary>The slot was empty — a new run was created.</summary>
    Imported,

    /// <summary>The slot already held a run with the identical <c>ContentHash</c>. Nothing was written.</summary>
    Unchanged,

    /// <summary>The slot held a run with a different <c>ContentHash</c> — its trades were replaced in place, no second run created.</summary>
    Replaced,

    /// <summary>The file failed a file-level guard (see <see cref="ParsedBacktestFileDto.RejectionReason"/>) or its persistence failed.</summary>
    Rejected,
}

/// <summary>Result of one trade-list import into one <c>(StrategyId, Kind)</c> slot.</summary>
public sealed record BacktestImportResultDto(
    string FileName,
    BacktestImportOutcome Outcome,
    int? TradeCount,
    int? RejectedRowCount,
    string? Reason);

/// <summary>
/// Result of one walk-forward-export import. <c>OosFromDate</c> is echoed back because it is the
/// single value the whole evaluation story depends on, and the user has no other way to see which
/// boundary the file produced.
/// </summary>
public sealed record WalkForwardImportResultDto(
    string FileName,
    BacktestImportOutcome Outcome,
    int? WindowCount,
    DateTime? OosFromDate,
    string? Reason);
