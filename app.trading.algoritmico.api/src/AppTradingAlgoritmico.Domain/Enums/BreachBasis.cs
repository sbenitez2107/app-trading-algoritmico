namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discloses that a breach readout is a LOWER BOUND, not the vendor's verdict: FTMO and Axi both
/// breach on equity including unrealised open P&amp;L, but the app holds only closed trades.
/// <see cref="ClosedTradeLowerBound"/> applies to <see cref="GuardrailKind.LossLimits"/> and
/// <see cref="GuardrailKind.StagedLossLimits"/>; <see cref="GuardrailKind.VarTarget"/> carries no
/// breach basis at all (null) — it has no breach semantics (`funding-guardrails` spec).
/// </summary>
public enum BreachBasis
{
    ClosedTradeLowerBound = 0,

    /// <summary>
    /// PR P4 (`ftmo-breach-simulation` change, `funding-guardrails` delta — design.md Decision 7):
    /// discloses that <see cref="GuardrailKind.LossLimits"/>'s <c>DailyBreached</c> readout compares
    /// the segment's VaR95 against the daily loss limit — a quantile comparison, not a replay of the
    /// exact tail where a breach lives. Label-only change: the underlying <c>DailyBreached</c> and
    /// <c>DailyHeadroomPct</c> VALUES are unchanged; only this disclosure differs from the previous
    /// (misleading) <see cref="ClosedTradeLowerBound"/> label.
    /// </summary>
    VarQuantileComparison = 1,
}
