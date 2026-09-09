using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Domain.Guardrails;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Pure policy tests for <see cref="GuardrailShape"/> (`funding-guardrails` spec — "Kind Bound to
/// FundingService" and "FtmoProduct Discriminator"). One rule, consumed by both the write-reject
/// path (<see cref="Infrastructure.Services.RiskLimitsService"/>) and the read-flag path.
/// </summary>
public class GuardrailShapeTests
{
    [Theory]
    [InlineData(FundingService.Ftmo, GuardrailKind.LossLimits)]
    [InlineData(FundingService.Axi, GuardrailKind.StagedLossLimits)]
    [InlineData(FundingService.DarwinexZero, GuardrailKind.VarTarget)]
    public void RequiredKindFor_KnownService_ReturnsBoundKind(FundingService service, GuardrailKind expected)
    {
        GuardrailShape.RequiredKindFor(service).Should().Be(expected);
    }

    [Fact]
    public void RequiredKindFor_Other_ReturnsNull()
    {
        GuardrailShape.RequiredKindFor(FundingService.Other).Should().BeNull();
    }

    [Theory]
    [InlineData(FundingService.Ftmo, GuardrailKind.LossLimits, true)]
    [InlineData(FundingService.DarwinexZero, GuardrailKind.LossLimits, false)]
    [InlineData(FundingService.Axi, GuardrailKind.LossLimits, false)]
    [InlineData(FundingService.Ftmo, GuardrailKind.VarTarget, false)]
    [InlineData(FundingService.Other, GuardrailKind.LossLimits, true)]
    [InlineData(FundingService.Other, GuardrailKind.StagedLossLimits, true)]
    public void IsShapeConsistent_MatchesRequiredKindFor(FundingService service, GuardrailKind kind, bool expected)
    {
        GuardrailShape.IsShapeConsistent(service, kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(FtmoProduct.OneStep, DrawdownModel.Static, false)]
    [InlineData(FtmoProduct.OneStep, DrawdownModel.Trailing, true)]
    [InlineData(FtmoProduct.TwoStep, DrawdownModel.Static, true)]
    [InlineData(FtmoProduct.TwoStep, DrawdownModel.Trailing, false)]
    public void IsFtmoProductConsistent_NonNullProduct_EnforcesDrawdownModelPairing(
        FtmoProduct product, DrawdownModel drawdownModel, bool expected)
    {
        GuardrailShape.IsFtmoProductConsistent(product, drawdownModel).Should().Be(expected);
    }

    [Theory]
    [InlineData(DrawdownModel.Static)]
    [InlineData(DrawdownModel.Trailing)]
    [InlineData(null)]
    public void IsFtmoProductConsistent_NullProduct_AlwaysTrue(DrawdownModel? drawdownModel)
    {
        GuardrailShape.IsFtmoProductConsistent(null, drawdownModel).Should().BeTrue();
    }
}
