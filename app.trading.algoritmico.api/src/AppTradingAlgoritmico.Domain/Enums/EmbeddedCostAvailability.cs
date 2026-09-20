namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The readiness of the backtest symbol's point-value calibration for embedded-cost estimation
/// (design D4, D8). Deliberately NOT <see cref="CalibrationStatus"/>: that enum has only three
/// members and cannot express "no calibration row exists at all" — a state this capability needs to
/// distinguish from a row that exists with a non-<c>Calibrated</c> status. Its zero member,
/// <see cref="NoCalibrationRow"/>, is the state that asserts nothing usable — not <c>Calibrated</c> —
/// mirroring the lesson from <c>PlatformType.MT4 = 0</c> and <c>FundingService.Other = 0</c>, where an
/// optimistic enum zero silently asserted a meaning nobody chose.
/// </summary>
public enum EmbeddedCostAvailability
{
    /// <summary>No <see cref="AppTradingAlgoritmico.Domain.Entities.SymbolCalibration"/> row exists for this symbol at all.</summary>
    NoCalibrationRow = 0,

    /// <summary>A calibration row exists but <see cref="CalibrationStatus.InsufficientSamples"/> was recorded.</summary>
    InsufficientSamples,

    /// <summary>A calibration row exists but <see cref="CalibrationStatus.Inconsistent"/> was recorded.</summary>
    Inconsistent,

    /// <summary>A calibration row exists with <see cref="CalibrationStatus.Calibrated"/> — the only state that publishes a point value.</summary>
    Calibrated,
}
