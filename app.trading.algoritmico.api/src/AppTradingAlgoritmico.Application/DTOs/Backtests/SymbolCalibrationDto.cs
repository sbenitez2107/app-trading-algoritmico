using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One symbol's point-value assessment — both the pure calibrator's output and the REST
/// projection of a persisted <see cref="AppTradingAlgoritmico.Domain.Entities.SymbolCalibration"/> row.
/// </summary>
public sealed record SymbolCalibrationDto(
    string Symbol,
    decimal? PointValue,
    int SampleCount,
    decimal? MinObserved,
    decimal? MaxObserved,
    CalibrationStatus Status,
    DateTime CalibratedAt);
