namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why a breach simulation refused to produce a verdict (design.md Decision 6). Only meaningful
/// alongside <see cref="FtmoSimulationStatus.Refused"/>.
/// <para>
/// Zero-value choice: <see cref="InvalidRequest"/> is the generic, least-specific reason — a
/// default-initialized value should never pin a specific false claim (e.g. "product is not
/// TwoStep" about a product that is actually fine). It reads as "something is wrong, look closer",
/// not as a diagnosis.
/// </para>
/// </summary>
public enum FtmoSimulationRefusal
{
    /// <summary>Bad capital, target, or source grid on the request. Also the CLR default.</summary>
    InvalidRequest = 0,

    /// <summary>The product is null or <c>OneStep</c> on the supplied <c>BrokerRiskLimits</c> row.</summary>
    ProductNotTwoStep,

    /// <summary>
    /// No FTMO <c>BrokerRiskLimits</c> row is configured, or its daily/max loss percentage is null or
    /// outside (0, 1]. A null limit is an unconfigured rule, never a 0% rule.
    /// </summary>
    LimitsNotConfigured,

    /// <summary>The row's drawdown model is not Static.</summary>
    DrawdownModelNotStatic,

    /// <summary>No usable <c>FtmoInstrumentSpec</c> exists for the run's symbol: none at all, or one declaring a non-positive contract size.</summary>
    InstrumentSpecMissing,

    /// <summary>Calibration status is not <c>Calibrated</c>, or its <c>PointValue</c> is null or non-positive.</summary>
    PointValueNotCalibrated,

    /// <summary>The symbol settles in a non-account currency and no FX band was declared.</summary>
    FxRateNotDeclared,

    /// <summary>The declared FX band is degenerate the wrong way or inverted, or yields a non-positive rate.</summary>
    InvalidFxBand,

    /// <summary>The risk normalizer refused the run, or its risk-per-trade estimate is non-positive (no usable estimate).</summary>
    RiskNotEstimable,

    /// <summary>The run's trades carry more than one segment.</summary>
    RunSegmentsDisagree,

    /// <summary>The source or FTMO timezone could not be resolved.</summary>
    TimeZoneDataUnavailable,
}
