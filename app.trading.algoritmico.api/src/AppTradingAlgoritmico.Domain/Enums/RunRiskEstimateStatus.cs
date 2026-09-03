namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Outcome of a run's own risk-per-trade estimate (design.md D1/D4), shaped exactly like
/// <see cref="CalibrationStatus"/>: the estimate is ALWAYS returned, and only the value is
/// withheld when the status is not <see cref="Estimated"/> — the measured fractions that caused
/// the refusal must survive it.
/// </summary>
public enum RunRiskEstimateStatus
{
    Estimated = 0,

    /// <summary>Fewer usable SL closes than <c>TradeRiskNormalizer.MinimumSlSamples</c>.</summary>
    InsufficientSamples,

    /// <summary>An <c>Â</c> exists but fewer than 85% of the SL closes are consistent with it.</summary>
    Inconsistent,
}
