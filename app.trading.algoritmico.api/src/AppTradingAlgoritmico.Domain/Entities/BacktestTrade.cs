using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One row from an imported SQX/AlgoWizard trade-list CSV. Keyed by <c>(BacktestRunId, RowIndex)</c>,
/// NEVER by <see cref="Ticket"/> — tickets collide across independently-generated backtest runs
/// (27 verified collisions between the two committed fixtures, genuinely different trades).
/// <see cref="Ticket"/> is stored as informational data only, with a non-unique index.
/// This entity is NEVER written to or read from <c>StrategyTrade</c> — see <see cref="AppTradingAlgoritmico.Application.Interfaces.IBacktestDbContext"/>
/// for the structural isolation that enforces this at compile time.
/// </summary>
public class BacktestTrade : BaseEntity
{
    public Guid BacktestRunId { get; set; }
    public BacktestRun BacktestRun { get; set; } = null!;

    /// <summary>0-based ordinal of this row among all data rows of the source file. Unique with <see cref="BacktestRunId"/>.</summary>
    public int RowIndex { get; set; }

    /// <summary>Informational only — NOT unique. See class remarks.</summary>
    public long Ticket { get; set; }

    /// <summary>Verbatim SQX symbol — no normalization (C4).</summary>
    public required string Symbol { get; set; }

    public required string Type { get; set; }

    public DateTime OpenTime { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal Size { get; set; }

    public DateTime CloseTime { get; set; }
    public decimal ClosePrice { get; set; }

    /// <summary>Raw "Profit/Loss" column — never used as a calibration source (spread/commission contaminated).</summary>
    public decimal Profit { get; set; }

    public decimal Balance { get; set; }

    /// <summary>Verbatim "Sample type" source value.</summary>
    public required string SampleTypeRaw { get; set; }

    public BacktestSegment Segment { get; set; }

    /// <summary>The "n" in "OOSn" (e.g. 1 for "OOS1"); null for InSample/InSampleTest/Unknown.</summary>
    public int? SegmentIndex { get; set; }

    /// <summary>Verbatim "Close type" — SL | PT | TrailingStop | End Of Friday | End Of Friday (Time).</summary>
    public required string CloseType { get; set; }

    /// <summary>|MAE| when <see cref="CloseType"/> == "SL", else null. Never defaulted.</summary>
    public decimal? RealizedRisk { get; set; }

    /// <summary>Nullable from the first migration — current SQX export has no Stop Loss column.</summary>
    public decimal? StopLoss { get; set; }

    public string? Comment { get; set; }
}
