using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for `StrategyService.GetByAccountAsync` — verifies that the live KPIs
/// (NetProfit, WinRate, ProfitFactor, MaxDD%, Return/DD, Sharpe, TotalReturn) are
/// populated from MT4 trades for each strategy, or null when no trades exist.
/// </summary>
public class StrategyServiceLiveKpisTests
{
    private static StrategyService CreateSut(
        AppTradingAlgoritmico.Infrastructure.Persistence.AppDbContext db) =>
        new(db, Mock.Of<ISqxParserService>(), Mock.Of<IHtmlReportParserService>());

    private static (Guid accountId, Guid stratWithTrades, Guid stratWithoutTrades) Seed(string dbName)
    {
        var accountId = Guid.NewGuid();
        var stratA = Guid.NewGuid();
        var stratB = Guid.NewGuid();

        using var db = InMemoryDbContextFactory.Create(dbName);
        db.TradingAccounts.Add(new TradingAccount
        {
            Id = accountId,
            Name = "Acc",
            Broker = "Darwinex",
            AccountType = AccountType.Demo,
            Platform = PlatformType.MT4,
            AccountNumber = 1,
            Login = 1,
            PasswordEncrypted = "e",
            Server = "s",
            IsEnabled = true,
            InitialBalance = 100_000m,
            CreatedAt = DateTime.UtcNow,
        });
        db.Strategies.AddRange(
            new Strategy { Id = stratA, Name = "A_HasTrades", TradingAccountId = accountId, MagicNumber = 100 },
            new Strategy { Id = stratB, Name = "B_NoTrades", TradingAccountId = accountId, MagicNumber = 200 });

        // 4 trades on A: 3 wins (+50, +30, +100), 1 loss (-40). Costs zero.
        var d = new DateTime(2026, 4, 1);
        StrategyTrade Trade(long ticket, DateTime open, DateTime close, decimal profit) => new()
        {
            Id = Guid.NewGuid(),
            StrategyId = stratA,
            Ticket = ticket,
            OpenTime = open,
            CloseTime = close,
            Type = "buy",
            Size = 0.01m,
            Item = "ndx",
            OpenPrice = 100m,
            ClosePrice = 101m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = 0m,
            Taxes = 0m,
            Swap = 0m,
            Profit = profit,
            IsOpen = false,
        };

        db.StrategyTrades.AddRange(
            Trade(1, d, d.AddHours(1), 50m),
            Trade(2, d.AddDays(1), d.AddDays(1).AddHours(1), 30m),
            Trade(3, d.AddDays(2), d.AddDays(2).AddHours(1), -40m),
            Trade(4, d.AddDays(3), d.AddDays(3).AddHours(1), 100m));

        db.SaveChanges();
        return (accountId, stratA, stratB);
    }

    [Fact]
    public async Task GetByAccountAsync_StrategyWithTrades_PopulatesLiveKpis()
    {
        var dbName = Guid.NewGuid().ToString();
        var (accountId, stratA, _) = Seed(dbName);

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        var result = await sut.GetByAccountAsync(accountId);

        var a = result.Items.Single(s => s.Id == stratA);
        a.LiveTradeCount.Should().Be(4);
        a.LiveNetProfit.Should().Be(140m, "50 + 30 - 40 + 100");
        a.LiveWinRate.Should().BeApproximately(0.75m, 0.0001m);
        a.LiveProfitFactor.Should().BeApproximately(4.5m, 0.0001m, "180 / 40");
        a.LiveTotalReturn.Should().BeApproximately(0.0014m, 0.0001m, "140 / 100k");
        a.LiveMaxDrawdownPercent.Should().NotBeNull();
        a.LiveReturnDrawdownRatio.Should().NotBeNull();
        a.LiveSharpeRatio.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByAccountAsync_StrategyWithoutTrades_LeavesLiveKpisNull()
    {
        var dbName = Guid.NewGuid().ToString();
        var (accountId, _, stratB) = Seed(dbName);

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sut = CreateSut(db);

        var result = await sut.GetByAccountAsync(accountId);

        var b = result.Items.Single(s => s.Id == stratB);
        b.LiveTradeCount.Should().Be(0);
        b.LiveNetProfit.Should().BeNull();
        b.LiveWinRate.Should().BeNull();
        b.LiveProfitFactor.Should().BeNull();
        b.LiveMaxDrawdownPercent.Should().BeNull();
        b.LiveReturnDrawdownRatio.Should().BeNull();
        b.LiveSharpeRatio.Should().BeNull();
        b.LiveTotalReturn.Should().BeNull();
    }
}
