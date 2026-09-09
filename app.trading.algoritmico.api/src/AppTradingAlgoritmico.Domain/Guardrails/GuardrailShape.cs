using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Guardrails;

/// <summary>
/// Single source of the `Kind` &lt;-&gt; `FundingService` rulebook binding
/// (`funding-guardrails` spec — "Kind Bound to FundingService", "FtmoProduct Discriminator"). The
/// write path (<see cref="Infrastructure.Services.RiskLimitsService"/>, via the Infrastructure
/// project) calls this to REJECT a mismatched upsert; the read path calls the same policy to FLAG a
/// pre-existing mismatched row as <c>RulebookMismatch = true</c> instead of throwing. One rule, two
/// consumers — write-reject and read-flag cannot drift.
/// </summary>
public static class GuardrailShape
{
    /// <summary>
    /// The <see cref="GuardrailKind"/> a <see cref="FundingService"/> requires, or <c>null</c> when
    /// unconstrained (<see cref="FundingService.Other"/> is the escape hatch for unmodelled
    /// services).
    /// </summary>
    public static GuardrailKind? RequiredKindFor(FundingService service) => service switch
    {
        FundingService.Ftmo => GuardrailKind.LossLimits,
        FundingService.Axi => GuardrailKind.StagedLossLimits,
        FundingService.DarwinexZero => GuardrailKind.VarTarget,
        FundingService.Other => null,
        _ => null,
    };

    /// <summary>True when <paramref name="kind"/> is the required kind for <paramref name="service"/>, or the service is unconstrained.</summary>
    public static bool IsShapeConsistent(FundingService service, GuardrailKind kind)
    {
        var required = RequiredKindFor(service);
        return required is null || required.Value == kind;
    }

    /// <summary>
    /// FTMO's product/drawdown pairing invariant: <see cref="FtmoProduct.OneStep"/> requires
    /// <see cref="DrawdownModel.Trailing"/>; <see cref="FtmoProduct.TwoStep"/> requires
    /// <see cref="DrawdownModel.Static"/>. A <c>null</c> product is always consistent — the
    /// invariant is enforced only when the product is known (`funding-guardrails` spec —
    /// "FtmoProduct Discriminator").
    /// </summary>
    public static bool IsFtmoProductConsistent(FtmoProduct? product, DrawdownModel? drawdownModel)
    {
        if (product is null)
            return true;

        return product.Value switch
        {
            FtmoProduct.OneStep => drawdownModel == DrawdownModel.Trailing,
            FtmoProduct.TwoStep => drawdownModel == DrawdownModel.Static,
            _ => true,
        };
    }
}
