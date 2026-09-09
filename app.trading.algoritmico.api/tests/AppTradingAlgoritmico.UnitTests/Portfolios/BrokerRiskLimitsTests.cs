using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Entity-level defaults for the <see cref="GuardrailKind"/> discriminator. A new row (as an
/// existing pre-migration row would appear after the additive migration defaults it) must be
/// <see cref="GuardrailKind.LossLimits"/> with the VarTarget-only fields left unset.
/// </summary>
public class BrokerRiskLimitsTests
{
    [Fact]
    public void New_DefaultsToLossLimitsKind_WithNullVarFields()
    {
        var entity = new BrokerRiskLimits { Broker = "FTMO" };

        entity.Kind.Should().Be(GuardrailKind.LossLimits, "existing/new rows default to the breach-style kind");
        entity.TargetVarPct.Should().BeNull("VarTarget fields are unset for a LossLimits row");
        entity.VarFloorPct.Should().BeNull("VarTarget fields are unset for a LossLimits row");
    }

    [Fact]
    public void New_VarTargetKind_CanCarryVarFields()
    {
        var entity = new BrokerRiskLimits
        {
            Broker = "Darwinex",
            Kind = GuardrailKind.VarTarget,
            TargetVarPct = 0.065m,
            VarFloorPct = 0.0325m,
        };

        entity.Kind.Should().Be(GuardrailKind.VarTarget);
        entity.TargetVarPct.Should().Be(0.065m);
        entity.VarFloorPct.Should().Be(0.0325m);
    }

    [Fact]
    public void New_StagedLossLimitsKind_CanCarryStagesAndNoFtmoProduct()
    {
        var entity = new BrokerRiskLimits
        {
            Broker = "Axi",
            FundingService = FundingService.Axi,
            Kind = GuardrailKind.StagedLossLimits,
            DrawdownModel = null,
        };
        entity.Stages.Add(new FundingStageLimit { StageOrdinal = 1, StageName = "Stage 1", MaxLossLimitPct = 0.06m });

        entity.Kind.Should().Be(GuardrailKind.StagedLossLimits);
        entity.DrawdownModel.Should().BeNull();
        entity.FtmoProduct.Should().BeNull();
        entity.Stages.Should().ContainSingle(s => s.MaxLossLimitPct == 0.06m);
    }
}
