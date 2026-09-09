namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discloses that a demo-vs-backtest comparability readout is measured over the EXACT-MINUTE
/// PAIRED SUBSET only, never over the full demo or backtest trade set. Mirrors
/// <see cref="BreachBasis"/> — a caveat encoded as a type rather than a comment, so a call site
/// cannot drop it. There is exactly one basis today, so the enum carries exactly one member; the
/// point is not choice, it is that no readout can be constructed without stating it
/// (`demo-backtest-comparability` spec, design.md D6).
/// </summary>
public enum ComparabilityBasis
{
    PairedOpensOnly = 0,
}
