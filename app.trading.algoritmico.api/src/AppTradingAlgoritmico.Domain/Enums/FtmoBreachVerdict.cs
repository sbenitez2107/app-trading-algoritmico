namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The three-state finding for one FTMO limit (daily or max loss). No boolean breach flag exists
/// anywhere in this capability's output (spec.md Three-State Finding requirement).
/// <para>
/// <b>Zero-value choice, deliberate.</b> This repo has been bitten three times by an optimistic
/// enum zero (<c>PlatformType.MT4</c>, <c>FundingService.Other</c>,
/// <c>CalibrationStatus.Calibrated</c>) reading as a confident default. Here, neither
/// <see cref="Breached"/> nor <see cref="NoBreachObserved"/> is safe as the CLR default: a
/// default-initialized value must not silently assert a clean pass, and must not silently assert a
/// breach either. <see cref="BreachContingent"/> asserts the least — it commits to neither
/// outcome — so it is enum value <c>0</c>.
/// </para>
/// </summary>
public enum FtmoBreachVerdict
{
    /// <summary>Breaches exist, but none is clean — see <c>Causes</c> on the finding. Never a bare default read.</summary>
    BreachContingent = 0,

    /// <summary>At least one clean (no-cause) breaching close occurred.</summary>
    Breached = 1,

    /// <summary>No breaching close was detected. Silent on survival, not evidence of it.</summary>
    NoBreachObserved = 2,
}
