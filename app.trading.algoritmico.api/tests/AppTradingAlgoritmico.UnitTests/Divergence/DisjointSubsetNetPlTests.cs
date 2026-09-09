using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Spec Requirement "The Trade-Set Difference Is Reported As A Set Relationship, Never As A Score":
/// each disjoint subset's net P/L is reported SEPARATELY, never folded into the offset computation
/// nor into any single residual figure. Without these figures a reader cannot tell what SHARE OF
/// THE VALUE the paired subset — the only subset the offset describes — actually covers.
/// <para>
/// The two sides do NOT compute "net" the same way, and this file pins that the asymmetry is
/// disclosed rather than flattened: demo net is <c>Profit + Commission + Swap + Taxes</c>
/// (<see cref="AnalyticsSeries"/> precedent), while a backtest <c>Profit</c> already includes
/// commission and spread and excludes swap, which SQX does not model.
/// </para>
/// </summary>
public class DisjointSubsetNetPlTests
{
    private static readonly Guid StrategyId = Guid.NewGuid();
    private const BacktestRunKind Kind = BacktestRunKind.Deploy;

    /// <summary>
    /// Every bucket populated with a distinct, recognisable net P/L so a figure landing in the
    /// wrong subset cannot pass. Prices are chosen so no offset figure collides with the
    /// combined/compared numbers the "never a residual" test forbids.
    /// </summary>
    private static (List<OpenObservation> Demo, List<OpenObservation> Backtest) BuildBucketedFixture()
    {
        var demo = new List<OpenObservation>();
        var backtest = new List<OpenObservation>();
        var baseMinute = new DateTime(2026, 4, 21, 10, 0, 0);

        // Two exact-minute pairs: demo net 10 + 20 = 30, backtest net 3 + 4 = 7.
        demo.Add(new OpenObservation(baseMinute, 15016.2m, "buy", 10m));
        backtest.Add(new OpenObservation(baseMinute, 15000m, "Buy", 3m));
        demo.Add(new OpenObservation(baseMinute.AddMinutes(1), 15025.5m, "buy", 20m));
        backtest.Add(new OpenObservation(baseMinute.AddMinutes(1), 15000m, "Buy", 4m));

        // One demo-only minute: net 100.
        demo.Add(new OpenObservation(baseMinute.AddMinutes(2), 15005m, "buy", 100m));

        // One backtest-only minute: net -50.
        backtest.Add(new OpenObservation(baseMinute.AddMinutes(3), 15000m, "Buy", -50m));

        // One ambiguous demo minute (two demo opens): net 5 + 6 = 11.
        demo.Add(new OpenObservation(baseMinute.AddMinutes(4), 15001m, "buy", 5m));
        demo.Add(new OpenObservation(baseMinute.AddMinutes(4), 15002m, "buy", 6m));

        // One ambiguous backtest minute (two backtest opens): net 7 + 8 = 15.
        backtest.Add(new OpenObservation(baseMinute.AddMinutes(5), 15000m, "Buy", 7m));
        backtest.Add(new OpenObservation(baseMinute.AddMinutes(5), 15001m, "Buy", 8m));

        return (demo, backtest);
    }

    [Fact]
    public void Measure_EveryBucketPopulated_ReportsEachDisjointSubsetsNetPlSeparately()
    {
        var (demo, backtest) = BuildBucketedFixture();

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedCount.Should().Be(2);
        result.DemoOnlyCount.Should().Be(1);
        result.BacktestOnlyCount.Should().Be(1);
        result.AmbiguousMinuteDemoCount.Should().Be(2);
        result.AmbiguousMinuteBacktestCount.Should().Be(2);

        result.PairedDemoNetPl.Should().Be(
            30m, "the paired subset's demo-side net P/L states what share of the demo value the offset covers");
        result.PairedBacktestNetPl.Should().Be(
            7m, "the paired subset's backtest-side net P/L is a separate figure on a different cost basis");
        result.DemoOnlyNetPl.Should().Be(100m, "the unpaired demo value the offset says nothing about");
        result.BacktestOnlyNetPl.Should().Be(-50m, "the unpaired backtest value the offset says nothing about");
        result.AmbiguousMinuteDemoNetPl.Should().Be(11m, "refused minutes carry value too, and it is disclosed");
        result.AmbiguousMinuteBacktestNetPl.Should().Be(15m);
    }

    [Fact]
    public void Measure_EmptySubset_ReportsNullNetPlBecauseTheArithmeticIsUndefinedNotWithheld()
    {
        var openTime = new DateTime(2026, 5, 4, 9, 30, 0);
        List<OpenObservation> demo = [new(openTime, 15022.06m, "buy", 42m)];
        List<OpenObservation> backtest = [new(openTime, 15000m, "Buy", 17m)];

        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        result.PairedDemoNetPl.Should().Be(42m, "a subset of one still publishes its figure — no invented minimum");
        result.PairedBacktestNetPl.Should().Be(17m);
        result.DemoOnlyNetPl.Should().BeNull("a net P/L over an empty subset is undefined, not zero");
        result.BacktestOnlyNetPl.Should().BeNull();
        result.AmbiguousMinuteDemoNetPl.Should().BeNull();
        result.AmbiguousMinuteBacktestNetPl.Should().BeNull();
    }

    [Fact]
    public void Dto_NetPlBasis_IsComputedPerSideNonNullableAndNotDroppableAtAnyCallSite()
    {
        foreach (var name in new[]
                 {
                     nameof(PriceOffsetComparabilityDto.DemoNetPlBasis),
                     nameof(PriceOffsetComparabilityDto.BacktestNetPlBasis),
                 })
        {
            var property = typeof(PriceOffsetComparabilityDto).GetProperty(name);

            property.Should().NotBeNull();
            Nullable.GetUnderlyingType(property!.PropertyType).Should().BeNull(
                $"a nullable {name} would let a readout state a net P/L without stating what 'net' means");
            property.GetSetMethod(nonPublic: true).Should().BeNull($"{name} is computed from a constant");

            typeof(PriceOffsetComparabilityDto).GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Should().NotContain(
                    p => p.Name == name,
                    $"{name} must not be settable at construction — only a computed property makes it non-droppable");
        }

        var dto = new PriceOffsetComparabilityDto(
            StrategyId, Kind, ComparabilityReadoutStatus.NoPairedOpens, 0, 0, 0, 0, 0, []);

        dto.DemoNetPlBasis.Should().NotBe(
            dto.BacktestNetPlBasis,
            "the two sides do not compute net the same way; one shared basis would flatten the asymmetry "
            + "and imply the figures are comparable");
        dto.Basis.Should().Be(
            ComparabilityBasis.PairedOpensOnly,
            "the net-P/L basis must not overload the offset's comparability basis — they disclose different things");
    }

    [Fact]
    public void Dto_ExposesNoCombinedComparedOrResidualNetPlMember()
    {
        var (demo, backtest) = BuildBucketedFixture();
        var result = DemoBacktestComparabilityCalculator.Measure(StrategyId, Kind, demo, backtest);

        var forbiddenWords = new[] { "Total", "Combined", "Sum", "Residual", "Difference", "Delta", "Excess", "Outperform" };
        var properties = typeof(PriceOffsetComparabilityDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            foreach (var word in forbiddenWords)
            {
                property.Name.Should().NotContain(
                    word,
                    $"{property.Name} would fold the disjoint subsets into a single figure that replaces them");
            }
        }

        var numericValues = properties
            .Select(p => p.GetValue(result))
            .OfType<decimal>()
            .ToList();

        numericValues.Should().NotContain(
            113m, "113 is every subset's net P/L summed — no single combined number may replace the parts");
        numericValues.Should().NotContain(
            23m, "23 is paired demo net minus paired backtest net — the two sides must never be compared by the code");
    }

    [Fact]
    public async Task GetAsync_PairedTrade_UsesTheDemoCostColumnsAndTheBacktestProfitAsStored()
    {
        var strategyId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var openTime = new DateTime(2026, 4, 21, 10, 15, 0);

        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
        db.BacktestRuns.Add(new BacktestRun
        {
            Id = runId,
            StrategyId = strategyId,
            Kind = BacktestRunKind.Deploy,
            SourceFileName = "deploy.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
        });
        db.StrategyTrades.Add(new StrategyTrade
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Ticket = 1,
            OpenTime = openTime,
            Type = "buy",
            Size = 0.1m,
            Item = "NDX",
            OpenPrice = 15022.06m,
            Profit = 100m,
            Commission = -5m,
            Swap = -2m,
            Taxes = -1m,
        });
        db.BacktestTrades.Add(new BacktestTrade
        {
            Id = Guid.NewGuid(),
            BacktestRunId = runId,
            RowIndex = 0,
            Ticket = 1,
            Symbol = "NDX_DARWINEX",
            Type = "Buy",
            OpenTime = openTime,
            OpenPrice = 15000.00m,
            Size = 0.1m,
            CloseTime = openTime.AddHours(1),
            ClosePrice = 15010m,
            Profit = 80m,
            SampleTypeRaw = "IST",
            CloseType = "PT",
        });
        await db.SaveChangesAsync();

        var sut = new DemoBacktestComparabilityReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.PairedDemoNetPl.Should().Be(
            92m, "demo net is Profit plus the signed commission, swap and taxes columns (AnalyticsSeries.NetOf)");
        result.PairedBacktestNetPl.Should().Be(
            80m, "a backtest Profit already includes commission and spread and excludes swap — nothing is added to it");
        result.DemoNetPlBasis.Should().Be(NetPlBasis.DemoProfitPlusCommissionSwapTaxes);
        result.BacktestNetPlBasis.Should().Be(NetPlBasis.BacktestProfitInclusiveOfCommissionAndSpreadExcludingSwap);
    }
}
