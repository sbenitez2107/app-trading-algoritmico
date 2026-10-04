using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B3 (B2 review follow-up RELIABILITY-001) — a member that HOLDS a run of the kind but has
/// zero projected rows has no range, so the intersection is undefined. It used to read as an unattributed
/// <c>NoCommonWindow</c> (which blames nobody); it must name the member as <c>MemberHasNoTradesInWindow</c>.
/// A genuinely disjoint set of ranges still is <c>NoCommonWindow</c>.
/// </summary>
public class FtmoGroupEmptyMemberTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid IdC = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    private static DateTime At(int day, int hour) => new(2026, 1, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(int row, DateTime open, DateTime close) =>
        new(row, open, close, 10m, ResizeOutcome.OnTarget, 1m);

    private static GroupMemberKindInput Member(Guid id, int order, params ProjectedTrade[] trades) =>
        new(id, $"member-{order}", order, Guid.NewGuid(), null,
            new FtmoSimulationInputs.RunProjection(null, BacktestSegment.InSample, [.. trades], [.. trades], 0, 0, 0));

    private static FtmoGroupKindResultDto Compute(params GroupMemberKindInput[] members) =>
        ComputeGroup(
            BacktestRunKind.Deploy, members,
            new GroupParams(Jerusalem, Berlin, 10_000m, 0.05m, 0.10m, null, (1m, 1m), new FtmoChallengeRulesDto(0.10m, 0.05m, 4, null)),
            CancellationToken.None);

    private static GroupMemberKindInput Healthy(Guid id, int order) =>
        Member(id, order, Trade(0, At(12, 8), At(12, 9)), Trade(1, At(14, 8), At(14, 9)));

    [Fact]
    public void ComputeGroup_AMemberWithAHeldRunButNoRows_IsNamedNotBlamedOnTheWindow()
    {
        var result = Compute(Healthy(IdA, 0), Member(IdB, 1), Healthy(IdC, 2));

        result.Status.Should().Be(FtmoSimulationStatus.Refused);
        result.Refusal.Should().Be(FtmoGroupRefusal.MemberHasNoTradesInWindow);
        result.MemberRefusals.Should().ContainSingle()
            .Which.Should().Be(new FtmoGroupMemberRefusalDto(IdB, "member-1", FtmoGroupRefusal.MemberHasNoTradesInWindow, null));
        result.Window.Should().BeNull("with no range there is no window to echo");
        result.Run.Should().BeNull();
        result.Diagnostics.Should().BeNull();
    }

    [Fact]
    public void ComputeGroup_EveryEmptyMemberIsListed_AndTheCoverageEchoesTheEmptyOneWithNoDates()
    {
        var result = Compute(Member(IdA, 0), Healthy(IdB, 1), Member(IdC, 2));

        result.Refusal.Should().Be(FtmoGroupRefusal.MemberHasNoTradesInWindow);
        result.MemberRefusals.Select(r => r.StrategyId).Should().Equal(IdA, IdC);
        result.Coverage.Should().HaveCount(3);
        result.Coverage[0].Should().Be(new FtmoGroupMemberCoverageDto(IdA, "member-0", null, null, 0));
        result.Coverage[1].FirstOpen.Should().Be(At(12, 8));
    }

    [Fact]
    public void ComputeGroup_GenuinelyDisjointRanges_AreStillANoCommonWindowThatBlamesNoOne()
    {
        var disjoint = Member(IdB, 1, Trade(0, At(20, 8), At(20, 9)));

        var result = Compute(Healthy(IdA, 0), disjoint);

        result.Refusal.Should().Be(FtmoGroupRefusal.NoCommonWindow);
        result.MemberRefusals.Should().BeEmpty();
    }

    [Fact]
    public async Task ThroughTheService_AHeldRunWithZeroTrades_NamesTheMemberAndNeverProducesAnUnattributedRefusal()
    {
        // Documents the end-to-end behaviour: the shipped projection already refuses an empty run
        // (RiskNotEstimable), so the member is named under MemberRunRefused before the window is ever computed.
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "FTMO",
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            Verified = true,
        });
        db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
        {
            SqxSymbol = "EMPTY_GRP",
            FtmoSymbol = "EMPTY_GRP",
            ContractSize = 100m,
            ProfitCurrency = "USD",
            SizeDecimals = 2,
            Step = 0.01m,
            MinLot = 0.01m,
            MaxLots = 1000m,
            SourceTimeZoneId = "Asia/Jerusalem",
            Provenance = "test",
            CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Symbol = "EMPTY_GRP",
            Status = CalibrationStatus.Calibrated,
            PointValue = 100m,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        });
        db.Strategies.Add(new Strategy { Id = IdA, Name = "empty", CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
        db.BacktestRuns.Add(new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "empty.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = IdA,
            Kind = BacktestRunKind.Deploy,
            Symbol = "EMPTY_GRP",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await new FtmoGroupSimulationReadService(db).SimulateAsync(
            new FtmoGroupSimulationParameters([IdA], "FTMO", 10_000m, 100m, null, null, 2, 0.01m, 0.01m, 10m), CancellationToken.None);

        var deploy = result.Kinds.Single(k => k.Kind == BacktestRunKind.Deploy);
        deploy.Refusal.Should().NotBe(FtmoGroupRefusal.NoCommonWindow);
        deploy.MemberRefusals.Should().ContainSingle().Which.StrategyId.Should().Be(IdA);
    }
}
