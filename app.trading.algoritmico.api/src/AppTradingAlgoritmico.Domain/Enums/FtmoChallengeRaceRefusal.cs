namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why the FTMO 2-Step challenge race was refused, scoped to the race only — the run itself stays
/// <see cref="FtmoSimulationStatus.Evaluated"/> (ftmo-challenge-race, spec.md "A Stored Profit Target
/// Percentage That Disagrees With The Fixed Rule Refuses The Race, Not The Run"). Bare enum: the
/// stored value that caused the refusal is echoed as a separate top-level field
/// (<c>FtmoChallengeRaceDto.StoredProfitTargetPct</c>), not carried inside the refusal reason itself
/// (orchestrator-resolved, 2026-09-27).
/// <para>
/// The whole-run refusal path (non-<c>TwoStep</c> or missing product, unconfigured limits, etc.)
/// already refuses the ENTIRE run before any race code runs (hard rule 7); this enum's only member
/// covers the race-only, narrower refusal.
/// </para>
/// </summary>
public enum FtmoChallengeRaceRefusal
{
    /// <summary>The stored <c>BrokerRiskLimits.ProfitTargetPct</c> is present and not 0.10 (the fixed 2-Step rule).</summary>
    ProfitTargetMismatch = 0,
}
