namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// A start's whole-chain outcome (ftmo-multi-start, spec.md "Six Chain Outcomes, Three Right-Censored
/// And Labelled As Such"). Exactly one of these six values, never a boolean pass/fail flag, and never
/// wording that affirms survival (spec.md's no-pass-wording requirement) — <see cref="FundedNoBreachAtEndOfData"/>
/// replaces "survived", which both shipped specs ban.
/// <para>
/// Zero-value choice: <see cref="Phase1UndecidedAtEndOfData"/> is the CLR default (non-optimistic zero,
/// per design.md's Interfaces/Contracts note).
/// </para>
/// <para>
/// Apply note (ftmo-multi-start PR1, task 1.4): pulled forward ahead of design.md's File Changes
/// table, which schedules the <c>Domain/Enums</c> row for PR4 alongside the other new enums, the same
/// way <see cref="FtmoFundedOutcome"/> was pulled forward in task 1.3. <c>FtmoMultiStartChain.Classify</c>
/// (PR1, task 1.4) needs this type to compile; it is pure and has no dependency on the PR4 DTO/service
/// surface — moving only this one enum earlier changes no PR4 scope or file count.
/// </para>
/// </summary>
public enum FtmoChainOutcome
{
    /// <summary>Phase 1 never reached its target and never breached before the replayed data ended. Also the CLR default.</summary>
    Phase1UndecidedAtEndOfData = 0,

    /// <summary>Phase 1 breached a loss limit before reaching its target.</summary>
    Phase1Breached = 1,

    /// <summary>Phase 1 reached its target; phase 2 breached a loss limit before reaching its own target.</summary>
    Phase2Breached = 2,

    /// <summary>Phase 1 reached its target; phase 2 never reached its target and never breached before the replayed data ended.</summary>
    Phase2UndecidedAtEndOfData = 3,

    /// <summary>Phase 1 and phase 2 both reached their targets; the funded phase breached a loss limit.</summary>
    FundedBreached = 4,

    /// <summary>Phase 1 and phase 2 both reached their targets; the funded phase never breached before the replayed data ended.</summary>
    FundedNoBreachAtEndOfData = 5,
}
