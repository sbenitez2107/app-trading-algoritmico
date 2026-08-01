using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Unit tests for <see cref="PortfolioService.GetSummariesAsync"/>.
/// Uses the EF InMemory provider (same harness as PortfolioServiceTradesTests).
/// Verifies KPI mapping from <see cref="PortfolioAnalyticsCalculator.Compute"/>, broker filter,
/// CreatedAt-DESC ordering, and the single-query / no-N+1 trade load strategy.
/// </summary>
public class PortfolioServiceSummariesTests
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

    private static TradingAccount MakeAccount(Guid id, string broker) =>
        new()
        {
            Id = id,
            Name = broker + "-Account",
            Broker = broker,
            AccountType = AccountType.Live,
            Platform = PlatformType.MT5,
            AccountNumber = 123456L,
            Login = 123456L,
            PasswordEncrypted = "n/a",
            Server = "server"
        };

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
    public async Task GetSummariesAsync_NoPortfolios_ReturnsEmptyList()
    {
        // Arrange
        await using var db = InMemoryDbContextFactory.Create();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummariesAsync_SinglePortfolioTwoMembers_MapsKpisFromCompute()
    {
        // Arrange — two strategies, known trades, so we can assert exact computed KPIs.
        var stratA = Guid.NewGuid();
        var stratB = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();

        var d = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.AddRange(
            MakeStrategy(stratA, "A"),
            MakeStrategy(stratB, "B"));
        db.StrategyTrades.AddRange(
            Trade(stratA, 1, d, d.AddHours(1), 500m),          // win
            Trade(stratA, 2, d.AddDays(1), d.AddDays(1).AddHours(1), -200m), // loss
            Trade(stratB, 3, d.AddDays(2), d.AddDays(2).AddHours(1), 300m));  // win
        db.Portfolios.Add(MakePortfolio(portfolioId, "MyPortfolio", "Darwinex", 100_000m, d, stratA, stratB));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Pre-compute expected KPIs with the calculator directly (pure, no DB).
        var expectedKpis = PortfolioAnalyticsCalculator.Compute(100_000m, [
            new PortfolioMemberInput(stratA, "A", 1m, [
                Trade(stratA, 1, d, d.AddHours(1), 500m),
                Trade(stratA, 2, d.AddDays(1), d.AddDays(1).AddHours(1), -200m)]),
            new PortfolioMemberInput(stratB, "B", 1m, [
                Trade(stratB, 3, d.AddDays(2), d.AddDays(2).AddHours(1), 300m)])
        ]);

        // Act
        var summaries = await sut.GetSummariesAsync();

        // Assert — header fields
        summaries.Should().HaveCount(1);
        var s = summaries[0];
        s.Id.Should().Be(portfolioId);
        s.Name.Should().Be("MyPortfolio");
        s.Broker.Should().Be("Darwinex");
        s.AccountType.Should().Be(AccountType.Live);
        s.InitialCapital.Should().Be(100_000m);
        s.BaseCurrency.Should().Be("USD");
        s.MemberCount.Should().Be(2);
        s.CreatedAt.Should().Be(d);

        // Assert — KPI fields match the calculator output exactly.
        s.FinalEquity.Should().Be(expectedKpis.FinalEquity);
        s.NetProfit.Should().Be(expectedKpis.NetProfit, "500 - 200 + 300 = 600");
        s.TradeCount.Should().Be(expectedKpis.TradeCount);
        s.WinCount.Should().Be(expectedKpis.WinCount);
        s.LossCount.Should().Be(expectedKpis.LossCount);
        s.WinRate.Should().BeApproximately(expectedKpis.WinRate, 0.0001m);
        s.TotalReturn.Should().BeApproximately(expectedKpis.TotalReturn, 0.0001m);
        s.MaxDrawdownPercent.Should().BeApproximately(expectedKpis.MaxDrawdownPercent, 0.0001m);
        s.ProfitFactor.Should().BeApproximately(expectedKpis.ProfitFactor, 0.0001m);
        s.SharpeRatio.Should().BeApproximately(expectedKpis.SharpeRatio, 0.0001m);
        s.Sqn.Should().BeApproximately(expectedKpis.Sqn, 0.0001m);
        s.MonthlyAvgProfit.Should().BeApproximately(expectedKpis.MonthlyAvgProfit, 0.01m);
        s.DailyAvgProfit.Should().BeApproximately(expectedKpis.DailyAvgProfit, 0.01m);
        s.ReturnDrawdownRatio.Should().BeApproximately(expectedKpis.ReturnDrawdownRatio, 0.0001m);
        s.Cagr.Should().BeApproximately(expectedKpis.Cagr, 0.0001m);
    }

    [Fact]
    public async Task GetSummariesAsync_MultiplePortfolios_OrderedByCreatedAtDesc()
    {
        // Arrange — three portfolios with different CreatedAt values.
        var pOld = Guid.NewGuid();
        var pMid = Guid.NewGuid();
        var pNew = Guid.NewGuid();

        var base1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var base2 = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var base3 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Portfolios.AddRange(
            MakePortfolio(pOld, "Old", "Darwinex", 10_000m, base1),
            MakePortfolio(pNew, "New", "Darwinex", 10_000m, base3),
            MakePortfolio(pMid, "Mid", "Darwinex", 10_000m, base2));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert — newest first (CreatedAt DESC)
        result.Should().HaveCount(3);
        result.Select(s => s.Id).Should().ContainInOrder(pNew, pMid, pOld);
    }

    [Fact]
    public async Task GetSummariesAsync_BrokerFilter_ReturnsOnlyMatchingBroker()
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
        var result = await sut.GetSummariesAsync(broker: "FTMO");

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(pFtmo);
        result[0].Broker.Should().Be("FTMO");
    }

    [Fact]
    public async Task GetSummariesAsync_EmptyPortfolio_ReturnsZeroKpis()
    {
        // Arrange — portfolio with no member strategies.
        var portfolioId = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Portfolios.Add(MakePortfolio(portfolioId, "Empty", "Axi", 20_000m, d));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert — header is present, all KPIs are zeroed out.
        result.Should().HaveCount(1);
        var s = result[0];
        s.TradeCount.Should().Be(0);
        s.NetProfit.Should().Be(0m);
        s.FinalEquity.Should().Be(20_000m, "no trades → final equity equals initial capital");
        s.WinRate.Should().Be(0m);
        s.MaxDrawdownPercent.Should().Be(0m);
    }

    [Fact]
    public async Task GetSummariesAsync_TradesLoadedInBulk_NoPer_PortfolioQuery()
    {
        // This test proves the mapping works correctly when the same strategy appears
        // as a member in two different portfolios — the bulk trade-load groups correctly
        // without duplicating or losing trades per-portfolio.
        var sharedStratId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var d = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

        await using var db = InMemoryDbContextFactory.Create();
        db.Strategies.Add(MakeStrategy(sharedStratId, "Shared"));
        db.StrategyTrades.AddRange(
            Trade(sharedStratId, 10, d, d.AddHours(1), 1000m),
            Trade(sharedStratId, 11, d.AddDays(1), d.AddDays(1).AddHours(1), -400m));
        db.Portfolios.AddRange(
            MakePortfolio(p1, "P1", "Darwinex", 100_000m, d, sharedStratId),
            MakePortfolio(p2, "P2", "Darwinex", 50_000m, d.AddHours(1), sharedStratId));
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert — both portfolios resolve the shared strategy's net correctly (600).
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.NetProfit == 600m,
            "both portfolios combine the same strategy's two trades at weight 1: 1000 - 400 = 600");
    }
}
