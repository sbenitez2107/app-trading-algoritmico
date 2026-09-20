using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Phase 5 — status handling and the B1 happy path, on EF InMemory via <see cref="InMemoryDbContextFactory"/>.</summary>
public class CostDecompositionReadServiceTests
{
    [Fact]
    public async Task GetAsync_WhenNoRunExistsForKind_ReturnsNoRunForKindStatus()
    {
        var strategyId = Guid.NewGuid();
        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(new Strategy { Id = strategyId, Name = "S" });
        await db.SaveChangesAsync();

        var sut = new CostDecompositionReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(CostDecompositionStatus.NoRunForKind);
        result.Coverage.Months.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenRunExistsButNoDemoTrades_ReturnsNoDemoTradesStatus()
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

        var sut = new CostDecompositionReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(CostDecompositionStatus.NoDemoTrades);
        result.Coverage.Months.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenDataPresent_ReturnsCoverageComponentOnlyStatusWithComparabilityAndCoverage()
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

        var sut = new CostDecompositionReadService(db);
        var result = await sut.GetAsync(strategyId, BacktestRunKind.Deploy, default);

        result.Status.Should().Be(CostDecompositionStatus.CoverageComponentOnly);
        result.Comparability.PairedCount.Should().Be(1);
        result.Coverage.Months.Should().ContainSingle(m => m.Year == 2026 && m.Month == 4);
    }
}
