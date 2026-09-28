using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR4, Phase 4.3/4.4 — <see cref="FtmoMultiStartReadService"/> end-to-end: outcome
/// coverage/shares (spec.md "Aggregates Are Counts, Shares, And Order Statistics"), censored runway,
/// start-1 equivalence with the single-start race, the Unscalable-first-trade divergence disclosure,
/// cancellation, and the disclosure/banned-wording sweep. Uses the in-memory EF provider — no
/// migration is applied to any real database.
/// </summary>
public class FtmoMultiStartReadServiceTests
{
    private const string Broker = "FTMO";
    private const string XauSymbol = "XAUUSD_MULTISTART";
    private static readonly DateTime SafeDay = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "multi-start test strategy", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    private static async Task<Guid> SeedRunAsync(AppDbContext db, Guid strategyId, IEnumerable<BacktestTrade> trades)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "multi-start.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = BacktestRunKind.Deploy,
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
        decimal? realizedRisk = null, DateTime? open = null, DateTime? close = null) => new()
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
            Segment = BacktestSegment.InSample,
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

    /// <summary>A start whose phase 1 reaches +10% on day 4 (day-min met, flat book) and phase 2 carries on.</summary>
    private static List<BacktestTrade> TargetReachingTrades()
    {
        var trades = SlCalibrationTrades();
        trades.Add(MakeTrade(3, size: 1.00m, profit: 1_100m, closeType: "TP", open: SafeDay.AddDays(4).AddHours(-1), close: SafeDay.AddDays(4)));
        trades.Add(MakeTrade(4, size: 1.00m, profit: 10m, closeType: "TP", open: SafeDay.AddDays(5).AddHours(-1), close: SafeDay.AddDays(5)));
        return trades;
    }

    private static async Task SeedBrokerRiskLimitsAsync(AppDbContext db)
    {
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            Verified = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedInstrumentSpecAsync(AppDbContext db)
    {
        db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
        {
            SqxSymbol = XauSymbol,
            FtmoSymbol = XauSymbol,
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
        await db.SaveChangesAsync();
    }

    private static async Task SeedCalibrationAsync(AppDbContext db)
    {
        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Symbol = XauSymbol,
            Status = CalibrationStatus.Calibrated,
            PointValue = 100m,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static FtmoBreachSimulationRequest Request(Guid strategyId) =>
        new(strategyId, Broker, XauSymbol, InitialCapital: 10_000m, TargetRiskPerTrade: 100m,
            FxLow: null, FxHigh: null, SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m);

    private static async Task<Guid> SeedFullFixtureAsync(AppDbContext db, IEnumerable<BacktestTrade> trades)
    {
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, trades);
        await SeedBrokerRiskLimitsAsync(db);
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);
        return strategyId;
    }

    // ---- 4.3.1 / 4.3.2 / 4.3.3 ----

    [Fact]
    public async Task EveryStartLandsInExactlyOneOfTheSixOutcomes()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedFullFixtureAsync(db, TargetReachingTrades());

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Starts.Should().HaveCount(1);
        run.Summary.Should().NotBeNull();
        run.Summary!.Outcomes.Sum(o => o.Count).Should().Be(run.Summary.StartCount);
    }

    [Fact]
    public async Task AggregateSharesIncludeOutcomesWithZeroOccurrences()
    {
        await using var db = InMemoryDbContextFactory.Create();
        // FundedBreached never occurs on this fixture: phase 1's target close leaves no further trade,
        // so the funded phase is NotStarted -> Classify never returns FundedBreached.
        var strategyId = await SeedFullFixtureAsync(db, TargetReachingTrades());

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var summary = result.Runs.Single().Summary!;
        summary.Outcomes.Should().HaveCount(6, "every one of the six outcomes is reported, zeros included");
        summary.Outcomes.Should().Contain(
            o => o.Outcome == FtmoChainOutcome.FundedBreached && o.Count == 0 && o.Share == 0m,
            "an outcome that never occurs is still reported, not omitted");
    }

    [Fact]
    public async Task EveryOutcomesShareIsComputedOverTheFullStartCount()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedFullFixtureAsync(db, TargetReachingTrades());

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var summary = result.Runs.Single().Summary!;
        summary.Outcomes.Sum(o => o.Share).Should().Be(1m);
    }

    // ---- 4.3.4 ----

    [Fact]
    public async Task ACensoredStartReportsItsRunwayNotAFabricatedCutoff()
    {
        await using var db = InMemoryDbContextFactory.Create();
        // Only the SL calibration trades: phase 1 never reaches its target -> Phase1UndecidedAtEndOfData.
        var strategyId = await SeedFullFixtureAsync(db, SlCalibrationTrades());

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var row = result.Runs.Single().Starts.Single();
        row.Outcome.Should().Be(FtmoChainOutcome.Phase1UndecidedAtEndOfData);
        row.IsCensored.Should().BeTrue();
        row.RunwayCalendarDays.Should().NotBeNull();
    }

    // ---- 4.3.5 ----

    [Fact]
    public async Task StartOneEqualsTheSingleStartChallengeRaceWhenTheFirstTradeIsScalable()
    {
        await using var singleDb = InMemoryDbContextFactory.Create();
        var singleStrategyId = await SeedFullFixtureAsync(singleDb, TargetReachingTrades());
        var singleResult = await new FtmoBreachSimulationReadService(singleDb).SimulateAsync(Request(singleStrategyId), CancellationToken.None);
        var singleRace = singleResult.Runs.Single().ChallengeRace!;

        await using var multiDb = InMemoryDbContextFactory.Create();
        var multiStrategyId = await SeedFullFixtureAsync(multiDb, TargetReachingTrades());
        var multiResult = await new FtmoMultiStartReadService(multiDb).SimulateAsync(Request(multiStrategyId), CancellationToken.None);
        var start1 = multiResult.Runs.Single().Starts.Single();

        start1.Phase1.Outcome.Should().Be(singleRace.Phase1!.Outcome);
        start1.Phase1.OutcomeSourceClose.Should().Be(singleRace.Phase1.OutcomeSourceClose);
        start1.Phase2.Outcome.Should().Be(singleRace.Phase2!.Outcome);
    }

    // ---- 4.3.6 ----

    [Fact]
    public async Task AnUnscalableFirstTradeMakesStartOneDivergeFromTheAnchorDisclosed()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var trades = new List<BacktestTrade>
        {
            // Size <= 0 is Unscalable (FtmoTradeProjector.Project) — this row anchors the SINGLE-start
            // replay (FtmoReplayCalendar.Build includes Unscalable rows) but is excluded from start
            // enumeration (FtmoStartEnumerator only considers scalable opens).
            MakeTrade(0, size: 0m, profit: 0m, closeType: "TP", open: SafeDay.AddDays(-1).AddHours(-1), close: SafeDay.AddDays(-1)),
            MakeTrade(1, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
            MakeTrade(2, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
            MakeTrade(3, size: 1.00m, profit: -1m, closeType: "SL", realizedRisk: 100m),
        };
        var strategyId = await SeedFullFixtureAsync(db, trades);

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Start1DiffersFromSingleStartAnchor.Should().BeTrue();
        run.Starts.Single().StartSourceOpen.Should().NotBe(trades[0].OpenTime);
    }

    // ---- 4.3.8 ----

    [Fact]
    public async Task CancellationMidRunThrowsBeforeTheNextStart()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedFullFixtureAsync(db, TargetReachingTrades());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = new FtmoMultiStartReadService(db);
        var act = async () => await sut.SimulateAsync(Request(strategyId), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- 4.4.1 ----

    [Fact]
    public async Task DisclosureCoversNonIndependenceOptimismAndUnmodelledWithdrawals()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedFullFixtureAsync(db, TargetReachingTrades());

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var disclosure = result.Runs.Single().Disclosure.ToLowerInvariant();
        disclosure.Should().Contain("not independent");
        disclosure.Should().Contain("not probabilities");
        disclosure.Should().Contain("understate breaches");
        disclosure.Should().Contain("scaling plan");
    }

    // ---- 4.4.2: banned-wording sweep over every new type this capability produces ----

    [Fact]
    public void NoOutputContainsBannedSurvivalWordingAcrossEveryNewEnumTypeOrTheDisclosure()
    {
        var banned = new[] { "passed", "safe", "survived", "would have passed" };

        var disclosure = FtmoMultiStartReadService.Disclosure.ToLowerInvariant();
        foreach (var word in banned)
            disclosure.Should().NotContain(word);

        var enumTypes = new[] { typeof(FtmoChainOutcome), typeof(FtmoFundedOutcome), typeof(FtmoStartGrain) };
        foreach (var enumType in enumTypes)
        {
            Enum.GetUnderlyingType(enumType).Should().NotBe(typeof(bool));
            foreach (var name in Enum.GetNames(enumType))
            {
                var lower = name.ToLowerInvariant();
                foreach (var word in banned)
                    lower.Should().NotContain(word);
            }
        }

        var dtoTypes = new[]
        {
            typeof(FtmoMultiStartDto), typeof(FtmoMultiStartRunDto), typeof(FtmoMultiStartRowDto),
            typeof(FtmoFundedPhaseDto), typeof(FtmoOutcomeCountDto), typeof(FtmoOrderStatisticsDto),
            typeof(FtmoMultiStartSummaryDto),
        };
        foreach (var dtoType in dtoTypes)
        {
            var typeName = dtoType.Name.ToLowerInvariant();
            foreach (var word in banned)
                typeName.Should().NotContain(word);

            foreach (var property in dtoType.GetProperties())
            {
                var propertyName = property.Name.ToLowerInvariant();
                foreach (var word in banned)
                    propertyName.Should().NotContain(word);
            }
        }
    }

    // ---- Spec gap pin (CONFIRMED default — user confirmation obtained; see design.md/tasks.md for the
    // recorded decision): neither spec.md nor design.md names a scenario for a run whose PROJECTED
    // trades are entirely Unscalable (FtmoStartEnumerator.Enumerate returns zero starts AND zero
    // MonthsWithoutStart, since there is no first/last month to range over). The user confirmed the
    // CURRENT default (Starts=[], MonthsWithoutStart=[], Summary=null, no new refusal reason) as final,
    // BECAUSE this path is unreachable through the endpoint anyway (see the discovery below): there is
    // no observable behaviour left to fabricate a refusal for. This pin keeps a future change from
    // silently altering that confirmed default.
    //
    // DISCOVERY (apply-time, not silently swept): this path is UNREACHABLE through the full DB-backed
    // SimulateAsync today. TradeRiskNormalizer.TryNormalize requires >= 3 SL trades with Size > 0
    // (TradeRiskNormalizer.MinimumSlSamples) to estimate a run's risk-per-trade at all; those same rows
    // are then themselves scalable under FtmoTradeProjector.Project's OWN rule (only Size <= 0 ->
    // Unscalable), so a run whose calibration succeeds always has at least one scalable trade, and a run
    // with zero scalable trades always fails calibration first (RiskNotEstimable, a Refused status, not
    // Evaluated) — TradeRiskNormalizer refuses it as RiskNotEstimable before FtmoStartEnumerator ever
    // runs. Confirmed by AnAllUnscalableRun_.../TryNormalizeConfirmsRiskNotEstimableFiresFirst below.
    // This pin therefore exercises FtmoMultiStartReadService.ComputeRun directly (the internal seam from
    // already-projected trades onward — see the PR4 follow-up benchmark), which is exactly where
    // FtmoStartEnumerator.Enumerate's own empty-result behaviour is actually consumed. ----

    [Fact]
    public void AnAllUnscalableProjectedSeries_ReportsEmptyStartsAndNullSummary()
    {
        var jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var allUnscalable = new[]
        {
            new FtmoTradeProjector.ProjectedTrade(0, SafeDay, SafeDay.AddHours(1), Net: null, ResizeOutcome.Unscalable, FtmoLots: 0m),
            new FtmoTradeProjector.ProjectedTrade(1, SafeDay.AddDays(40), SafeDay.AddDays(40).AddHours(1), Net: null, ResizeOutcome.Unscalable, FtmoLots: 0m),
        };
        var rules = new FtmoChallengeRulesDto(0.10m, 0.05m, 4, TimeLimitDays: null);

        var run = FtmoMultiStartReadService.ComputeRun(
            Guid.NewGuid(), BacktestRunKind.Deploy, BacktestSegment.InSample, allUnscalable, allUnscalable,
            jerusalem, berlin, initialCapital: 10_000m, dailyPct: 0.05m, maxPct: 0.10m, profitTargetPct: null,
            fxBand: (1m, 1m), unscalableCount: 2, rules, CancellationToken.None);

        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.Starts.Should().BeEmpty();
        run.MonthsWithoutStart.Should().BeEmpty("there is no first/last month to range over when nothing is scalable");
        run.Summary.Should().BeNull();
    }

    [Fact]
    public async Task AnAllUnscalableRun_TryNormalizeConfirmsRiskNotEstimableFiresFirst()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedFullFixtureAsync(db,
        [
            MakeTrade(0, size: 0m, profit: 0m, closeType: "TP"),
            MakeTrade(1, size: 0m, profit: 0m, closeType: "TP"),
        ]);

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        // Documents the discovery above: a genuinely all-Unscalable run refuses at calibration
        // (RiskNotEstimable), never reaching the empty-starts/null-summary path this pin exercises above.
        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Refused);
        run.Refusal.Should().Be(FtmoSimulationRefusal.RiskNotEstimable);
    }

    // ---- RELIABILITY-001: the multi-start ProfitTargetMismatch branch (FtmoMultiStartReadService.cs,
    // ComputeRun) had no direct test. Mirrors FtmoChallengeRace.Evaluate's own ProfitTargetMismatch
    // refusal (FtmoChallengeRaceTests, FtmoBreachSimulationReadServiceTests) at the multi-start level:
    // a stored profit target != Phase1TargetPct (10%) refuses the race, echoes the stored value, and
    // reports no starts/summary while leaving the run itself Evaluated (not Refused — the whole-run
    // refusal path is orthogonal to this race-only refusal). ----

    [Fact]
    public async Task AStoredProfitTargetOtherThanTenPercentRefusesTheRaceWithTheStoredValueEchoed()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyAsync(db);
        await SeedRunAsync(db, strategyId, TargetReachingTrades());
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = 0.08m,
            Verified = true,
        });
        await db.SaveChangesAsync();
        await SeedInstrumentSpecAsync(db);
        await SeedCalibrationAsync(db);

        var sut = new FtmoMultiStartReadService(db);
        var result = await sut.SimulateAsync(Request(strategyId), CancellationToken.None);

        var run = result.Runs.Single();
        run.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        run.RaceRefusal.Should().Be(FtmoChallengeRaceRefusal.ProfitTargetMismatch);
        run.StoredProfitTargetPct.Should().Be(0.08m);
        run.Starts.Should().BeEmpty();
        run.Summary.Should().BeNull();
    }
}
