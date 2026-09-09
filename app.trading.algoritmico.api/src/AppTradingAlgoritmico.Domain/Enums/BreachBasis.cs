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
}
