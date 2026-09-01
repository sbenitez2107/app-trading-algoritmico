namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Outcome of <see cref="AppTradingAlgoritmico.Domain.Entities.SymbolCalibration"/> assessment.
/// Every assessment persists a row regardless of status — only <c>PointValue</c> is withheld
/// for <see cref="InsufficientSamples"/> and <see cref="Inconsistent"/>.
/// </summary>
public enum CalibrationStatus
{
    Calibrated = 0,
    InsufficientSamples,
    Inconsistent,
}
