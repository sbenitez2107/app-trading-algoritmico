using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B1 (design.md D1) — the split of <c>ResolveSharedAsync</c> into
/// <c>ResolveLimitsAsync</c> + pure <c>ResolveSymbol</c> (+ async wrapper), and the composition that
/// must keep the shipped precedence and the shipped DB-query shape unchanged.
/// </summary>
public class FtmoSimulationInputsResolveSymbolTests
{
    private const string Broker = "FTMO";
    private const string Symbol = "XAUUSD_B1";

    private static FtmoInstrumentSpec Spec(
        string currency = "USD", string zone = "Asia/Jerusalem", decimal contractSize = 100m,
        int sizeDecimals = 2, decimal step = 0.01m, decimal minLot = 0.01m, decimal maxLots = 1000m) => new()
        {
            SqxSymbol = Symbol,
            FtmoSymbol = Symbol,
            ContractSize = contractSize,
            ProfitCurrency = currency,
            SizeDecimals = sizeDecimals,
            Step = step,
            MinLot = minLot,
            MaxLots = maxLots,
            SourceTimeZoneId = zone,
            Provenance = "test",
            CapturedOn = new DateOnly(2026, 1, 1),
        };

    private static SymbolCalibration Calibration(
        CalibrationStatus status = CalibrationStatus.Calibrated, decimal? pointValue = 100m) => new()
        {
            Symbol = Symbol,
            Status = status,
            PointValue = pointValue,
            SampleCount = 10,
            CalibratedAt = DateTime.UtcNow,
        };

    // ---- B1.1.1: refusal order spec -> calibration -> FX ----

    [Fact]
    public void ResolveSymbol_NullSpec_RefusesInstrumentSpecMissing()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(null, Calibration(), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
        result.Spec.Should().BeNull();
        result.FtmoGrid.Should().BeNull();
    }

    [Fact]
    public void ResolveSymbol_NonPositiveContractSize_RefusesInstrumentSpecMissing()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(contractSize: 0m), Calibration(), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    [Fact]
    public void ResolveSymbol_SpecGridThatDoesNotConstruct_RefusesInstrumentSpecMissing()
    {
        // Step 0.01 needs 2 decimals; declaring 1 makes LotGrid throw ArgumentOutOfRangeException.
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(sizeDecimals: 1), Calibration(), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
        result.FtmoGrid.Should().BeNull();
    }

    [Fact]
    public void ResolveSymbol_SpecRefused_CalibrationThatWouldRefuseNeverWins()
    {
        // The calibration is uncalibrated (would be PointValueNotCalibrated) AND the spec is missing:
        // the spec refusal wins because the calibration is only consulted for a usable spec.
        var result = FtmoSimulationInputs.ResolveSymbol(
            null, Calibration(CalibrationStatus.InsufficientSamples, null), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    [Fact]
    public void ResolveSymbol_SpecRefused_ReportsNoPointValueEvenWithAUsableCalibration()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(null, Calibration(), null, null);

        result.PointValue.Should().Be(0m);
    }

    [Fact]
    public void ResolveSymbol_NullCalibration_RefusesPointValueNotCalibrated()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(), null, null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
        result.Spec.Should().NotBeNull();
        result.FtmoGrid.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSymbol_CalibratedStatusWithNullPointValue_RefusesPointValueNotCalibrated()
    {
        // Calibrated == 0 is the CLR default, so the null PointValue is checked independently of Status.
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(), Calibration(CalibrationStatus.Calibrated, null), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public void ResolveSymbol_NonCalibratedStatus_RefusesPointValueNotCalibrated()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(
            Spec(), Calibration(CalibrationStatus.InsufficientSamples, 100m), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public void ResolveSymbol_NonPositivePointValue_RefusesPointValueNotCalibrated()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(), Calibration(CalibrationStatus.Calibrated, 0m), null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    [Fact]
    public void ResolveSymbol_CalibrationRefusal_BeatsAnFxRefusal()
    {
        // Non-USD spec with no FX declared AND no calibration: calibration comes first.
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(currency: "EUR"), null, null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
    }

    // ---- B1.1.2: FX, zone, helpers ----

    [Fact]
    public void ResolveSymbol_UsdSpec_YieldsUnitBandAndIgnoresAnyDeclaredFx()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(currency: "USD"), Calibration(), null, null);
        var withFx = FtmoSimulationInputs.ResolveSymbol(Spec(currency: "usd"), Calibration(), 0.9m, 1.1m);

        result.Refusal.Should().BeNull();
        result.FxBand.Should().Be((1m, 1m));
        withFx.Refusal.Should().BeNull();
        withFx.FxBand.Should().Be((1m, 1m));
    }

    [Fact]
    public void ResolveSymbol_NonUsdSpecWithDeclaredBand_UsesTheBand()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(currency: "EUR"), Calibration(), 1.05m, 1.15m);

        result.Refusal.Should().BeNull();
        result.FxBand.Should().Be((1.05m, 1.15m));
        result.PointValue.Should().Be(100m);
    }

    [Theory]
    [InlineData(null, 1.1)]
    [InlineData(1.05, null)]
    [InlineData(null, null)]
    public void ResolveSymbol_NonUsdSpecWithoutBothBandEnds_RefusesFxRateNotDeclared(double? low, double? high)
    {
        var result = FtmoSimulationInputs.ResolveSymbol(
            Spec(currency: "EUR"), Calibration(), (decimal?)low, (decimal?)high);

        result.Refusal.Should().Be(FtmoSimulationRefusal.FxRateNotDeclared);
        result.FxBand.Should().Be((1m, 1m));
    }

    [Theory]
    [InlineData(0, 1.1)]
    [InlineData(1.05, 0)]
    [InlineData(1.2, 1.1)]
    public void ResolveSymbol_NonUsdSpecWithDegenerateBand_RefusesInvalidFxBand(double low, double high)
    {
        var result = FtmoSimulationInputs.ResolveSymbol(
            Spec(currency: "EUR"), Calibration(), (decimal)low, (decimal)high);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InvalidFxBand);
    }

    [Fact]
    public void ResolveSymbol_UsableSpec_ResolvesSourceZoneAndLeavesZoneRefusalNull()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(zone: "Asia/Jerusalem"), Calibration(), null, null);

        result.Refusal.Should().BeNull();
        result.ZoneRefusal.Should().BeNull();
        result.SourceZone.Should().NotBeNull();
        result.SourceZone!.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem").Id);
        result.Spec.Should().NotBeNull();
        result.FtmoGrid.Should().NotBeNull();
        result.PointValue.Should().Be(100m);
    }

    [Fact]
    public void ResolveSymbol_UnresolvableSourceZone_SetsZoneRefusalSeparatelyFromRefusal()
    {
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(zone: "Not/AZone"), Calibration(), null, null);

        result.Refusal.Should().BeNull("the zone refusal is kept apart so InvalidRequest can still win in the composition");
        result.ZoneRefusal.Should().Be(FtmoSimulationRefusal.TimeZoneDataUnavailable);
        result.SourceZone.Should().BeNull();
    }

    [Fact]
    public void ResolveSymbol_RefusedSymbol_NeverEvaluatesTheZone()
    {
        // The zone is unresolvable too, but the calibration refusal is the one reported.
        var result = FtmoSimulationInputs.ResolveSymbol(Spec(zone: "Not/AZone"), null, null, null);

        result.Refusal.Should().Be(FtmoSimulationRefusal.PointValueNotCalibrated);
        result.ZoneRefusal.Should().BeNull();
    }

    [Fact]
    public void TryResolveBerlin_ResolvesTheBerlinZone()
    {
        var ok = FtmoSimulationInputs.TryResolveBerlin(out var zone);

        ok.Should().BeTrue();
        zone.Should().NotBeNull();
        zone!.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin").Id);
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("EUR", false)]
    public void SettlesInAccountCurrency_IsTrueOnlyForUsd(string currency, bool expected)
    {
        FtmoSimulationInputs.SettlesInAccountCurrency(Spec(currency: currency)).Should().Be(expected);
    }

    // ---- B1.1.2: async wrapper reads what the pure core needs ----

    [Fact]
    public async Task ResolveSymbolAsync_UsableSpecAndCalibration_MatchesThePureCore()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.FtmoInstrumentSpecs.Add(Spec(currency: "EUR"));
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSymbolAsync(db, Symbol, 1.05m, 1.15m, CancellationToken.None);

        result.Refusal.Should().BeNull();
        result.FxBand.Should().Be((1.05m, 1.15m));
        result.PointValue.Should().Be(100m);
        result.Spec!.SqxSymbol.Should().Be(Symbol);
    }

    [Fact]
    public async Task ResolveSymbolAsync_MissingSpecAndMissingCalibration_SpecRefusalWins()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await FtmoSimulationInputs.ResolveSymbolAsync(db, Symbol, null, null, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InstrumentSpecMissing);
    }

    // ---- B1.1.3: ResolveLimitsAsync ----

    private static BrokerRiskLimits Limits(
        decimal? daily = 0.05m, decimal? max = 0.10m, FtmoProduct? product = FtmoProduct.TwoStep,
        DrawdownModel? model = DrawdownModel.Static, decimal? target = 0.10m) => new()
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = product,
            DrawdownModel = model,
            DailyLossLimitPct = daily,
            MaxLossLimitPct = max,
            ProfitTargetPct = target,
            Verified = true,
        };

    [Fact]
    public async Task ResolveLimitsAsync_NoRow_RefusesLimitsNotConfigured()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.LimitsNotConfigured);
        result.DailyPct.Should().Be(0m);
        result.MaxPct.Should().Be(0m);
        result.ProfitTargetPct.Should().BeNull();
    }

    [Theory]
    [InlineData(null, 0.10)]
    [InlineData(0.05, null)]
    [InlineData(0.0, 0.10)]
    [InlineData(0.05, 1.5)]
    public async Task ResolveLimitsAsync_UnusablePercentages_RefuseLimitsNotConfigured(double? daily, double? max)
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(Limits((decimal?)daily, (decimal?)max));
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.LimitsNotConfigured);
    }

    [Fact]
    public async Task ResolveLimitsAsync_ProductNotTwoStep_RefusesButCarriesTheLimits()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(Limits(product: FtmoProduct.OneStep, model: DrawdownModel.Trailing));
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.ProductNotTwoStep, "product is checked before the drawdown model");
        result.DailyPct.Should().Be(0.05m);
        result.MaxPct.Should().Be(0.10m);
    }

    [Fact]
    public async Task ResolveLimitsAsync_NullProduct_RefusesProductNotTwoStep()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(Limits(product: null));
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.ProductNotTwoStep);
    }

    [Fact]
    public async Task ResolveLimitsAsync_DrawdownNotStatic_RefusesDrawdownModelNotStatic()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(Limits(model: DrawdownModel.Trailing));
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.DrawdownModelNotStatic);
    }

    [Fact]
    public async Task ResolveLimitsAsync_Configured_CarriesDailyMaxAndProfitTarget()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(Limits(daily: 0.04m, max: 0.09m, target: 0.08m));
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveLimitsAsync(db, Broker, CancellationToken.None);

        result.Refusal.Should().BeNull();
        result.DailyPct.Should().Be(0.04m);
        result.MaxPct.Should().Be(0.09m);
        result.ProfitTargetPct.Should().Be(0.08m);
    }

    // ---- B1.1.4: composition precedence through the unchanged ResolveSharedAsync ----

    private static async Task<Guid> SeedStrategyWithRunAsync(AppDbContext db)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "b1 strategy", CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = "b1.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategy.Id,
            Kind = BacktestRunKind.Deploy,
            Symbol = Symbol,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    private static FtmoBreachSimulationRequest Request(
        Guid strategyId, decimal capital = 10_000m, decimal risk = 100m, decimal? fxLow = null, decimal? fxHigh = null) =>
        new(strategyId, Broker, Symbol, capital, risk, fxLow, fxHigh,
            SizeDecimals: 2, Step: 0.01m, MinLot: 0.01m, MaxLots: 10m);

    [Fact]
    public async Task ResolveSharedAsync_InvalidRequestAndUnresolvableZone_InvalidRequestWins()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyWithRunAsync(db);
        db.BrokerRiskLimits.Add(Limits());
        db.FtmoInstrumentSpecs.Add(Spec(zone: "Not/AZone"));
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(db, Request(strategyId, capital: 0m), CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.InvalidRequest);
        result.SourceZone.Should().BeNull();
        result.BerlinZone.Should().BeNull();
    }

    [Fact]
    public async Task ResolveSharedAsync_ValidRequestAndUnresolvableZone_RefusesTimeZoneDataUnavailable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyWithRunAsync(db);
        db.BrokerRiskLimits.Add(Limits());
        db.FtmoInstrumentSpecs.Add(Spec(zone: "Not/AZone"));
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(db, Request(strategyId), CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.TimeZoneDataUnavailable);
        result.Spec.Should().NotBeNull();
        result.DailyPct.Should().Be(0.05m);
        result.PointValue.Should().Be(100m);
    }

    [Fact]
    public async Task ResolveSharedAsync_AllInputsUsable_FillsEveryField()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyWithRunAsync(db);
        db.BrokerRiskLimits.Add(Limits(target: 0.10m));
        db.FtmoInstrumentSpecs.Add(Spec(currency: "EUR"));
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(
            db, Request(strategyId, fxLow: 1.05m, fxHigh: 1.15m), CancellationToken.None);

        result.NoRuns.Should().BeFalse();
        result.Refusal.Should().BeNull();
        result.Runs.Should().HaveCount(1);
        result.DailyPct.Should().Be(0.05m);
        result.MaxPct.Should().Be(0.10m);
        result.Spec.Should().NotBeNull();
        result.SourceGrid.Should().NotBeNull();
        result.FtmoGrid.Should().NotBeNull();
        result.PointValue.Should().Be(100m);
        result.FxBand.Should().Be((1.05m, 1.15m));
        result.SourceZone.Should().NotBeNull();
        result.BerlinZone.Should().NotBeNull();
        result.ProfitTargetPct.Should().Be(0.10m);
    }

    [Fact]
    public async Task ResolveSharedAsync_NoRuns_ReturnsNoRunsBeforeAnyLimitsRefusal()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(db, Request(Guid.NewGuid()), CancellationToken.None);

        result.NoRuns.Should().BeTrue();
        result.Refusal.Should().BeNull("no limits row exists, but the runs check comes first");
    }

    [Fact]
    public async Task ResolveSharedAsync_LimitsNotConfigured_ReturnsTheZeroedResolutionWithoutASourceGrid()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyWithRunAsync(db);
        db.FtmoInstrumentSpecs.Add(Spec());
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(db, Request(strategyId), CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.LimitsNotConfigured);
        result.DailyPct.Should().Be(0m);
        result.Spec.Should().BeNull("the spec query is never issued once the limits refuse");
        result.SourceGrid.Should().BeNull();
        result.FxBand.Should().Be((1m, 1m));
    }

    [Fact]
    public async Task ResolveSharedAsync_ProductRefusal_KeepsLimitsAndSourceGridButNeverReadsTheSymbol()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var strategyId = await SeedStrategyWithRunAsync(db);
        db.BrokerRiskLimits.Add(Limits(product: FtmoProduct.OneStep));
        db.FtmoInstrumentSpecs.Add(Spec());
        db.SymbolCalibrations.Add(Calibration());
        await db.SaveChangesAsync();

        var result = await FtmoSimulationInputs.ResolveSharedAsync(db, Request(strategyId), CancellationToken.None);

        result.Refusal.Should().Be(FtmoSimulationRefusal.ProductNotTwoStep);
        result.DailyPct.Should().Be(0.05m);
        result.MaxPct.Should().Be(0.10m);
        result.ProfitTargetPct.Should().Be(0.10m);
        result.SourceGrid.Should().NotBeNull("the shipped path builds the source grid regardless of the limits refusal");
        result.Spec.Should().BeNull("the symbol queries are not issued when the limits refuse");
        result.PointValue.Should().Be(0m);
    }
}
