using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Task 5.1/5.2 — status handling and happy-path values, on EF InMemory via
/// <see cref="InMemoryDbContextFactory"/> (the full <c>AppDbContext</c>). The QUERY-COST claim (at
/// most three database commands, no entity materialization) is asserted separately, on real
/// SQLite, in <see cref="DemoBacktestComparabilityQueryCostTests"/> — the full
/// <c>AppDbContext</c> cannot be created on SQLite (unrelated configurations declare
/// <c>nvarchar(max)</c>), the same documented trade-off <c>BacktestReadinessQueryCostTests</c>
/// already makes for a sibling read path.
/// </summary>
public class DemoBacktestComparabilityReadServiceTests
{
    [Fact]
    public async Task GetAsync_StrategyWithNoRunOfThatKind_ReturnsNoRunForKindAndNoFigures()
    {
        var strategyId = Guid.NewGuid();
        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
        await db.SaveChangesAsync();

        var sut = new DemoBacktestComparabilityReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(ComparabilityReadoutStatus.NoRunForKind);
        result.PairedCount.Should().Be(0);
        result.Months.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_RunExistsButStrategyHasNoDemoTrades_ReturnsNoDemoTrades()
    {
        var strategyId = Guid.NewGuid();
        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
        db.BacktestRuns.Add(new BacktestRun
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Kind = BacktestRunKind.Deploy,
            SourceFileName = "deploy.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
        });
        await db.SaveChangesAsync();

        var sut = new DemoBacktestComparabilityReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(ComparabilityReadoutStatus.NoDemoTrades);
    }

    [Fact]
    public async Task GetAsync_DemoAndBacktestTradesShareAMinute_ReturnsMeasuredWithPairedCount()
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
            SampleTypeRaw = "IST",
            CloseType = "PT",
        });
        await db.SaveChangesAsync();

        var sut = new DemoBacktestComparabilityReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(ComparabilityReadoutStatus.Measured);
        result.PairedCount.Should().Be(1);
        result.Months.Should().ContainSingle(m => m.Year == 2026 && m.Month == 4);
    }

    [Fact]
    public async Task GetAsync_RunKindOnlyInTheOtherSlot_NeverFallsBackToIt()
    {
        var strategyId = Guid.NewGuid();
        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
        db.BacktestRuns.Add(new BacktestRun
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Kind = BacktestRunKind.Evaluation,
            SourceFileName = "evaluation.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
        });
        await db.SaveChangesAsync();

        var sut = new DemoBacktestComparabilityReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(
            ComparabilityReadoutStatus.NoRunForKind,
            "the caller named Deploy; an Evaluation run existing must not silently answer for it (D3)");
    }
}
