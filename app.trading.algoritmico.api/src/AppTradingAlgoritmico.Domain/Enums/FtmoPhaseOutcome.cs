namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// One phase's (Challenge or Verification) outcome in the FTMO 2-Step challenge race
/// (ftmo-challenge-race, spec.md "Phase Outcome Is A Four-State Enum, Never A Boolean"). Exactly one
/// of these four values, never a boolean pass/fail flag, and never wording that affirms survival for
/// <see cref="TargetReachedFirst"/> (spec.md's no-pass-wording requirement).
/// <para>
/// Zero-value choice: <see cref="NotStarted"/> is the CLR default — a default-initialized phase result
/// should read as "this phase never ran", not as a specific decided outcome.
/// </para>
/// </summary>
public enum FtmoPhaseOutcome
{
    /// <summary>The phase never started (phase 2 when phase 1 did not reach its target). Also the CLR default.</summary>
    NotStarted = 0,

    /// <summary>The phase's target was reached first, at a flat close, with the day minimum already met.</summary>
    TargetReachedFirst = 1,

    /// <summary>A loss limit (daily or max) was breached first, ending the phase.</summary>
    BreachedFirst = 2,

    /// <summary>The replayed data ended before either the target or a breach was resolved.</summary>
    NeitherByEndOfData = 3,
}
