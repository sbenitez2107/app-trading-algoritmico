using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// PR P4, Phases 11-13 — <see cref="FtmoBreachSimulationReadService"/>: every refusal reason
/// (design.md Decision 6), the FX-band merge (Decision 3), resize counts, IS/OOS separation and the
/// disclosures every result carries (spec.md). Uses the in-memory EF provider — no migration is
/// applied to any real database.
/// </summary>
public class FtmoBreachSimulationReadServiceTests
{
    private const string Broker = "FTMO";
    private const string XauSymbol = "XAUUSD_TEST";
    private const string EurSymbol = "DEUIDXEUR_TEST";
    private static readonly DateTime SafeDay = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "FTMO test strategy", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    private static async Task<Guid> SeedRunAsync(
        AppDbContext db, Guid strategyId, BacktestRunKind kind, IEnumerable<BacktestTrade> trades)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = $"{kind}.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = kind,
            Symbol = XauSymbol,
            CreatedAt = DateTime.UtcNow,
        };
        db.BacktestRuns.Add(run);
        foreach (var trade in trades)
        {
            trade.BacktestRunId = run.Id;
            db.BacktestTrades.Add(trade);
        }
        await db.SaveChangesAsync();
        return run.Id;
    }

    private static BacktestTrade MakeTrade(
        int rowIndex, decimal size, decimal profit, string closeType = "TP",
        decimal? realizedRisk = null, DateTime? open = null, DateTime? close = null,
        BacktestSegment segment = BacktestSegment.InSample) => new()
        {
            RowIndex = rowIndex,
            Ticket = rowIndex + 1,
            Symbol = XauSymbol,
            Type = "Long",
            OpenTime = open ?? SafeDay.AddHours(-1).AddDays(rowIndex),
            OpenPrice = 100m,
            Size = size,
            CloseTime = close ?? SafeDay.AddDays(rowIndex),
            ClosePrice = 101m,
            Profit = profit,
            Balance = 10_000m,
            SampleTypeRaw = "InSample",
            Segment = segment,
            CloseType = closeType,
            RealizedRisk = realizedRisk,
        };

    /// <summary>Three consistent SL closes so <c>TradeRiskNormalizer.TryNormalize</c> succeeds with Â=100.</summary>
    private static List<BacktestTrade> SlCalibrationTrades() =>
    [
        MakeTrade(0, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
        MakeTrade(1, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
        MakeTrade(2, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
    ];

    private static async Task SeedBrokerRiskLimitsAsync(
        AppDbContext db, FtmoProduct? product = FtmoProduct.TwoStep, Domain.Enums.DrawdownModel? drawdown = Domain.Enums.DrawdownModel.Static,
        decimal? dailyPct = 0.05m, decimal? maxPct = 0.10m)
    {
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = product,
            DrawdownModel = drawdown,
            DailyLossLimitPct = dailyPct,
            MaxLossLimitPct = maxPct,
            Verified = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedInstrumentSpecAsync(
        AppDbContext db, string sqxSymbol = XauSymbol, string currency = "USD", decimal contractSize = 100m,
        string sourceZone = "Asia/Jerusalem", int sizeDecimals = 2, decimal step = 0.01m,
        decimal minLot = 0.01m, decimal maxLots = 1000m)
    {
        db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
        {
            SqxSymbol = sqxSymbol,
            FtmoSymbol = sqxSymbol,
            ContractSize = contractSize,
            ProfitCurrency = currency,
            SizeDecimals = sizeDecimals,
            Step = step,
            MinLot = minLot,
            MaxLots = maxLots,
            SourceTimeZoneId = sourceZone,
            Provenance = "test",
            CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCalibrationAsync(
        AppDbContext db, string symbol = XauSymbol, CalibrationStatus status = CalibrationStatus.Calibrated, decimal? pointValue = 100m)
    {
        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Symbol = symbol,
            Status = status,
            PointValue = pointValue,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The SOURCE grid is always passed explicitly: the request record carries no default grid
    /// (RELIABILITY-001). These values are TEST DATA declaring the committed XAUUSD exports' grid.
    /// </summary>
    private static FtmoBreachSimulationRequest Request(
        Guid strategyId, string sqxSymbol = XauSymbol, decimal target = 100m,
        decimal initialCapital = 10_000m, decimal? fxLow = null, decimal? fxHigh = null,
        int sizeDecimals = 2, decimal step = 0.01m, decimal minLot = 0.01m, decimal maxLots = 10m) =>
        new(strategyId, Broker, sqxSymbol, initialCapital, target, fxLow, fxHigh,
            SizeDecimals: sizeDecimals, Step: step, MinLot: minLot, MaxLots: maxLots);

    // =====================================================================
    // Phase 11 — one test per refusal reason.
    // =====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData(FtmoProduct.OneStep)]
    public async Task Simulate_NullOrOneStepProduct_RefusesProductNotTwoStep(FtmoProduct? product)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db, product: product);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs.Should().ContainSingle();
        result.Runs[0].Status.Should().Be(FtmoSimulationStatus.Refused);
        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.ProductNotTwoStep);
        result.Runs[0].Daily.Should().BeNull();
        result.Runs[0].Max.Should().BeNull();

        // ftmo-challenge-race, tasks.md 2.6.6/2.11.2 (hard rule 7): the EXISTING whole-run refusal
        // already refuses the race too — no new refusal logic needed, only this coverage, exercised
        // THROUGH THE SERVICE rather than a hand-built FtmoChallengeRace.RunPhase call.
        result.Runs[0].ChallengeRace.Should().BeNull();
    }

    /// <summary>
    /// ftmo-challenge-race, tasks.md 2.6.7: both phases read the same stored
    /// DailyLossLimitPct/MaxLossLimitPct — exercised end-to-end (the daily/max floors close over the
    /// same `dailyPct`/`maxPct` values passed into both `RunPhase` calls from `RunChain`).
    /// </summary>
    [Fact]
    public async Task Simulate_ChallengeRace_LossLimitsAreReadUnchangedAcrossBothPhases()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        // Phase 1 reaches +10% on day 4 (day min met, flat book); a further trade after the handover
        // exercises phase 2 against the SAME stored 5%/10% limits.
        trades.Add(MakeTrade(3, size: 1.00m, profit: 1_100m, closeType: "TP", open: SafeDay.AddDays(4).AddHours(-1), close: SafeDay.AddDays(4)));
        trades.Add(MakeTrade(4, size: 1.00m, profit: 10m, closeType: "TP", open: SafeDay.AddDays(5).AddHours(-1), close: SafeDay.AddDays(5)));
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var race = result.Runs.Single().ChallengeRace;

        race.Should().NotBeNull();
        race!.Refusal.Should().BeNull();
        race.Rules.Phase1TargetPct.Should().Be(0.10m);
        race.Rules.Phase2TargetPct.Should().Be(0.05m);
    }

    /// <summary>ftmo-challenge-race, tasks.md 2.6.1/2.6.4: a stored 0.10 (or null) target does not refuse.</summary>
    [Theory]
    [InlineData(0.10)]
    [InlineData(null)]
    public async Task Simulate_ChallengeRace_StoredTargetOf010OrNull_DoesNotRefuse(double? profitTargetPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = Domain.Enums.DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = (decimal?)profitTargetPct,
            Verified = true,
        });
        await db.SaveChangesAsync();
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var race = result.Runs.Single().ChallengeRace;

        race.Should().NotBeNull();
        race!.Refusal.Should().BeNull();
    }

    /// <summary>ftmo-challenge-race, tasks.md 2.6.2/2.6.3: a stored target != 0.10 refuses only the race.</summary>
    [Fact]
    public async Task Simulate_ChallengeRace_StoredTargetOtherThan010_RefusesRaceOnly_RunStaysEvaluated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = Domain.Enums.DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = 0.08m,
            Verified = true,
        });
        await db.SaveChangesAsync();
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var withoutRace = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var run = withoutRace.Runs.Single();

        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.Daily.Should().NotBeNull();
        run.ChallengeRace.Should().NotBeNull();
        run.ChallengeRace!.Refusal.Should().Be(FtmoChallengeRaceRefusal.ProfitTargetMismatch);
        run.ChallengeRace.StoredProfitTargetPct.Should().Be(0.08m);
        run.ChallengeRace.Phase1.Should().BeNull();
        run.ChallengeRace.Phase2.Should().BeNull();
    }

    [Fact]
    public async Task Simulate_NoBrokerRiskLimitsRow_RefusesLimitsNotConfigured()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.LimitsNotConfigured);
    }

    /// <summary>
    /// A <c>LossLimits</c> row can be saved with either percentage null (<c>RiskLimitsService</c>
    /// requires only <c>DrawdownModel</c>). A null limit means the rule is NOT configured — it is not
    /// a 0% rule. Substituting 0% would put the max floor at initial capital and read every loss as a
    /// FALSE <c>Breached</c>. The trades here are fully estimable, so a missing guard surfaces as an
    /// <c>Evaluated</c> result carrying findings.
    /// </summary>
    [Theory]
    [InlineData(null, 0.10)]
    [InlineData(0.05, null)]
    [InlineData(null, null)]
    public async Task Simulate_LossLimitsRowWithNullPercentage_RefusesLimitsNotConfigured(double? dailyPct, double? maxPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db, dailyPct: (decimal?)dailyPct, maxPct: (decimal?)maxPct);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.LimitsNotConfigured);
    }

    /// <summary>
    /// A stored percentage outside (0, 1] is not a usable rule either — the same fraction contract
    /// <c>RiskLimitsService</c> enforces for funding stages and VaR targets. It is a property of the
    /// stored configuration, not of the request, so it refuses as <c>LimitsNotConfigured</c>, not
    /// <c>InvalidRequest</c>.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.10)]
    [InlineData(-0.05, 0.10)]
    [InlineData(1.5, 0.10)]
    [InlineData(0.05, 0.0)]
    [InlineData(0.05, -0.10)]
    [InlineData(0.05, 1.5)]
    public async Task Simulate_LossLimitsRowWithOutOfRangePercentage_RefusesLimitsNotConfigured(double dailyPct, double maxPct)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db, dailyPct: (decimal)dailyPct, maxPct: (decimal)maxPct);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.LimitsNotConfigured);
    }

    /// <summary>Boundary: exactly 1 (100%) is inside (0, 1] and must still be evaluated.</summary>
    [Fact]
    public async Task Simulate_LossLimitsRowWithFullFractionPercentages_IsEvaluated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db, dailyPct: 1m, maxPct: 1m);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs.Single().Status.Should().Be(FtmoSimulationStatus.Evaluated);
    }

    [Fact]
    public async Task Simulate_DrawdownModelNotStatic_RefusesDrawdownModelNotStatic()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db, drawdown: Domain.Enums.DrawdownModel.Trailing);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.DrawdownModelNotStatic);
    }

    [Fact]
    public async Task Simulate_NoInstrumentSpecForSymbol_RefusesInstrumentSpecMissing()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedCalibrationAsync(db);
        // No FtmoInstrumentSpec row seeded at all.

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    /// <summary>
    /// The live refusal case (design.md Decision 6 / Corrections #6): NQ
    /// (<c>USATECHIDXUSD_M1_UTC02</c>) is measured <c>Inconsistent</c> with a null point value.
    /// <c>CalibrationStatus.Calibrated == 0</c> is the CLR default, so this test constructs the
    /// combination <c>Status == Calibrated</c> WITH a null <c>PointValue</c> to prove the null check
    /// fires independently of <c>Status</c> — not only when <c>Status != Calibrated</c>.
    /// </summary>
    [Fact]
    public async Task Simulate_NqUsatechCalibrationInconsistentNullPointValue_RefusesPointValueNotCalibrated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        // Status happens to read as the enum's zero default (Calibrated) while PointValue is null —
        // the exact shape a naive "Status != Calibrated" check would miss.
        await SeedCalibrationAsync(db, status: CalibrationStatus.Calibrated, pointValue: null);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public async Task Simulate_InconsistentCalibrationStatus_RefusesPointValueNotCalibrated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db, status: CalibrationStatus.Inconsistent, pointValue: null);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public async Task Simulate_NonUsdSymbolNoFxBandSupplied_RefusesFxRateNotDeclared()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, currency: "EUR");
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.FxRateNotDeclared);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)] // inverted: low > high
    public async Task Simulate_DegenerateOrInvertedFxBand_RefusesInvalidFxBand(decimal low, decimal high)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, currency: "EUR");
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId, fxLow: low, fxHigh: high), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.InvalidFxBand);
    }

    [Fact]
    public async Task Simulate_NormalizerRefusesRun_RefusesRiskNotEstimable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        // No SL closes at all -> TradeRiskNormalizer.Estimate is InsufficientSamples -> TryNormalize false.
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, [MakeTrade(0, size: 1m, profit: 10m)]);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.RiskNotEstimable);
    }

    [Fact]
    public async Task Simulate_RunTradesCarryMultipleSegments_RefusesRunSegmentsDisagree()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        trades[2].Segment = BacktestSegment.OutOfSample;
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.RunSegmentsDisagree);
    }

    [Fact]
    public async Task Simulate_UnresolvableTimeZoneId_RefusesTimeZoneDataUnavailable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, sourceZone: "Not/A_Real_Zone");
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.TimeZoneDataUnavailable);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(10_000, 0)]
    public async Task Simulate_InvalidCapitalOrTargetOrSourceGrid_RefusesInvalidRequest(decimal capital, decimal target)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(
            Request(strategyId, target: target, initialCapital: capital), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.InvalidRequest);
    }

    /// <summary>
    /// RELIABILITY-001: an explicitly invalid SOURCE grid reaches
    /// <see cref="FtmoBreachSimulationRequest.TryBuildSourceGrid"/> and is refused as
    /// <c>InvalidRequest</c> — never repaired into a grid the caller did not declare.
    /// </summary>
    [Theory]
    [InlineData(2, 0, 0.01, 10)] // step = 0
    [InlineData(2, 0.01, 0, 10)] // minLot = 0
    [InlineData(2, 1, 1, 10)] // LotGrid rejects: step 1 needs 0 decimals, grid declares 2
    [InlineData(2, 0.01, 0.01, 0.005)] // LotGrid rejects: maxLots below minLot
    public async Task Simulate_InvalidDeclaredSourceGrid_RefusesInvalidRequestWithNoFindings(
        int sizeDecimals, double step, double minLot, double maxLots)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(
            Request(strategyId, sizeDecimals: sizeDecimals, step: (decimal)step, minLot: (decimal)minLot, maxLots: (decimal)maxLots),
            CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.InvalidRequest);
    }

    /// <summary>
    /// RELIABILITY-001: the request record must not be able to supply a SOURCE grid the caller did
    /// not declare. A constructor default for any of the four grid parameters is a fabricated
    /// domain value (<c>.agents/knowledge/imox/INDEX.md</c> §5).
    /// </summary>
    [Fact]
    public void Request_SourceGridParameters_HaveNoDefaultValue()
    {
        string[] gridParameters =
        [
            nameof(FtmoBreachSimulationRequest.SizeDecimals),
            nameof(FtmoBreachSimulationRequest.Step),
            nameof(FtmoBreachSimulationRequest.MinLot),
            nameof(FtmoBreachSimulationRequest.MaxLots),
        ];

        var parameters = typeof(FtmoBreachSimulationRequest).GetConstructors()
            .Single(c => c.GetParameters().Length > 1)
            .GetParameters()
            .Where(p => gridParameters.Contains(p.Name))
            .ToList();

        parameters.Should().HaveCount(4);
        parameters.Where(p => p.HasDefaultValue).Select(p => p.Name).Should().BeEmpty(
            "the source lot grid is declared by the caller and never defaulted");
    }

    // ---- RELIABILITY-003: the untested disjuncts inside the refusal guards ----

    private static void AssertRefusedWithoutFindings(FtmoRunSimulationResultDto run, FtmoSimulationRefusal reason)
    {
        run.Status.Should().Be(FtmoSimulationStatus.Refused);
        run.Refusal.Should().Be(reason);
        run.Daily.Should().BeNull();
        run.Max.Should().BeNull();
        run.RaisedToMinimumCount.Should().Be(0);
        run.CappedAtMaximumCount.Should().Be(0);
        run.UnscalableCount.Should().Be(0);
    }

    /// <summary>
    /// ISOLATION: the run's trades are deliberately NOT risk-estimable. <c>RefuseRunInputs</c> re-checks
    /// the contract size per run with the SAME reason, so with estimable trades this test could not tell whether the
    /// shared guard fired. With non-estimable trades, a missing shared guard surfaces as
    /// <c>RiskNotEstimable</c> instead.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Simulate_SpecRowWithNonPositiveContractSize_RefusesInstrumentSpecMissing(int contractSize)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, [MakeTrade(0, size: 1m, profit: 10m)]);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, contractSize: contractSize); // the row EXISTS
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    /// <summary>
    /// A persisted spec row whose FTMO grid <c>LotGrid</c>'s constructor rejects (here step 0.01
    /// declared with 3 decimals) is refused as <c>InstrumentSpecMissing</c>, not thrown.
    /// </summary>
    [Fact]
    public async Task Simulate_SpecRowWithGridLotGridRejects_RefusesInstrumentSpecMissing()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, sizeDecimals: 3, step: 0.01m, minLot: 0.01m, maxLots: 1000m);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    [Fact]
    public async Task Simulate_NoCalibrationRowForSymbol_RefusesPointValueNotCalibrated()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        // No SymbolCalibration row seeded at all.

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    /// <summary>
    /// ISOLATION: the run's trades are deliberately NOT risk-estimable. <c>RefuseRunInputs</c> re-checks
    /// the source point value per run with the SAME reason, so with estimable trades this test could not tell whether the
    /// shared guard fired. With non-estimable trades, a missing shared guard surfaces as
    /// <c>RiskNotEstimable</c> instead.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Simulate_CalibratedWithNonPositivePointValue_RefusesPointValueNotCalibrated(int pointValue)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, [MakeTrade(0, size: 1m, profit: 10m)]);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db, status: CalibrationStatus.Calibrated, pointValue: pointValue);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        AssertRefusedWithoutFindings(result.Runs.Single(), FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public async Task Simulate_AnyRefusedRun_CarriesNoFindings()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        // No BrokerRiskLimits at all -> LimitsNotConfigured.

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Refused);
        run.Daily.Should().BeNull();
        run.Max.Should().BeNull();
        run.RaisedToMinimumCount.Should().Be(0);
        run.CappedAtMaximumCount.Should().Be(0);
        run.UnscalableCount.Should().Be(0);
    }

    /// <summary>
    /// "Without a declared symbol mapping" (spec.md) is satisfied by the SAME guard as
    /// <see cref="Simulate_NoInstrumentSpecForSymbol_RefusesInstrumentSpecMissing"/>: the service
    /// looks up <c>FtmoInstrumentSpec</c> by EXACT <c>SqxSymbol</c> match — never by string
    /// similarity. A near-miss symbol name is refused, not fuzzy-matched to the seeded row.
    /// </summary>
    [Fact]
    public async Task Simulate_NearMissSymbolName_RefusedWithoutStringSimilarityMatch()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db, sqxSymbol: XauSymbol); // seeded under the real symbol
        await SeedCalibrationAsync(db, symbol: XauSymbol);

        var sut = new FtmoBreachSimulationReadService(db);
        // Request names a symbol one character off from the seeded row — no fuzzy match must occur.
        var result = await sut.SimulateAsync(Request(strategyId, sqxSymbol: XauSymbol + "X"), CancellationToken.None);

        result.Runs[0].Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    // =====================================================================
    // Phase 12 — FX band evaluation and merge.
    // =====================================================================

    [Fact]
    public async Task Simulate_SameCurrencySymbol_NoFxBandRequired_Proceeds()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db); // USD
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs[0].Status.Should().Be(FtmoSimulationStatus.Evaluated);
        result.Runs[0].Refusal.Should().BeNull();
    }

    private static async Task<Guid> SeedEurFxScenarioAsync(AppDbContext db, decimal bigTradeProfit)
    {
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        trades.Add(MakeTrade(3, size: 1.00m, profit: bigTradeProfit, closeType: "TP"));
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        // A COARSE grid (step/min 1) is deliberate: it makes the floor-rounding difference between
        // fxLow and fxHigh large enough to flip a verdict, even though FX otherwise nearly cancels
        // in the projector's own arithmetic (design.md Decision 3).
        await SeedInstrumentSpecAsync(
            db, sqxSymbol: EurSymbol, currency: "EUR", contractSize: 1m,
            sizeDecimals: 0, step: 1m, minLot: 1m, maxLots: 1000m);
        await SeedCalibrationAsync(db, symbol: EurSymbol, pointValue: 100m);
        return strategyId;
    }

    [Fact]
    public async Task Simulate_AgreeingFxBand_ProducesCleanVerdictWithoutFxRoundingSensitive()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedEurFxScenarioAsync(db, bigTradeProfit: -10m); // tiny loss, never breaches either end

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(
            Request(strategyId, sqxSymbol: EurSymbol, fxLow: 1.0m, fxHigh: 1.5m), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.Daily!.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
        run.Daily.Causes.Should().NotContain(BreachContingencyCause.FxRoundingSensitive);
        run.FxLow.Should().Be(1.0m);
        run.FxHigh.Should().Be(1.5m);
    }

    /// <summary>
    /// Because FX nearly cancels in the projector's own arithmetic (design.md Decision 3), a verdict
    /// flip across the band comes from the LOT-GRID FLOOR, not from the P/L scaling directly. With
    /// contract size 1 and step 1, <c>fxLow=1.0</c> lands the lot count on the exact quotient
    /// (<c>u=100</c>, no rounding loss, achieved P/L = declared Profit), while <c>fxHigh=1.5</c>
    /// floors <c>u=66.667</c> down to 66, losing ~1% of achieved size/loss — just enough, with the
    /// Profit chosen here, to cross the 5% daily floor at one end and not the other.
    /// </summary>
    [Fact]
    public async Task Simulate_DisagreeingFxBand_YieldsBreachContingentFxRoundingSensitive_AndEchoesTheBand()
    {
        await using var db = InMemoryDbContextFactory.Create();
        // fx=1.0 -> net' = -501 exactly -> balance 9499 < floor 9500 -> Breached.
        // fx=1.5 -> net' = -495.99      -> balance 9504.01 >= floor 9500 -> NoBreachObserved.
        var strategyId = await SeedEurFxScenarioAsync(db, bigTradeProfit: -501m);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(
            Request(strategyId, sqxSymbol: EurSymbol, fxLow: 1.0m, fxHigh: 1.5m), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.Daily!.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        run.Daily.Causes.Should().Contain(BreachContingencyCause.FxRoundingSensitive);
        run.FxLow.Should().Be(1.0m);
        run.FxHigh.Should().Be(1.5m);
    }

    // =====================================================================
    // Phase 13 — resize counts, IS/OOS separation, disclosures, per-strategy scope.
    // =====================================================================

    [Fact]
    public async Task Simulate_ResizeCounts_AppearAlongsideNoBreachObserved()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        // Under target=100, Â=100, P_src=100, M=100: u = size*100*100/(100*100) = size. A size of
        // 0.001 floors (step 0.01) below MinLot (0.01) -> RaisedToMinimum. Small profit so no breach.
        trades.Add(MakeTrade(3, size: 0.001m, profit: 1m, closeType: "TP"));
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Daily!.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
        run.RaisedToMinimumCount.Should().BeGreaterThan(0, "the 0.001-lot row floors below MinLot");
    }

    [Fact]
    public async Task Simulate_IsAndOosRuns_ProduceTwoSeparateResults_NeverMerged()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedRunAsync(db, strategyId, BacktestRunKind.Evaluation, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        result.Runs.Should().HaveCount(2);
        result.Runs.Select(r => r.Kind).Should().BeEquivalentTo([BacktestRunKind.Deploy, BacktestRunKind.Evaluation]);
        result.Runs.Select(r => r.RunId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Simulate_EverySwapDisclosureAndEmbeddedCommissionDisclosure_AppearOnEveryResult()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var evaluated = (await sut.SimulateAsync(Request(strategyId), CancellationToken.None)).Runs.Single();

        // A refused result too, from a sibling scenario with no BrokerRiskLimits.
        await using var refusedDb = InMemoryDbContextFactory.Create();
        var refusedStrategyId = await SeedStrategyAsync(refusedDb);
        await SeedRunAsync(refusedDb, refusedStrategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        var refused = (await new FtmoBreachSimulationReadService(refusedDb)
            .SimulateAsync(Request(refusedStrategyId), CancellationToken.None)).Runs.Single();

        foreach (var run in new[] { evaluated, refused })
        {
            run.NotModelled.Should().Contain("Swap");
            run.EmbeddedCommissionDisclosure.Should().Contain("embed", "commission is embedded, not absent")
                .And.Contain("not absent", "the disclosure must explicitly deny the absent framing, not just omit it");
        }
    }

    // =====================================================================
    // ftmo-first-breach-timing Phase 7 — first-breach timing wiring.
    // =====================================================================

    [Fact]
    public async Task Simulate_UsdSymbol_SameCurrencyBoth_FirstBreachReportsBothEnds()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        trades.Add(MakeTrade(3, size: 1.00m, profit: -800m, closeType: "SL"));
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db); // USD
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Daily!.FirstBreach.Should().NotBeNull();
        // USD settles on the identity band (1,1): both ends coincide — pinned as BothEnds (task 5.6 /
        // the orchestrator's resolved reading), never a "skip the comparison" special case.
        run.Daily.FirstBreach!.FxBandEnd.Should().Be(FtmoFxBandEnd.BothEnds);
    }

    [Fact]
    public async Task Simulate_RefusedRun_EveryNewFieldIsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, SlCalibrationTrades());
        // No BrokerRiskLimits row -> LimitsNotConfigured (refused).

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Refused);
        run.Daily.Should().BeNull();
        run.Max.Should().BeNull();
        run.FirstLimitBreach.Should().BeNull();
        run.ReplayStartSourceTime.Should().BeNull();
        run.ReplayStartFtmoDay.Should().BeNull();
    }

    [Fact]
    public async Task Simulate_NonRefusedRun_EchoesTheReplayStartAnchor()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades();
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.ReplayStartSourceTime.Should().NotBeNull();
        run.ReplayStartFtmoDay.Should().NotBeNull();
        var earliestOpen = trades.Min(t => t.OpenTime);
        run.ReplayStartSourceTime.Should().Be(earliestOpen);
    }

    [Fact]
    public async Task Simulate_DisagreeingFxBandOnDailyLimit_OneEndNeverBreaches_FirstBreachReportsTheBreachingEnd()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedEurFxScenarioAsync(db, bigTradeProfit: -501m);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(
            Request(strategyId, sqxSymbol: EurSymbol, fxLow: 1.0m, fxHigh: 1.5m), CancellationToken.None);

        var run = result.Runs.Single();
        // This is the existing FxRoundingSensitive fixture, on the DAILY limit: fxLow=1.0 breaches
        // (balance < floor) while fxHigh=1.5 never breaches at all (Simulate_DisagreeingFxBand...
        // above). With no high-end BreachPoint to merge against, FtmoBreachTiming.Earliest reports
        // the low end's point verbatim (design.md Decision 6) — pinned here as FxLow, not "any of
        // the enum's values". Row 3 (the big loss trade) is the only trade whose loss can cross the
        // daily floor, so its close time is the expected SourceCloseTime.
        run.Daily!.FirstBreach.Should().NotBeNull();
        run.Daily.FirstBreach!.FxBandEnd.Should().Be(FtmoFxBandEnd.FxLow);
        run.Daily.FirstBreach.SourceCloseTime.Should().Be(SafeDay.AddDays(3));
    }

    [Fact]
    public async Task Simulate_TwoStrategiesEachNoBreachObserved_NoCombinedPortfolioFindingProduced()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyIdA = await SeedStrategyAsync(db);
        var strategyIdB = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyIdA, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedRunAsync(db, strategyIdB, BacktestRunKind.Deploy, SlCalibrationTrades());
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var resultA = await sut.SimulateAsync(Request(strategyIdA), CancellationToken.None);
        var resultB = await sut.SimulateAsync(Request(strategyIdB), CancellationToken.None);

        // Each call is scoped to exactly one strategy's own runs; there is no API surface on this
        // service that accepts more than one StrategyId or returns a combined finding.
        resultA.StrategyId.Should().Be(strategyIdA);
        resultB.StrategyId.Should().Be(strategyIdB);
        resultA.Runs.Single().Daily!.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
        resultB.Runs.Single().Daily!.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
    }

    // =====================================================================
    // ftmo-challenge-race PR1, Phase 1.0 — the snapshot pin (tasks.md task 1.0.1). Captured on
    // UNMODIFIED code before any PR1 production change (design.md Testing Strategy row 1) and MUST
    // stay green, unedited, EXCLUDING only the FtmoTradingDaysElapsed paths (which are the ONE thing
    // PR1 is allowed to change). A day-gap fixture is used deliberately (trade 3 opens day 5, closes
    // day 10; trade 4 opens day 10, closes day 10) so open-days and close-days genuinely differ:
    // old (by-close) counts {0,1,2,10} = 4; new (by-open) counts {0,1,2,5,10} = 5.
    // =====================================================================

    private static async Task<Guid> SeedTradingDaySnapshotScenarioAsync(AppDbContext db)
    {
        var strategyId = await SeedStrategyAsync(db);
        var trades = SlCalibrationTrades(); // rowIndex 0,1,2 — opens/closes days 0,1,2
        trades.Add(MakeTrade(
            3, size: 1.00m, profit: 5m, closeType: "TP",
            open: SafeDay.AddDays(5), close: SafeDay.AddDays(10)));
        trades.Add(MakeTrade(
            4, size: 1.00m, profit: -900m, closeType: "SL",
            open: SafeDay.AddDays(10).AddHours(-1), close: SafeDay.AddDays(10)));
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);
        return strategyId;
    }

    [Fact]
    public async Task Simulate_TradingDayRecalculationFixture_SnapshotPin_OnlyFtmoTradingDaysElapsedChanges()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedTradingDaySnapshotScenarioAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var run = result.Runs.Single();

        // Captured by actually running this exact fixture through the UNMODIFIED (pre-PR1)
        // FtmoBreachSimulationReadService/FtmoReplayCalendar (a temporary probe harness, discarded
        // after capture) — not hand-derived. Every field below is byte-identical to that capture,
        // EXCEPT the FtmoTradingDaysElapsed paths (excluded below), which is PR1's only change.
        var breachClose = SafeDay.AddDays(10);
        var breachDay = DateOnly.FromDateTime(breachClose);
        var timing = new FtmoBreachTimingDto(
            breachClose, breachDay, BalanceAfterClose: 9102m, FloorLevel: 9497.00m,
            FtmoBreachPointClass.Clean, Causes: [], FtmoTradingDaysElapsed: -1, CalendarDaysElapsed: 10,
            FtmoFxBandEnd.BothEnds);
        var expected = new FtmoRunSimulationResultDto(
            run.RunId, BacktestRunKind.Deploy, BacktestSegment.InSample, FtmoSimulationStatus.Evaluated, Refusal: null,
            new FtmoLimitFindingDto(
                FtmoBreachVerdict.Breached, Causes: [],
                DisclosureText: "A closed-trade breach with no identified contingency was detected in the replayed series.")
            {
                FirstBreach = timing,
                FirstCleanBreach = timing,
            },
            new FtmoLimitFindingDto(
                FtmoBreachVerdict.NoBreachObserved, Causes: [],
                DisclosureText: "No breach was found in the replayed closed-trade data. A closed-trade replay and "
                    + "unmodelled swap both push the result toward looking less risky than reality allows; this "
                    + "finding makes no claim about what a live equity path would have done.")
            {
                FirstBreach = null,
                FirstCleanBreach = null,
            },
            RaisedToMinimumCount: 0, CappedAtMaximumCount: 0, UnscalableCount: 0,
            FxLow: 1m, FxHigh: 1m,
            FtmoRunSimulationResultDto.DefaultNotModelled,
            FtmoRunSimulationResultDto.DefaultEmbeddedCommissionDisclosure)
        {
            FirstLimitBreach = new FtmoFirstLimitBreachDto(
                FtmoFirstBreachingLimit.Daily, breachClose, breachDay, FtmoTradingDaysElapsed: -1, CalendarDaysElapsed: 10),
            ReplayStartSourceTime = SafeDay.AddDays(-0).AddHours(-1), // trade 0's open: SafeDay - 1h, day 0
            ReplayStartFtmoDay = DateOnly.FromDateTime(SafeDay),
            // ftmo-challenge-race (orchestrator-approved exception, 2026-09-27): this snapshot was
            // captured before ChallengeRace existed, so it never protected that field — excluding it
            // here does not weaken anything the pin originally guarded; every pre-existing field above
            // is still compared exactly, byte-identical to the pre-PR1 capture.
            ChallengeRace = null,
        };

        result.Runs.Single().Should().BeEquivalentTo(expected, options => options
            .Excluding(member => member.Name == nameof(FtmoBreachTimingDto.FtmoTradingDaysElapsed))
            .Excluding(member => member.Name == nameof(FtmoFirstLimitBreachDto.FtmoTradingDaysElapsed))
            .Excluding(member => member.Name == nameof(FtmoRunSimulationResultDto.ChallengeRace)));
    }

    /// <summary>
    /// Pins the corrected values directly (task 1.0.1's second half — "Then pin the new values"):
    /// with the by-open cutoff rule, this fixture's elapsed FTMO trading days is 5 (days 0, 1, 2, 5,
    /// 10 — trade 3's open on day 5 is now counted), not the shipped by-close count of 4.
    /// </summary>
    [Fact]
    public async Task Simulate_TradingDayRecalculationFixture_ReportsFiveElapsedTradingDays_NotFour()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedTradingDaySnapshotScenarioAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var run = result.Runs.Single();

        run.Daily!.FirstBreach!.FtmoTradingDaysElapsed.Should().Be(5);
        run.FirstLimitBreach!.FtmoTradingDaysElapsed.Should().Be(5);
    }

    /// <summary>
    /// ftmo-challenge-race, tasks.md 2.5.7 (spec.md "The Race's Phase-One Breach Matches The Shipped
    /// First Breach"): this fixture's shipped daily-loss FirstBreach (day 10) occurs well before any
    /// target could be reached (the balance never approaches +10%), so the race's phase-1 result MUST
    /// report BreachedFirst for the SAME close/limit/class as the shipped FirstBreach — exercised
    /// THROUGH THE SERVICE, not a hand-built RunPhase call.
    /// </summary>
    [Fact]
    public async Task Simulate_ChallengeRace_PhaseOneBreach_MatchesTheShippedFirstBreach()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedTradingDaySnapshotScenarioAsync(db);

        var sut = new FtmoBreachSimulationReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);
        var run = result.Runs.Single();

        var shippedBreach = run.Daily!.FirstBreach!;
        var phase1 = run.ChallengeRace!.Phase1!;

        phase1.Outcome.Should().Be(FtmoPhaseOutcome.BreachedFirst);
        phase1.OutcomeSourceClose.Should().Be(shippedBreach.SourceCloseTime);
        phase1.BreachLimit.Should().Be(FtmoFirstBreachingLimit.Daily);
        phase1.BreachPointClass.Should().Be(shippedBreach.PointClass);
    }
}
