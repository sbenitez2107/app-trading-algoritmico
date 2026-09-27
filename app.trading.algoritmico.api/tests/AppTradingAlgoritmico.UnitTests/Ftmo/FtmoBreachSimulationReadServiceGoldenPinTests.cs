using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// Golden pin (tasks.md Phase 0, design.md's Testing Strategy row 0) — a CHARACTERIZATION test, not
/// a RED/GREEN pair. Written and captured on unmodified <c>main</c> before any production change in
/// the <c>ftmo-first-breach-timing</c> change: it pins the exact <c>Verdict</c>/<c>Causes</c>/
/// <c>DisclosureText</c> literals produced by this fixture, and it MUST stay green, unedited, through
/// every later task in that change (spec.md "A verdict, its causes, and its disclosure text are
/// unchanged by adding timing").
/// <para>
/// Fixture shape: a EUR symbol (coarse lot grid, mirroring the shipped
/// <c>FtmoBreachSimulationReadServiceTests</c>' FX-disagreement fixture) with (1) a CONTINGENT daily
/// breach (day 19, overlapped by a straddling position opened day 18 and closed day 20) followed by
/// (2) a distinct, later CLEAN daily breach (day 25, no overlap), and (3) a max-loss breach (day 30)
/// whose FX band DISAGREES between <c>fxLow</c> and <c>fxHigh</c> (the lot-grid floor rounds the loss
/// down just enough at <c>fxHigh</c> to stay above the static max floor, while <c>fxLow</c> breaches
/// it) — exactly the shape proposal.md's D3(b) names for the pin.
/// </para>
/// <para>
/// The literals below were captured by actually running this exact fixture through the unmodified
/// <see cref="FtmoBreachSimulationReadService"/>/<see cref="FtmoBreachEvaluator"/> (a temporary probe
/// harness, discarded after capture) — not hand-derived. The daily finding's <c>Verdict == Breached</c>
/// with empty run-level <c>Causes</c>, despite the first breaching close (day 19) being contingent, is
/// the shipped-code discard proposal.md's finding #1 names: <c>BuildFinding</c>'s <c>BreachedAt</c>
/// always passes <c>[]</c> once a later clean breach exists. First-breach timing (this change) must
/// surface that close's own <c>[ConcurrentOpenPosition]</c> cause on <c>FirstBreach.Causes</c> without
/// changing this run-level readout.
/// </para>
/// </summary>
public class FtmoBreachSimulationReadServiceGoldenPinTests
{
    private const string Broker = "FTMO";
    private const string EurSymbol = "GOLDENPIN_EUR";

    private static BacktestTrade T(
        int rowIndex, int day, decimal size, decimal profit, string closeType,
        decimal? realizedRisk, int openHour, int closeHour, int? openDay = null) => new()
        {
            RowIndex = rowIndex,
            Ticket = rowIndex + 1,
            Symbol = EurSymbol,
            Type = "Long",
            OpenTime = new DateTime(2026, 1, openDay ?? day, openHour, 0, 0, DateTimeKind.Unspecified),
            OpenPrice = 100m,
            Size = size,
            CloseTime = new DateTime(2026, 1, day, closeHour, 0, 0, DateTimeKind.Unspecified),
            ClosePrice = 101m,
            Profit = profit,
            Balance = 10_000m,
            SampleTypeRaw = "InSample",
            Segment = BacktestSegment.InSample,
            CloseType = closeType,
            RealizedRisk = realizedRisk,
        };

    [Fact]
    public async Task Simulate_ContingentThenCleanDailyBreach_PlusDisagreeingFxBandOnMax_MatchesCapturedGoldenLiterals()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "golden-pin", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);

        var trades = new List<BacktestTrade>
        {
            // Three SL closes so TradeRiskNormalizer.TryNormalize succeeds with Â=100.
            T(0, 15, 1m, -1m, "SL", 100m, 9, 10),
            T(1, 16, 1m, -1m, "SL", 100m, 9, 10),
            T(2, 17, 1m, -1m, "SL", 100m, 9, 10),
            // Overlap span: opens day 18, closes day 20 — straddles the day-19 breaching close.
            T(3, 20, 1m, 2000m, "TP", null, 8, 9, openDay: 18),
            // First breaching close (day 19): CONTINGENT — inside the overlap span above.
            T(4, 19, 1m, -700m, "SL", null, 8, 9),
            // Distinct, later CLEAN daily breach — no overlap.
            T(5, 25, 1m, -700m, "SL", null, 8, 9),
            // Max-loss breach whose FX band disagrees between fxLow (Breached) and fxHigh
            // (lot-grid rounding keeps balance above the static floor) — the FxRoundingSensitive case.
            T(6, 30, 1m, -1600m, "SL", null, 8, 9),
        };

        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "golden-pin.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategy.Id,
            Kind = BacktestRunKind.Deploy,
            Symbol = EurSymbol,
            CreatedAt = DateTime.UtcNow,
        };
        foreach (var t in trades)
            t.BacktestRunId = run.Id;
        db.BacktestRuns.Add(run);
        db.BacktestTrades.AddRange(trades);

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

        db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
        {
            SqxSymbol = EurSymbol,
            FtmoSymbol = EurSymbol,
            ContractSize = 1m,
            ProfitCurrency = "EUR",
            SizeDecimals = 0,
            Step = 1m,
            MinLot = 1m,
            MaxLots = 1000m,
            SourceTimeZoneId = "Asia/Jerusalem",
            Provenance = "test",
            CapturedOn = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        db.SymbolCalibrations.Add(new SymbolCalibration
        {
            Symbol = EurSymbol,
            Status = CalibrationStatus.Calibrated,
            PointValue = 100m,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        var sut = new FtmoBreachSimulationReadService(db);
        var request = new FtmoBreachSimulationRequest(
            strategy.Id, Broker, EurSymbol, InitialCapital: 10_000m, TargetRiskPerTrade: 100m,
            FxLow: 1.0m, FxHigh: 1.5m, SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m);

        var result = await sut.SimulateAsync(request, CancellationToken.None);
        var runResult = result.Runs.Single();

        // ---- Captured golden literals (run on unmodified main; must never be edited) ----
        runResult.Status.Should().Be(FtmoSimulationStatus.Evaluated);
        runResult.Refusal.Should().BeNull();

        runResult.Daily.Should().NotBeNull();
        runResult.Daily!.Verdict.Should().Be(FtmoBreachVerdict.Breached);
        runResult.Daily.Causes.Should().BeEmpty();
        runResult.Daily.DisclosureText.Should().Be(
            "A closed-trade breach with no identified contingency was detected in the replayed series.");

        runResult.Max.Should().NotBeNull();
        runResult.Max!.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        runResult.Max.Causes.Should().Equal(BreachContingencyCause.FxRoundingSensitive);
        runResult.Max.DisclosureText.Should().Be(
            "A closed-trade breach was detected, but it coincides with a condition that makes it "
            + "uncertain: FxRoundingSensitive.");
    }
}
