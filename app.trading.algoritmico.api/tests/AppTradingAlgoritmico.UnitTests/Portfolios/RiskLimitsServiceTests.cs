using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Kind-aware validation for <see cref="RiskLimitsService.UpsertAsync"/>
/// (`funding-guardrails` spec — "Kind Determines Valid Field Set" and
/// "VarTarget Percentage Validation"). Uses the EF InMemory provider.
/// </summary>
public class RiskLimitsServiceTests
{
    private static UpsertBrokerRiskLimitsDto LossLimitsDto(
        string broker = "FTMO",
        decimal? dailyLossLimitPct = 0.05m,
        decimal? maxLossLimitPct = 0.10m,
        decimal? profitTargetPct = 0.10m,
        decimal? targetVarPct = null,
        decimal? varFloorPct = null) => new(
            Broker: broker,
            FundingService: FundingService.Ftmo,
            Kind: GuardrailKind.LossLimits,
            DailyLossLimitPct: dailyLossLimitPct,
            MaxLossLimitPct: maxLossLimitPct,
            ProfitTargetPct: profitTargetPct,
            DrawdownModel: DrawdownModel.Static,
            TargetVarPct: targetVarPct,
            VarFloorPct: varFloorPct,
            Verified: true);

    private static UpsertBrokerRiskLimitsDto VarTargetDto(
        string broker = "Darwinex",
        decimal? targetVarPct = 0.065m,
        decimal? varFloorPct = 0.0325m,
        decimal? dailyLossLimitPct = null,
        decimal? maxLossLimitPct = null,
        decimal? profitTargetPct = null) => new(
            Broker: broker,
            FundingService: FundingService.DarwinexZero,
            Kind: GuardrailKind.VarTarget,
            DailyLossLimitPct: dailyLossLimitPct,
            MaxLossLimitPct: maxLossLimitPct,
            ProfitTargetPct: profitTargetPct,
            DrawdownModel: DrawdownModel.Static,
            TargetVarPct: targetVarPct,
            VarFloorPct: varFloorPct,
            Verified: true);

    [Fact]
    public async Task UpsertAsync_VarFieldsOnLossLimitsPayload_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto(targetVarPct: 0.065m);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_LossFieldsOnVarTargetPayload_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(dailyLossLimitPct: 0.05m);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_VarFloorAboveTarget_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(targetVarPct: 0.065m, varFloorPct: 0.10m);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(1.5)]
    public async Task UpsertAsync_VarPercentOutsideValidRange_Rejected(double invalidPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(targetVarPct: (decimal)invalidPct, varFloorPct: 0.0325m);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_VarTargetMissingOneField_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(targetVarPct: 0.065m, varFloorPct: null);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_ValidVarTargetPair_PersistsUnchanged()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(targetVarPct: 0.065m, varFloorPct: 0.0325m);

        var result = await sut.UpsertAsync(dto);

        result.Kind.Should().Be(GuardrailKind.VarTarget);
        result.TargetVarPct.Should().Be(0.065m);
        result.VarFloorPct.Should().Be(0.0325m);
        result.DailyLossLimitPct.Should().BeNull();
        result.MaxLossLimitPct.Should().BeNull();
        result.ProfitTargetPct.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_ValidLossLimitsPayload_PersistsUnchanged()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto(dailyLossLimitPct: 0.05m, maxLossLimitPct: 0.10m, profitTargetPct: 0.10m);

        var result = await sut.UpsertAsync(dto);

        result.Kind.Should().Be(GuardrailKind.LossLimits);
        result.DailyLossLimitPct.Should().Be(0.05m);
        result.TargetVarPct.Should().BeNull();
        result.VarFloorPct.Should().BeNull();
    }
}
