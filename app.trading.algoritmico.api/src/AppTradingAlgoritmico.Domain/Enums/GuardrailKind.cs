namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discriminates how a <see cref="AppTradingAlgoritmico.Domain.Entities.BrokerRiskLimits"/> row is
/// modeled. <see cref="LossLimits"/> covers breach-style prop firms (FTMO/Axi/Other) — unchanged
/// from today. <see cref="VarTarget"/> covers Darwinex Zero's monthly VaR-target rulebook
/// (<c>.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md</c> §1-§3), which has NO breach semantics:
/// missing the target rescales leverage, it does not terminate the account.
/// </summary>
public enum GuardrailKind
{
    LossLimits = 0,
    VarTarget = 1,
}
