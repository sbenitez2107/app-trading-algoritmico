using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One row of a <see cref="StrategyWalkForwardExport"/>, stored VERBATIM. No robustness aggregate
/// is persisted: a stored ratio goes stale the instant its formula changes, with no signal that it
/// has. Slice 1 RECORDS the windows; a later slice JUDGES them (design.md D13).
/// <para>
/// The four OOS numerics are nullable because the LAST row of every export is the window whose
/// out-of-sample period has NOT elapsed yet: SQX writes the literal string <c>N/A</c> there.
/// Parsing that as <c>0</c> would make the un-run window look like the worst one in the file, so
/// it is null and <see cref="IsFutureWindow"/> is set. Every aggregate must exclude it.
/// </para>
/// </summary>
public class WalkForwardWindow : BaseEntity
{
    public Guid ExportId { get; set; }
    public StrategyWalkForwardExport Export { get; set; } = null!;

    /// <summary>0-based ordinal among the file's data rows. Unique with <see cref="ExportId"/>; the export's order is meaningful.</summary>
    public int RowIndex { get; set; }

    public DateTime PeriodIsStart { get; set; }
    public DateTime PeriodIsEnd { get; set; }
    public DateTime PeriodOosStart { get; set; }
    public DateTime PeriodOosEnd { get; set; }

    public int DaysIs { get; set; }

    /// <summary>Populated even on the future window — SQX reports the planned span (381 in the committed fixture).</summary>
    public int DaysOos { get; set; }

    public decimal NetProfitIs { get; set; }
    public decimal RetDdRatioIs { get; set; }
    public decimal DrawdownIs { get; set; }
    public decimal AvgTradesPerMonthIs { get; set; }

    /// <summary>Null on the future window — never 0.</summary>
    public decimal? NetProfitOos { get; set; }

    /// <summary>Null on the future window — never 0.</summary>
    public decimal? RetDdRatioOos { get; set; }

    /// <summary>Null on the future window — never 0.</summary>
    public decimal? DrawdownOos { get; set; }

    /// <summary>Null on the future window — never 0.</summary>
    public decimal? AvgTradesPerMonthOos { get; set; }

    /// <summary>Verbatim <c>Parameters</c> text of this window. Inside it commas separate and dots are decimals — the inverse of every other column in the file.</summary>
    public required string Parameters { get; set; }

    /// <summary>
    /// True only when BOTH signals agree: the four OOS columns are the literal <c>N/A</c> AND the
    /// OOS period carries a <c> (future)</c> suffix. Disagreement rejects the file, because
    /// disagreement means the export format changed.
    /// </summary>
    public bool IsFutureWindow { get; set; }
}
