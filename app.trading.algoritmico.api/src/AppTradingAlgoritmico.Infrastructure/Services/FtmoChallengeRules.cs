using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-challenge-race (design.md Decision 7, spec.md "Two-Step Challenge And Verification Rules Are
/// Fixed In Code, Not Configurable") — the fixed FTMO 2-Step ruleset. Pure constant data, keyed on
/// <see cref="FtmoProduct.TwoStep"/>: 10% phase-1 target, 5% phase-2 target, both non-compounding
/// (against Initial Capital, never an intermediate balance), a 4-FTMO-trading-day minimum per phase,
/// and no time limit. Cites <c>SERVICE_FTMO.md:46-48,157-158,236</c>.
/// <para>
/// <c>LossLimits</c> rows have a single <c>ProfitTargetPct</c> column, which cannot hold both 10% and
/// 5% at once, and <c>Stages</c> are rejected on <c>LossLimits</c> rows
/// (<c>RiskLimitsService.cs:118-119</c>) — there is no storage shape for a per-phase configurable
/// target, so these values are fixed in code rather than read from a row that could not hold them.
/// </para>
/// </summary>
internal static class FtmoChallengeRules
{
    internal const decimal Phase1TargetPct = 0.10m;
    internal const decimal Phase2TargetPct = 0.05m;
    internal const int MinTradingDaysPerPhase = 4;

    /// <summary>The single supported product for this race — <see cref="FtmoProduct.TwoStep"/>.</summary>
    internal static readonly FtmoProduct Product = FtmoProduct.TwoStep;
}
