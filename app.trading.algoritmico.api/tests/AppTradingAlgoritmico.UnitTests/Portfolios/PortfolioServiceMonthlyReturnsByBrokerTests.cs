using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Unit tests for <see cref="PortfolioService.GetMonthlyReturnsByBrokerAsync"/>.
/// Uses the EF InMemory provider (same harness as PortfolioServiceSummariesTests).
/// Verifies that the bulk matrix matches the single-portfolio endpoint row for row,
/// plus the broker filter, CreatedAt-DESC ordering, and empty-portfolio behaviour.
/// </summary>
public class PortfolioServiceMonthlyReturnsByBrokerTests
{
    // -------------------------------------------------------------------------
    // Builders
    // -------------------------------------------------------------------------

    private static StrategyTrade Trade(
        Guid strategyId,
        long ticket,
        DateTime open,
        DateTime close,
        decimal profit) => new()
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Ticket = ticket,
            OpenTime = open,
            CloseTime = close,
            Type = "buy",
            Size = 0.1m,
            Item = "EURUSD",
            OpenPrice = 1.1000m,
            ClosePrice = 1.1050m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = 0m,
            Taxes = 0m,
            Swap = 0m,
            Profit = profit,
            IsOpen = false
        };

    private static Strategy MakeStrategy(Guid id, string name, Guid? accountId = null) =>
        new() { Id = id, Name = name, TradingAccountId = accountId };

    private static Portfolio MakePortfolio(
        Guid id,
        string name,
        string broker,
        decimal initialCapital,
        DateTime createdAt,
        params Guid[] strategyIds)
    {
        var p = new Portfolio
        {
            Id = id,
            Name = name,
            Broker = broker,
            AccountType = AccountType.Live,
            InitialCapital = initialCapital,
            BaseCurrency = "USD",
            CreatedAt = createdAt
        };

        foreach (var sid in strategyIds)
            p.Members.Add(new PortfolioStrategy { StrategyId = sid, Weight = 1m });

        return p;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_NoPortfolios_ReturnsEmptyList()
    {
        // Arrange
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetMonthlyReturnsByBrokerAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_MatchesSinglePortfolioEndpoint()
    {
        // Arrange — trades spread over three months so the compounding chain has real steps.
        var stratA = Guid.NewGuid();
        var stratB = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.AddRange(
            MakeStrategy(stratA, "A"),
            MakeStrategy(stratB, "B"));
        db.StrategyTrades.AddRange(
            Trade(stratA, 1, new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc), 1_000m),
            Trade(stratB, 2, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 5, 15, 0, 0, DateTimeKind.Utc), -400m),
            Trade(stratA, 3, new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 20, 15, 0, 0, DateTimeKind.Utc), 250m));
        db.Portfolios.Add(MakePortfolio(portfolioId, "Combined", "Darwinex", 100_000m, createdAt, stratA, stratB));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act — the bulk matrix must agree with the per-portfolio endpoint it replaces.
        var expected = await sut.GetMonthlyReturnsAsync(portfolioId);
        var matrix = await sut.GetMonthlyReturnsByBrokerAsync();

        // Assert
        matrix.Should().HaveCount(1);
        var row = matrix[0];
        row.PortfolioId.Should().Be(portfolioId);
        row.Name.Should().Be("Combined");
        row.MemberCount.Should().Be(2);
        row.Returns.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_BrokerFilter_ReturnsOnlyMatchingBroker()
    {
        // Arrange
        var pDarwinex = Guid.NewGuid();
        var pFtmo = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Portfolios.AddRange(
            MakePortfolio(pDarwinex, "DW Portfolio", "Darwinex", 50_000m, d),
            MakePortfolio(pFtmo, "FTMO Portfolio", "FTMO", 50_000m, d));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetMonthlyReturnsByBrokerAsync(broker: "FTMO");

        // Assert
        result.Should().HaveCount(1);
        result[0].PortfolioId.Should().Be(pFtmo);
        result[0].Name.Should().Be("FTMO Portfolio");
    }

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_MultiplePortfolios_OrderedByCreatedAtDesc()
    {
        // Arrange
        var pOld = Guid.NewGuid();
        var pMid = Guid.NewGuid();
        var pNew = Guid.NewGuid();

        await using var db = InMemoryDbContextFactory.Create();
        db.Portfolios.AddRange(
            MakePortfolio(pOld, "Old", "Darwinex", 10_000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakePortfolio(pNew, "New", "Darwinex", 10_000m, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakePortfolio(pMid, "Mid", "Darwinex", 10_000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetMonthlyReturnsByBrokerAsync();

        // Assert — same ordering as the summaries grid, so rows line up between views.
        result.Select(r => r.PortfolioId).Should().ContainInOrder(pNew, pMid, pOld);
    }

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_EmptyPortfolio_ReturnsRowWithNoMonths()
    {
        // Arrange — a portfolio with no member strategies still gets a row, just an empty series.
        var portfolioId = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Portfolios.Add(MakePortfolio(portfolioId, "Empty", "Axi", 20_000m, d));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetMonthlyReturnsByBrokerAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].MemberCount.Should().Be(0);
        result[0].Returns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyReturnsByBrokerAsync_SharedStrategy_ComputesPerPortfolioCapital()
    {
        // Arrange — the same strategy in two portfolios with different initial capital:
        // the bulk trade load must not leak one portfolio's series into the other.
        var sharedStratId = Guid.NewGuid();
        var pBig = Guid.NewGuid();
        var pSmall = Guid.NewGuid();
        var d = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(MakeStrategy(sharedStratId, "Shared"));
        db.StrategyTrades.Add(Trade(sharedStratId, 10, d, d.AddHours(1), 1_000m));
        db.Portfolios.AddRange(
            MakePortfolio(pBig, "Big", "Darwinex", 100_000m, d.AddHours(1), sharedStratId),
            MakePortfolio(pSmall, "Small", "Darwinex", 10_000m, d, sharedStratId));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetMonthlyReturnsByBrokerAsync();

        // Assert — same profit, different base capital → the smaller portfolio returns 10x more.
        var big = result.Single(r => r.PortfolioId == pBig);
        var small = result.Single(r => r.PortfolioId == pSmall);

        big.Returns.Should().ContainSingle();
        small.Returns.Should().ContainSingle();
        big.Returns[0].Profit.Should().Be(1_000m);
        small.Returns[0].Profit.Should().Be(1_000m);
        big.Returns[0].ReturnPercent.Should().BeApproximately(0.01m, 0.0001m);
        small.Returns[0].ReturnPercent.Should().BeApproximately(0.10m, 0.0001m);
    }
}
