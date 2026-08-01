using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Unit tests for <see cref="StrategyService.GetMonthlyReturnsByAccountAsync"/>.
/// Uses the EF InMemory provider (same harness as StrategyServiceGetByAccountTests).
/// Verifies per-strategy monthly compounding returns against the account's initial
/// balance, name ordering, empty-returns rows for strategies without trades, and
/// the account-not-found guard.
/// </summary>
public class StrategyServiceMonthlyReturnsByAccountTests
{
    // -------------------------------------------------------------------------
    // Builders
    // -------------------------------------------------------------------------

    private static TradingAccount MakeAccount(Guid id, decimal initialBalance = 10_000m) =>
        new()
        {
            Id = id,
            Name = "Demo-Account",
            Broker = "Darwinex",
            AccountType = AccountType.Demo,
            Platform = PlatformType.MT4,
            AccountNumber = 123456L,
            Login = 123456L,
            PasswordEncrypted = "n/a",
            Server = "server",
            InitialBalance = initialBalance
        };

    private static Strategy MakeStrategy(Guid id, string name, Guid accountId, string? symbol = "EURUSD") =>
        new() { Id = id, Name = name, TradingAccountId = accountId, Symbol = symbol };

    private static StrategyTrade Trade(
        Guid strategyId,
        long ticket,
        DateTime close,
        decimal profit,
        bool isOpen = false) => new()
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Ticket = ticket,
            OpenTime = close.AddHours(-2),
            CloseTime = isOpen ? null : close,
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
            IsOpen = isOpen
        };

    private static StrategyService CreateSut(Infrastructure.Persistence.AppDbContext db) =>
        new(db, new Mock<ISqxParserService>().Object, new Mock<IHtmlReportParserService>().Object);

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_AccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var db = InMemoryDbContextFactory.Create();
        var sut = CreateSut(db);

        // Act
        var act = () => sut.GetMonthlyReturnsByAccountAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_NoStrategies_ReturnsEmptyList()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        await using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.TradingAccounts.Add(MakeAccount(accountId));
            await seedDb.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        // Act
        var result = await sut.GetMonthlyReturnsByAccountAsync(accountId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_StrategyWithTrades_ComputesCompoundingMonthlyReturns()
    {
        // Arrange — one strategy, 2 trades closed in Jan and 1 in Feb 2026.
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        await using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.TradingAccounts.Add(MakeAccount(accountId, initialBalance: 10_000m));
            seedDb.Strategies.Add(MakeStrategy(strategyId, "Alpha", accountId));
            seedDb.StrategyTrades.AddRange(
                Trade(strategyId, 1, new DateTime(2026, 1, 10), 100m),
                Trade(strategyId, 2, new DateTime(2026, 1, 20), 50m),
                Trade(strategyId, 3, new DateTime(2026, 2, 5), -30m));
            await seedDb.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        // Act
        var result = await sut.GetMonthlyReturnsByAccountAsync(accountId);

        // Assert
        result.Should().HaveCount(1);
        var row = result[0];
        row.StrategyId.Should().Be(strategyId);
        row.Name.Should().Be("Alpha");
        row.Symbol.Should().Be("EURUSD");
        row.Returns.Should().HaveCount(2);

        var jan = row.Returns[0];
        jan.Year.Should().Be(2026);
        jan.Month.Should().Be(1);
        jan.EquityStart.Should().Be(10_000m);
        jan.Profit.Should().Be(150m);
        jan.ReturnPercent.Should().Be(0.015m);
        jan.TradeCount.Should().Be(2);

        var feb = row.Returns[1];
        feb.Month.Should().Be(2);
        feb.EquityStart.Should().Be(10_150m);
        feb.Profit.Should().Be(-30m);
        feb.ReturnPercent.Should().BeApproximately(-30m / 10_150m, 0.000001m);
    }

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_StrategyWithoutTrades_ReturnsRowWithEmptyReturns()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var withTrades = Guid.NewGuid();
        var withoutTrades = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        await using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.TradingAccounts.Add(MakeAccount(accountId));
            seedDb.Strategies.AddRange(
                MakeStrategy(withTrades, "Alpha", accountId),
                MakeStrategy(withoutTrades, "Beta", accountId, symbol: null));
            seedDb.StrategyTrades.Add(Trade(withTrades, 1, new DateTime(2026, 3, 1), 25m));
            await seedDb.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        // Act
        var result = await sut.GetMonthlyReturnsByAccountAsync(accountId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Returns.Should().HaveCount(1);
        result[1].Name.Should().Be("Beta");
        result[1].Symbol.Should().BeNull();
        result[1].Returns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_MultipleStrategies_OrdersByName()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        await using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.TradingAccounts.Add(MakeAccount(accountId));
            seedDb.Strategies.AddRange(
                MakeStrategy(Guid.NewGuid(), "Zeta", accountId),
                MakeStrategy(Guid.NewGuid(), "Alpha", accountId),
                MakeStrategy(Guid.NewGuid(), "Mid", accountId));
            await seedDb.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        // Act
        var result = await sut.GetMonthlyReturnsByAccountAsync(accountId);

        // Assert
        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Mid", "Zeta");
    }

    [Fact]
    public async Task GetMonthlyReturnsByAccountAsync_OpenTrades_AreExcludedFromReturns()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        await using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.TradingAccounts.Add(MakeAccount(accountId));
            seedDb.Strategies.Add(MakeStrategy(strategyId, "Alpha", accountId));
            seedDb.StrategyTrades.AddRange(
                Trade(strategyId, 1, new DateTime(2026, 4, 10), 80m),
                Trade(strategyId, 2, new DateTime(2026, 4, 15), 999m, isOpen: true));
            await seedDb.SaveChangesAsync();
        }

        await using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        // Act
        var result = await sut.GetMonthlyReturnsByAccountAsync(accountId);

        // Assert — only the closed trade counts.
        result[0].Returns.Should().HaveCount(1);
        result[0].Returns[0].Profit.Should().Be(80m);
        result[0].Returns[0].TradeCount.Should().Be(1);
    }
}
