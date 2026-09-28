namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The funded phase's outcome (ftmo-multi-start, spec.md "Each Start Runs Phase 1, Phase 2, Then A
/// Funded Phase"). Exactly one of these three values, never a boolean pass/fail flag, and never
/// wording that affirms survival (spec.md's no-pass-wording requirement) — <see cref="NoBreachByEndOfData"/>
/// replaces "survived", which both shipped specs ban.
/// <para>
/// Zero-value choice: <see cref="NotStarted"/> is the CLR default — a default-initialized funded
/// result should read as "phase 2 never reached its target", not as a specific decided outcome
/// (non-optimistic zero, per design.md's Interfaces/Contracts note).
/// </para>
/// <para>
/// Apply note (ftmo-multi-start PR1, task 1.3): created ahead of design.md's File Changes table,
/// which schedules the <c>Domain/Enums</c> row for PR4 alongside the other new enums. This one type
/// is required at compile time by <c>FtmoFundedPhase.cs</c>'s own outcome (PR1, task 1.3), which is
/// pure and has no dependency on the PR4 DTO/service surface — moving only this enum earlier changes
/// no PR4 scope or file count.
/// </para>
/// </summary>
public enum FtmoFundedOutcome
{
    /// <summary>Phase 2 did not reach its target, so the funded phase never started. Also the CLR default.</summary>
    NotStarted = 0,

    /// <summary>A loss limit (daily or max) was breached first, ending the funded phase.</summary>
    BreachedFirst = 1,

    /// <summary>The replayed data ended before a breach was resolved.</summary>
    NoBreachByEndOfData = 2,
}
