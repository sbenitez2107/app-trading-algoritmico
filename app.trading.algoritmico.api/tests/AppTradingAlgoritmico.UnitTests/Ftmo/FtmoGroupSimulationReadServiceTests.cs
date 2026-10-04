using System.Data.Common;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.3 (design.md D5) — <see cref="FtmoGroupSimulationReadService"/> end to end over
/// the EF in-memory provider (no real database is ever touched), except the query-count fence, which needs
/// a relational provider to count commands and therefore runs on in-memory SQLite.
/// </summary>
public class FtmoGroupSimulationReadServiceTests
{
    private const string Broker = "FTMO";
    private const string Gold = "XAUUSD_GRP";
    private const string Dax = "GER40_GRP";
    private const string Nasdaq = "NAS100_GRP";
    private const string NoSpec = "NOSPEC_GRP";
    private static readonly DateTime SafeDay = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    // ---- fixtures ----

    private static BacktestTrade MakeTrade(int row, string symbol, decimal profit, DateTime open, DateTime close) => new()
    {
        RowIndex = row,
        Ticket = row + 1,
        Symbol = symbol,
        Type = "Long",
        OpenTime = open,
        OpenPrice = 100m,
        Size = 1.00m,
        CloseTime = close,
        ClosePrice = 101m,
        Profit = profit,
        Balance = 10_000m,
        SampleTypeRaw = "InSample",
        Segment = BacktestSegment.InSample,
        CloseType = profit < 0m ? "SL" : "TP",
        RealizedRisk = profit < 0m ? 100m : null,
    };

    /// <summary>
    /// <paramref name="count"/> daily rows in strict (Open, row) order with a distinct close each, so there is
    /// no same-close tie. The first three are SL rows that let the risk normalizer estimate Â = 100.
    /// </summary>
    private static List<BacktestTrade> OrderedRun(string symbol, int count, int shift = 0, int startDay = 0)
    {
        var trades = new List<BacktestTrade>(count);
        for (var i = 0; i < count; i++)
        {
            var close = SafeDay.AddDays(startDay + i);
            var profit = i < 3 ? -1m : ((i + shift) % 7 == 3 ? 400m : ((i + shift) % 5 == 2 ? -150m : 20m));
            trades.Add(MakeTrade(i, symbol, profit, close.AddHours(-1), close));
        }

        return trades;
    }

    private static async Task SeedStrategyAsync(AppDbContext db, Guid id, string name)
    {
        db.Strategies.Add(new Strategy { Id = id, Name = name, CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
        await db.SaveChangesAsync();
    }

    private static async Task SeedRunAsync(
        AppDbContext db, Guid strategyId, BacktestRunKind kind, string symbol, IEnumerable<BacktestTrade> trades)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "group.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = kind,
            Symbol = symbol,
            CreatedAt = DateTime.UtcNow,
        };
        db.BacktestRuns.Add(run);
        foreach (var trade in trades)
        {
            trade.BacktestRunId = run.Id;
            db.BacktestTrades.Add(trade);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedLimitsAsync(
        AppDbContext db, FtmoProduct? product = FtmoProduct.TwoStep, decimal? dailyPct = 0.05m)
    {
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = product,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = dailyPct,
            MaxLossLimitPct = 0.10m,
            Verified = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSymbolAsync(
        AppDbContext db, string symbol, string currency = "USD", string zone = "Asia/Jerusalem", bool spec = true, bool calibrated = true)
    {
        if (spec)
        {
            db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
            {
                SqxSymbol = symbol,
                FtmoSymbol = symbol,
                ContractSize = 100m,
                ProfitCurrency = currency,
                SizeDecimals = 2,
                Step = 0.01m,
                MinLot = 0.01m,
                MaxLots = 1000m,
                SourceTimeZoneId = zone,
                Provenance = "test",
                CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        }

        if (calibrated)
        {
            db.SymbolCalibrations.Add(new SymbolCalibration
            {
                Symbol = symbol,
                Status = CalibrationStatus.Calibrated,
                PointValue = 100m,
                SampleCount = 10,
                CalibratedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>A member with a Deploy and an Evaluation run (each 90 daily rows from the same day).</summary>
    private static async Task SeedMemberAsync(
        AppDbContext db, Guid id, string name, string symbol = Gold, int shift = 0, bool deploy = true, bool evaluation = true,
        int startDay = 0, int count = 90)
    {
        await SeedStrategyAsync(db, id, name);
        if (deploy)
            await SeedRunAsync(db, id, BacktestRunKind.Deploy, symbol, OrderedRun(symbol, count, shift, startDay));
        if (evaluation)
            await SeedRunAsync(db, id, BacktestRunKind.Evaluation, symbol, OrderedRun(symbol, count, shift + 2, startDay));
    }

    private static async Task SeedWorldAsync(AppDbContext db)
    {
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold);
    }

    private static FtmoGroupSimulationParameters Params(
        IEnumerable<Guid> ids, decimal capital = 10_000m, decimal risk = 100m, decimal? fxLow = null, decimal? fxHigh = null,
        int sizeDecimals = 2, decimal step = 0.01m) =>
        new([.. ids], Broker, capital, risk, fxLow, fxHigh, sizeDecimals, step, MinLot: 0.01m, MaxLots: 10m);

    private static FtmoBreachSimulationRequest SingleRequest(Guid strategyId, string symbol = Gold) =>
        new(strategyId, Broker, symbol, InitialCapital: 10_000m, TargetRiskPerTrade: 100m,
            FxLow: null, FxHigh: null, SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m);

    private static Task<FtmoGroupSimulationDto> Simulate(AppDbContext db, FtmoGroupSimulationParameters p, CancellationToken ct = default)
        => new FtmoGroupSimulationReadService(db).SimulateAsync(p, ct);

    private static void AssertGroupWideRefusal(FtmoGroupSimulationDto result, FtmoGroupRefusal refusal)
    {
        result.Status.Should().Be(FtmoSimulationStatus.Refused);
        result.Refusal.Should().Be(refusal);
        result.Kinds.Should().BeEmpty("a group-wide refusal refuses the whole request");
        result.Disclosures.Should().Equal(FtmoGroupSimulationLimits.Disclosures);
    }

    /// <summary>True when two trades share a close instant and their row order differs from their open order.</summary>
    private static bool HasDisagreeingCloseTie(IReadOnlyList<BacktestTrade> trades) =>
        trades.Any(x => trades.Any(y =>
            x.RowIndex < y.RowIndex && x.CloseTime == y.CloseTime && x.OpenTime > y.OpenTime));

    private static bool IsInOpenOrder(IReadOnlyList<BacktestTrade> trades) =>
        trades.OrderBy(t => t.RowIndex).Zip(trades.OrderBy(t => t.RowIndex).Skip(1), (a, b) => a.OpenTime <= b.OpenTime).All(x => x);

    // ---- B2.3.1: one-member equivalence ----

    [Theory]
    [InlineData(BacktestRunKind.Deploy)]
    [InlineData(BacktestRunKind.Evaluation)]
    public async Task OneMemberGroup_EqualsTheSingleStrategyMultiStart_InEveryFieldExceptRunId(BacktestRunKind kind)
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "solo");

        // The fixture meets the equivalence criterion (and the test asserts it): rows in (Open, row) order
        // and no two trades sharing a close instant.
        var fixture = OrderedRun(Gold, 90, shift: kind == BacktestRunKind.Deploy ? 0 : 2);
        IsInOpenOrder(fixture).Should().BeTrue();
        HasDisagreeingCloseTie(fixture).Should().BeFalse();

        var single = (await new FtmoMultiStartReadService(db).SimulateAsync(SingleRequest(Id(1)), CancellationToken.None))
            .Runs.Single(r => r.Kind == kind);
        var group = (await Simulate(db, Params([Id(1)]))).Kinds.Single(k => k.Kind == kind);

        single.Starts.Count.Should().BeGreaterThan(1, "an empty comparison would prove nothing");
        group.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        group.Run!.RunId.Should().Be(Guid.Empty);
        group.Run.Should().BeEquivalentTo(single, o => o.ComparingRecordsByMembers().Excluding(r => r.RunId));
    }

    [Fact]
    public async Task OneMemberGroup_WhenTwoTradesShareACloseAgainstOpenOrder_MakesNoEqualityClaim()
    {
        // X (row 0) opens LATER than Y (row 1) but both close at T. The single path breaks the close tie by
        // file row (X first: -600 then +600 breaches the 5% daily limit); the group renumbers by Open (Y first:
        // +600 then -600 does not). Documented, never claimed equal (design.md D4, spec "one-member").
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedStrategyAsync(db, Id(1), "tie");
        var t = SafeDay.AddDays(10);
        var trades = new List<BacktestTrade>
        {
            MakeTrade(0, Gold, -600m, t.AddHours(-1), t),
            MakeTrade(1, Gold, 600m, t.AddHours(-3), t),
            MakeTrade(2, Gold, -1m, SafeDay.AddHours(-1), SafeDay),
            MakeTrade(3, Gold, -1m, SafeDay.AddDays(1).AddHours(-1), SafeDay.AddDays(1)),
            MakeTrade(4, Gold, -1m, SafeDay.AddDays(2).AddHours(-1), SafeDay.AddDays(2)),
            MakeTrade(5, Gold, 10m, t.AddDays(3).AddHours(-1), t.AddDays(3)),
        };
        HasDisagreeingCloseTie(trades).Should().BeTrue("this fixture exists to contain the tie");
        await SeedRunAsync(db, Id(1), BacktestRunKind.Deploy, Gold, trades);

        var single = (await new FtmoMultiStartReadService(db).SimulateAsync(SingleRequest(Id(1)), CancellationToken.None)).Runs.Single();
        var group = (await Simulate(db, Params([Id(1)]))).Kinds.Single(k => k.Kind == BacktestRunKind.Deploy).Run!;

        group.Should().NotBeEquivalentTo(single, o => o.ComparingRecordsByMembers().Excluding(r => r.RunId));
    }

    [Fact]
    public async Task OneMemberGroup_WhoseSymbolHasNoSpec_RefusesWithTheSameReasonAsTheSingleEndpoint()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, NoSpec, spec: false, calibrated: false);
        await SeedMemberAsync(db, Id(1), "nospec", NoSpec);

        var single = (await new FtmoMultiStartReadService(db).SimulateAsync(SingleRequest(Id(1), NoSpec), CancellationToken.None)).Runs;
        var group = await Simulate(db, Params([Id(1)]));

        single.Should().OnlyContain(r => r.Status == FtmoSimulationStatus.Refused && r.Refusal == FtmoSimulationRefusal.InstrumentSpecMissing);
        group.Kinds.Should().HaveCount(2);
        foreach (var kind in group.Kinds)
        {
            kind.Status.Should().Be(FtmoSimulationStatus.Refused);
            kind.Refusal.Should().Be(FtmoGroupRefusal.MemberRunRefused);
            kind.MemberRefusals.Should().ContainSingle()
                .Which.RunReason.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
        }
    }

    // ---- B2.3.2: dedup, order, names ----

    [Fact]
    public async Task RepeatedId_CountsOnce_IsEchoedAsRemoved_AndMembersAreSortedByStrategyId()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");
        await SeedMemberAsync(db, Id(2), "b", shift: 1);

        var result = await Simulate(db, Params([Id(2), Id(1), Id(2)]));
        var clean = await Simulate(db, Params([Id(1), Id(2)]));

        result.Members.Select(m => m.StrategyId).Should().Equal(Id(1), Id(2));
        result.Members.Select(m => m.MemberOrder).Should().Equal(0, 1);
        result.DuplicateIdsRemoved.Should().Equal(Id(2));
        clean.DuplicateIdsRemoved.Should().BeEmpty();
        result.Kinds.Should().BeEquivalentTo(clean.Kinds, o => o.ComparingRecordsByMembers(), "B's trades appear once: k is 2");
    }

    [Fact]
    public async Task RequestOrder_DoesNotChangeTheResponse()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");
        await SeedMemberAsync(db, Id(2), "b", shift: 1);
        await SeedMemberAsync(db, Id(3), "c", shift: 3);

        var first = await Simulate(db, Params([Id(1), Id(2), Id(3)]));
        var second = await Simulate(db, Params([Id(3), Id(1), Id(2)]));

        second.Should().BeEquivalentTo(first, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public async Task TheSameNameOnTwoStrategies_DifferingOnlyInCase_WarnsForBothAndKeepsBothMembers()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "Gold System");
        await SeedMemberAsync(db, Id(2), "gold system", shift: 1);
        await SeedMemberAsync(db, Id(3), "other", shift: 2);

        var result = await Simulate(db, Params([Id(1), Id(2), Id(3)]));

        result.Members.Should().HaveCount(3);
        var warning = result.DuplicateNameWarnings.Should().ContainSingle().Subject;
        warning.StrategyIds.Should().Equal(Id(1), Id(2));
        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);
    }

    // ---- B2.3.3: group-wide refusals ----

    [Fact]
    public async Task AnUnknownStrategyId_RefusesTheWholeRequestNamingTheId()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");

        var result = await Simulate(db, Params([Id(1), Id(9)]));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.MemberNotFound);
        result.UnknownStrategyIds.Should().Equal(Id(9));
    }

    [Theory]
    [InlineData(0, 100, 2, 0.01)]
    [InlineData(10_000, 0, 2, 0.01)]
    [InlineData(10_000, 100, 2, 0)]
    public async Task APresentButUnusableValue_RefusesTheWholeRequestAsInvalidRequest(
        double capital, double risk, int sizeDecimals, double step)
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");

        var result = await Simulate(db, Params([Id(1)], (decimal)capital, (decimal)risk, sizeDecimals: sizeDecimals, step: (decimal)step));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.InvalidRequest);
    }

    [Fact]
    public async Task UnconfiguredLimits_RefuseTheWholeRequestAsSharedInputsRefused()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedSymbolAsync(db, Gold);
        await SeedMemberAsync(db, Id(1), "a");

        var result = await Simulate(db, Params([Id(1)]));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.SharedInputsRefused);
        result.SharedRefusal.Should().Be(FtmoSimulationRefusal.LimitsNotConfigured);
    }

    [Fact]
    public async Task ANonTwoStepProduct_RefusesTheWholeRequestCarryingTheLossLimits()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db, product: FtmoProduct.OneStep);
        await SeedSymbolAsync(db, Gold);
        await SeedMemberAsync(db, Id(1), "a");

        var result = await Simulate(db, Params([Id(1)]));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.SharedInputsRefused);
        result.SharedRefusal.Should().Be(FtmoSimulationRefusal.ProductNotTwoStep);
        result.DailyLossLimitPct.Should().Be(0.05m);
        result.MaxLossLimitPct.Should().Be(0.10m);
    }

    [Fact]
    public async Task MembersWithDifferentSourceZones_RefuseTheWholeRequestListingEachMemberWithItsZone()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, zone: "Asia/Jerusalem");
        await SeedSymbolAsync(db, Nasdaq, zone: "America/New_York");
        await SeedMemberAsync(db, Id(1), "gold", Gold);
        await SeedMemberAsync(db, Id(2), "nasdaq", Nasdaq);

        var result = await Simulate(db, Params([Id(1), Id(2)]));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.MixedSourceTimeZones);
        result.Members.Select(m => (m.StrategyId, m.SourceTimeZoneId)).Should().Equal(
            (Id(1), "Asia/Jerusalem"), (Id(2), "America/New_York"));
    }

    [Fact]
    public async Task ASourceZoneUnknownToThisHost_RefusesTheWholeRequestAsSharedInputsRefused()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedLimitsAsync(db);
        await SeedSymbolAsync(db, Gold, zone: "Nowhere/Land");
        await SeedMemberAsync(db, Id(1), "gold", Gold);

        var result = await Simulate(db, Params([Id(1)]));

        AssertGroupWideRefusal(result, FtmoGroupRefusal.SharedInputsRefused);
        result.SharedRefusal.Should().Be(FtmoSimulationRefusal.TimeZoneDataUnavailable);
    }

    // ---- B2.3.4: sizing, echo, status, member-level refusals ----

    [Fact]
    public async Task SizingIsFixedByTheFullRunBeforeTheTrim_SoAMemberWhoseCalibrationRowsFallOutsideTheWindowStillRuns()
    {
        // A's three SL calibration rows are its first three days; B starts on day 40, so they are trimmed. If
        // the trim ran BEFORE the risk estimate, A would refuse RiskNotEstimable.
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a", count: 90, startDay: 0);
        await SeedMemberAsync(db, Id(2), "b", count: 45, startDay: 40);

        var result = await Simulate(db, Params([Id(1), Id(2)]));

        foreach (var kind in result.Kinds)
        {
            kind.Status.Should().Be(FtmoSimulationStatus.Evaluated, "A keeps the sizing of its full run");
            kind.Coverage.Single(c => c.StrategyId == Id(1)).InWindowTrades.Should().BeLessThan(90);
        }
    }

    [Fact]
    public async Task Members_AreEchoedInOrderWithTheirCurrencyZoneAndAppliedFxBand_AndTheEchoedBandIsTheDeclaredOne()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedSymbolAsync(db, Dax, currency: "EUR");
        await SeedMemberAsync(db, Id(1), "gold", Gold);
        await SeedMemberAsync(db, Id(2), "dax", Dax, shift: 1);

        var result = await Simulate(db, Params([Id(2), Id(1)], fxLow: 0.9m, fxHigh: 1.1m));

        result.Members.Should().Equal(
            new FtmoGroupMemberDto(Id(1), "gold", 0, "USD", "Asia/Jerusalem", 1m, 1m),
            new FtmoGroupMemberDto(Id(2), "dax", 1, "EUR", "Asia/Jerusalem", 0.9m, 1.1m));
        result.DailyLossLimitPct.Should().Be(0.05m);
        result.MaxLossLimitPct.Should().Be(0.10m);
        result.Kinds.Should().OnlyContain(k => k.Run!.FxLow == 0.9m && k.Run.FxHigh == 1.1m);
    }

    [Fact]
    public async Task AnAllUsdGroup_IgnoresADeclaredBand_AndEchoesOneOne()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "gold");

        var result = await Simulate(db, Params([Id(1)], fxLow: 0.9m, fxHigh: 1.1m));

        result.Members.Single().Should().Be(new FtmoGroupMemberDto(Id(1), "gold", 0, "USD", "Asia/Jerusalem", 1m, 1m));
        result.Kinds.Should().OnlyContain(k => k.Run!.FxLow == 1m && k.Run.FxHigh == 1m);
    }

    [Fact]
    public async Task ANonUsdMemberWithoutABand_RefusesBothKindsListingThatMemberOnly()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedSymbolAsync(db, Dax, currency: "EUR");
        await SeedMemberAsync(db, Id(1), "gold", Gold);
        await SeedMemberAsync(db, Id(2), "dax", Dax);

        var result = await Simulate(db, Params([Id(1), Id(2)]));

        result.Status.Should().Be(FtmoSimulationStatus.Evaluated, "a member-level refusal is not a group-wide one");
        result.Refusal.Should().BeNull();
        result.Kinds.Should().HaveCount(2);
        foreach (var kind in result.Kinds)
        {
            kind.Refusal.Should().Be(FtmoGroupRefusal.MemberRunRefused);
            kind.Run.Should().BeNull();
            kind.MemberRefusals.Should().ContainSingle()
                .Which.Should().Be(new FtmoGroupMemberRefusalDto(Id(2), "dax", FtmoGroupRefusal.MemberRunRefused, FtmoSimulationRefusal.FxRateNotDeclared));
        }
    }

    [Fact]
    public async Task AnInvertedBandWithANonUsdMember_RefusesBothKindsAsInvalidFxBand()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedSymbolAsync(db, Dax, currency: "EUR");
        await SeedMemberAsync(db, Id(1), "dax", Dax);

        var result = await Simulate(db, Params([Id(1)], fxLow: 1.2m, fxHigh: 0.8m));

        result.Kinds.Should().OnlyContain(k =>
            k.Refusal == FtmoGroupRefusal.MemberRunRefused
            && k.MemberRefusals.Single().RunReason == FtmoSimulationRefusal.InvalidFxBand);
    }

    [Fact]
    public async Task ASymbolLevelFailureOnOneMember_RefusesBothKindsAndTheOthersAreNotSimulatedAlone()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedSymbolAsync(db, NoSpec, spec: false, calibrated: false);
        await SeedMemberAsync(db, Id(1), "gold", Gold);
        await SeedMemberAsync(db, Id(2), "nospec", NoSpec);

        var result = await Simulate(db, Params([Id(1), Id(2)]));

        result.Kinds.Should().HaveCount(2);
        foreach (var kind in result.Kinds)
        {
            kind.Run.Should().BeNull();
            kind.MemberRefusals.Should().ContainSingle()
                .Which.Should().Be(new FtmoGroupMemberRefusalDto(Id(2), "nospec", FtmoGroupRefusal.MemberRunRefused, FtmoSimulationRefusal.InstrumentSpecMissing));
        }

        result.Members.Single(m => m.StrategyId == Id(2)).FxLow.Should().BeNull("no band was applied to a refused member");
    }

    [Fact]
    public async Task AMemberLackingOneKind_RefusesOnlyThatKind_AndTheEnvelopeIsNotRefused()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");
        await SeedMemberAsync(db, Id(2), "b", shift: 1, evaluation: false);

        var result = await Simulate(db, Params([Id(1), Id(2)]));

        result.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        result.Refusal.Should().BeNull();
        var deploy = result.Kinds.Single(k => k.Kind == BacktestRunKind.Deploy);
        var evaluation = result.Kinds.Single(k => k.Kind == BacktestRunKind.Evaluation);
        deploy.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        deploy.Run!.RunId.Should().Be(Guid.Empty);
        evaluation.Refusal.Should().Be(FtmoGroupRefusal.MemberMissingKind);
        evaluation.MemberRefusals.Single().StrategyId.Should().Be(Id(2));
        result.Disclosures.Should().Equal(FtmoGroupSimulationLimits.Disclosures);
    }

    // ---- B2.3.5: query count, cancellation, wording ----

    [Fact]
    public async Task ACancelledToken_ThrowsOperationCanceled()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedWorldAsync(db);
        await SeedMemberAsync(db, Id(1), "a");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await Simulate(db, Params([Id(1)]), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void NoDtoPropertyOrDisclosureCarriesBannedSurvivalWording()
    {
        var banned = new[] { "passed", "safe", "survived", "would have passed" };
        var types = new[]
        {
            typeof(FtmoGroupSimulationRequest), typeof(FtmoGroupSimulationParameters), typeof(FtmoGroupSimulationDto),
            typeof(FtmoGroupMemberDto), typeof(FtmoGroupNameWarningDto), typeof(FtmoGroupMemberRefusalDto),
            typeof(FtmoGroupWindowDto), typeof(FtmoGroupMemberCoverageDto), typeof(FtmoGroupKindResultDto),
        };

        foreach (var type in types)
        {
            foreach (var name in new[] { type.Name }.Concat(type.GetProperties().Select(p => p.Name)))
            {
                foreach (var word in banned)
                    name.ToLowerInvariant().Should().NotContain(word);
            }
        }

        foreach (var text in FtmoGroupSimulationLimits.Disclosures)
        {
            foreach (var word in banned)
                text.ToLowerInvariant().Should().NotContain(word);
        }
    }

    /// <summary>
    /// A relational provider is needed to COUNT commands: EF InMemory would let an N+1 pass in silence. The
    /// full <c>AppDbContext</c> cannot be created on SQLite as shipped (four configurations declare
    /// <c>nvarchar(max)</c>), so this subclass clears that column type for the test only.
    /// </summary>
    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (string.Equals(property.GetColumnType(), "nvarchar(max)", StringComparison.OrdinalIgnoreCase))
                    property.SetColumnType(null);
            }
        }
    }

    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => _count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task<int> CountCommandsAsync(int memberCount)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        var interceptor = new CountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        await using var db = new SqliteAppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        await SeedWorldAsync(db);
        var ids = new List<Guid>();
        for (var i = 1; i <= memberCount; i++)
        {
            await SeedMemberAsync(db, Id(i), $"m{i}", shift: i, count: 20);
            ids.Add(Id(i));
        }

        interceptor.Reset();
        var result = await Simulate(db, Params(ids));

        result.Kinds.Should().OnlyContain(k => k.Status == FtmoSimulationStatus.Evaluated, "the count must be of a full run");
        return interceptor.Count;
    }

    [Fact]
    public async Task TheNumberOfDatabaseCommands_IsTheSameForTwoMembersAndForSix_AndNeverAboveSix()
    {
        var two = await CountCommandsAsync(2);
        var six = await CountCommandsAsync(6);

        six.Should().Be(two, "the queries are batched with Contains over id and symbol lists, none per member");
        two.Should().BeLessThanOrEqualTo(6);
    }
}
