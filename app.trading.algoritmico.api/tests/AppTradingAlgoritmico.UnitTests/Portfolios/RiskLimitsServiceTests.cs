using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

    // ---- Binding: Kind bound to FundingService (funding-guardrails — "Kind Bound to FundingService") ----

    private static UpsertBrokerRiskLimitsDto StagedLossLimitsDto(
        string broker = "Axi",
        IReadOnlyList<UpsertFundingStageLimitDto>? stages = null) => new(
            Broker: broker,
            FundingService: FundingService.Axi,
            Kind: GuardrailKind.StagedLossLimits,
            DailyLossLimitPct: null,
            MaxLossLimitPct: null,
            ProfitTargetPct: null,
            DrawdownModel: null,
            TargetVarPct: null,
            VarFloorPct: null,
            Verified: true,
            Stages: stages ?? SixStages());

    private static List<UpsertFundingStageLimitDto> SixStages() =>
        Enumerable.Range(1, 6)
            .Select(i => new UpsertFundingStageLimitDto(i, $"Stage {i}", 0.05m + i * 0.01m, i == 6 ? null : 0.08m))
            .ToList();

    [Fact]
    public async Task UpsertAsync_DarwinexZeroWithLossLimitsKind_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto() with { Kind = GuardrailKind.LossLimits };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_AxiWithLossLimitsKind_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto(broker: "Axi") with { FundingService = FundingService.Axi };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_FtmoWithVarTargetKind_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto(broker: "FTMO") with { FundingService = FundingService.Ftmo };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- FtmoProduct discriminator (funding-guardrails — "FtmoProduct Discriminator") ----

    [Fact]
    public async Task UpsertAsync_FtmoOneStepWithStatic_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto() with { FtmoProduct = FtmoProduct.OneStep, DrawdownModel = DrawdownModel.Static };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_FtmoTwoStepWithStatic_Accepted()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto() with { FtmoProduct = FtmoProduct.TwoStep, DrawdownModel = DrawdownModel.Static };

        var result = await sut.UpsertAsync(dto);

        result.RulebookMismatch.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_FtmoWithNullProduct_Accepted()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto() with { FtmoProduct = null };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().NotThrowAsync();
    }

    // ---- DrawdownModel normalization (D4) ----

    [Fact]
    public async Task UpsertAsync_VarTargetCarryingDrawdownModelTrailing_PersistsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto() with { DrawdownModel = DrawdownModel.Trailing };

        var result = await sut.UpsertAsync(dto);

        result.DrawdownModel.Should().BeNull();
    }

    // ---- StagedLossLimits stage rulebook ----

    [Fact]
    public async Task UpsertAsync_StagedLossLimitsSixStages_PersistsAllSix()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto();

        var result = await sut.UpsertAsync(dto);

        result.Kind.Should().Be(GuardrailKind.StagedLossLimits);
        result.Stages.Should().HaveCount(6);
        result.Stages!.Select(s => s.MaxLossLimitPct).Should().OnlyContain(p => p > 0);
    }

    [Fact]
    public async Task UpsertAsync_StagedLossLimitsDuplicateOrdinal_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var stages = new List<UpsertFundingStageLimitDto>
        {
            new(1, "Stage 1", 0.06m, 0.08m),
            new(1, "Stage 1 dup", 0.06m, 0.08m),
        };
        var dto = StagedLossLimitsDto(stages: stages);

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_StagedLossLimitsWithParentScalar_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto() with { MaxLossLimitPct = 0.10m };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- RulebookMismatch — tolerant read of pre-existing mismatched rows (D2) ----

    [Fact]
    public async Task GetByBrokerAsync_LegacyAxiLossLimitsRow_FlagsMismatchWithoutThrowing()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "Axi",
            FundingService = FundingService.Axi,
            Kind = GuardrailKind.LossLimits,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = 0.10m,
            DrawdownModel = DrawdownModel.Static,
            Verified = true,
        });
        await db.SaveChangesAsync();

        var sut = new RiskLimitsService(db);
        var result = await sut.GetByBrokerAsync("Axi");

        result.Should().NotBeNull();
        result!.RulebookMismatch.Should().BeTrue();
    }

    [Fact]
    public async Task GetByBrokerAsync_FtmoRowWithNullProduct_FlagsMismatch()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "FTMO",
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = 0.10m,
            DrawdownModel = DrawdownModel.Static,
            FtmoProduct = null,
            Verified = true,
        });
        await db.SaveChangesAsync();

        var sut = new RiskLimitsService(db);
        var result = await sut.GetByBrokerAsync("FTMO");

        result.Should().NotBeNull();
        result!.RulebookMismatch.Should().BeTrue();
    }

    // ---- Stage collection is valid ONLY on StagedLossLimits (funding-guardrails —
    // "Kind Determines Valid Field Set"; `BrokerRiskLimits.Stages` — "Empty for every other kind") ----

    private static List<UpsertFundingStageLimitDto> OneStage(
        decimal maxLossLimitPct = 0.06m,
        decimal? profitTargetPct = 0.08m) =>
        [new UpsertFundingStageLimitDto(1, "Stage 1", maxLossLimitPct, profitTargetPct)];

    [Fact]
    public async Task UpsertAsync_StagesOnVarTargetPayload_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto() with { Stages = OneStage() };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_StagesOnVarTargetPayload_PersistsNoGuardrailAtAll()
    {
        // Pins the consequence the rejection prevents: Axi stage rows parented to a Darwinex Zero
        // guardrail. Rejecting before the write is what keeps `Stages` empty for a non-staged kind.
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = VarTargetDto() with { Stages = OneStage() };

        try
        {
            await sut.UpsertAsync(dto);
        }
        catch (ArgumentException)
        {
            // expected — the assertion below is about what did NOT reach the database
        }

        var persisted = await db.BrokerRiskLimits.Include(x => x.Stages).ToListAsync();
        persisted.SelectMany(x => x.Stages).Should().BeEmpty();
    }

    // ---- DrawdownModel: rejected on StagedLossLimits, normalized on VarTarget (D4 asymmetry) ----

    [Fact]
    public async Task UpsertAsync_StagedLossLimitsWithoutDrawdownModel_Accepted()
    {
        // The other half of the rejection below: proves the guard is a real discriminator and not
        // one that fires on every StagedLossLimits payload.
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var result = await sut.UpsertAsync(StagedLossLimitsDto());

        result.Kind.Should().Be(GuardrailKind.StagedLossLimits);
        result.DrawdownModel.Should().BeNull();
        result.Stages.Should().HaveCount(6);
    }

    [Fact]
    public async Task UpsertAsync_LossLimitsWithoutDrawdownModel_Rejected()
    {
        // `funding-guardrails` spec — "LossLimits payload without DrawdownModel rejected".
        // Only expressible now that the upsert DTO's DrawdownModel is nullable.
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = LossLimitsDto() with { DrawdownModel = null };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_DrawdownModelOnStagedLossLimitsPayload_Rejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto() with { DrawdownModel = DrawdownModel.Static };

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- Stage percentages are fractions in (0, 1], same as the VarTarget pair ----

    [Theory]
    [InlineData(5.0)]    // 500% — a percent written as a whole number instead of a fraction
    [InlineData(1.01)]
    [InlineData(0)]
    [InlineData(-0.01)]
    public async Task UpsertAsync_StageMaxLossOutsideValidRange_Rejected(double invalidPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto(stages: OneStage(maxLossLimitPct: (decimal)invalidPct));

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(1.01)]
    [InlineData(0)]
    [InlineData(-0.01)]
    public async Task UpsertAsync_StageProfitTargetOutsideValidRange_Rejected(double invalidPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto(stages: OneStage(profitTargetPct: (decimal)invalidPct));

        var act = async () => await sut.UpsertAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_StageProfitTargetNull_Accepted()
    {
        // A stage with no profit target is legal (Axi's final/Master stage) — the (0, 1] bound
        // applies to a value that IS supplied, never to its absence.
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var dto = StagedLossLimitsDto(stages: OneStage(profitTargetPct: null));

        var result = await sut.UpsertAsync(dto);

        result.Stages.Should().ContainSingle().Which.ProfitTargetPct.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_AxiReferenceStageLimits_Accepted()
    {
        // Axi Select's real stage max losses are -7% and -10% (KB — SERVICE_Axi_Select.md), i.e.
        // 0.07 and 0.10 as fractions. The bound must not reject the values it exists to protect.
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new RiskLimitsService(db);

        var stages = new List<UpsertFundingStageLimitDto>
        {
            new(1, "Stage 1", 0.07m, 0.08m),
            new(2, "Master", 0.10m, null),
        };
        var dto = StagedLossLimitsDto(stages: stages);

        var result = await sut.UpsertAsync(dto);

        result.Stages!.Select(s => s.MaxLossLimitPct).Should().Equal(0.07m, 0.10m);
    }
}
