using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One imported SQX/AlgoWizard trade-list CSV file, owned by exactly one <see cref="Strategy"/>.
/// <para>
/// Identity is the SLOT — the unique pair <c>(StrategyId, Kind)</c> — because the strategy is
/// known from the import route before the file is ever read. Attribution is therefore a foreign
/// key, never an inference: there is no filename convention to honour, no name-matching step, and
/// no way to produce an unattributed run. The previous revision derived attribution from the
/// filename, which is why it needed a join table, a duplicate fan-out and a derived
/// "unmatched" status; all three exist only to answer a question this shape stops asking.
/// </para>
/// <para>
/// <see cref="ContentHash"/> is a DE-DUP key, not identity, and deliberately carries no unique
/// index: one SQX strategy deployed under two <see cref="Strategy"/> rows (the same system on two
/// accounts) legitimately imports the same bytes twice. Calibration is what consumes the hash —
/// it counts one run per distinct hash so the sample size is not doubled. See design.md D3/D4.
/// </para>
/// </summary>
public class BacktestRun : BaseEntity
{
    /// <summary>Sanitized (<see cref="Path.GetFileName(string)"/>) original filename. Display/audit only — never part of identity.</summary>
    public required string SourceFileName { get; set; }

    /// <summary>SHA-256 over the raw file bytes, lowercase hex. De-dup key for calibration — NOT unique.</summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// The owning strategy, set from the import route. NOT NULL, cascade-deleted: deleting a
    /// strategy deletes its runs and their trades. No navigation property is declared on purpose —
    /// the importer's persistence surface must keep no compile-time path to a tracked
    /// <see cref="Strategy"/> (design.md D2).
    /// </summary>
    public Guid StrategyId { get; set; }

    /// <summary>Which of the strategy's two run slots this file occupies. Unique with <see cref="StrategyId"/>.</summary>
    public BacktestRunKind Kind { get; set; }

    /// <summary>The single SQX symbol for every trade in this run (file-level guard rejects multi-symbol files). Verbatim, no normalization (C4).</summary>
    public string? Symbol { get; set; }

    public ICollection<BacktestTrade> Trades { get; init; } = [];
}
