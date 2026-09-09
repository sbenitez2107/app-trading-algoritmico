namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// FTMO's challenge product. Optional on write — a <see cref="GuardrailKind.LossLimits"/> row with
/// <see cref="AppTradingAlgoritmico.Domain.Entities.BrokerRiskLimits.FundingService"/> =
/// <see cref="FundingService.Ftmo"/> and no product is accepted; the invariant pairing with
/// <see cref="DrawdownModel"/> (<see cref="OneStep"/> ⇒ <see cref="DrawdownModel.Trailing"/>,
/// <see cref="TwoStep"/> ⇒ <see cref="DrawdownModel.Static"/>) is enforced only when non-null.
/// </summary>
public enum FtmoProduct
{
    OneStep = 0,
    TwoStep = 1,
}
