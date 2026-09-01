using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One auditable point-value assessment per SQX symbol (verbatim, contract-level identity — see
/// design.md D4). Computed from `MAE` on SL-closed <see cref="BacktestTrade"/> rows only, NEVER
/// from `Profit`. A row is ALWAYS written once a symbol has been touched by an import, regardless
/// of <see cref="Status"/> — a missing row cannot express "tried, not enough" vs "never tried".
/// </summary>
public class SymbolCalibration : BaseEntity
{
    /// <summary>Verbatim SQX symbol. Unique. Contract-level identity — no underlying/broker mapping in slice 1 (C4).</summary>
    public required string Symbol { get; set; }

    /// <summary>Median of per-sample point values. NULL unless <see cref="Status"/> is <see cref="CalibrationStatus.Calibrated"/>.</summary>
    public decimal? PointValue { get; set; }

    public int SampleCount { get; set; }

    public decimal? MinObserved { get; set; }
    public decimal? MaxObserved { get; set; }

    public CalibrationStatus Status { get; set; }

    public DateTime CalibratedAt { get; set; }
}
