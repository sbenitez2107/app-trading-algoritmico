using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Slice C, Phase 5 — both read DTOs expose <c>SourcePlatform</c> verbatim, on EF InMemory via
/// <see cref="InMemoryDbContextFactory"/> (mirrors
/// <see cref="AppTradingAlgoritmico.UnitTests.Divergence.DemoBacktestComparabilityReadServiceTests"/>).
/// </summary>
public class BacktestReadServiceTests
{
    [Fact]
    public async Task GetRunsAsync_RunHasSourcePlatformMT5_DtoCarriesItVerbatim()
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
            SourcePlatform = PlatformType.MT5,
        });
        await db.SaveChangesAsync();

        var sut = new BacktestReadService(db);
        var result = await sut.GetRunsAsync(page: 1, pageSize: 10, default);

        result.Items.Should().ContainSingle().Which.SourcePlatform.Should().Be(PlatformType.MT5);
    }

    [Fact]
    public async Task GetRunsAsync_NullPlatform_StaysNullInTheDto()
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

        var sut = new BacktestReadService(db);
        var result = await sut.GetRunsAsync(page: 1, pageSize: 10, default);

        result.Items.Should().ContainSingle().Which.SourcePlatform.Should().BeNull("an undeclared platform must never render as MT4");
    }

    [Fact]
    public async Task GetByStrategyAsync_RunHasSourcePlatformMT5_SummaryDtoCarriesItVerbatim()
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
            SourcePlatform = PlatformType.MT5,
        });
        await db.SaveChangesAsync();

        var sut = new BacktestReadService(db);
        var result = await sut.GetByStrategyAsync(strategyId, default);

        result.Deploy!.SourcePlatform.Should().Be(PlatformType.MT5);
    }

    [Fact]
    public async Task GetByStrategyAsync_NullPlatform_StaysNullInTheSummaryDto()
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

        var sut = new BacktestReadService(db);
        var result = await sut.GetByStrategyAsync(strategyId, default);

        result.Deploy!.SourcePlatform.Should().BeNull("an undeclared platform must never render as MT4");
    }
}
