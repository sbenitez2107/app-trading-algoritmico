using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

public sealed record BacktestTradeDto(
    Guid Id,
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
