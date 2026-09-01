using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One successfully-parsed row from a SQX/AlgoWizard trade-list CSV export.
/// <see cref="RowIndex"/> is the 0-based ordinal of this row among ALL data rows in the source
/// file (including rejected ones) — it is the only identifier the export guarantees, since
/// <see cref="Ticket"/> is not unique across runs.
/// </summary>
public sealed record ParsedBacktestTradeDto(
    int RowIndex,
    long Ticket,
    string Symbol,
    string Type,
    DateTime OpenTime,
    decimal OpenPrice,
    decimal Size,
    DateTime CloseTime,
    decimal ClosePrice,
    decimal Profit,
    decimal Balance,
    string SampleTypeRaw,
    BacktestSegment Segment,
    int? SegmentIndex,
    string CloseType,
    decimal? RealizedRisk,
    decimal? StopLoss,
    string? Comment);
