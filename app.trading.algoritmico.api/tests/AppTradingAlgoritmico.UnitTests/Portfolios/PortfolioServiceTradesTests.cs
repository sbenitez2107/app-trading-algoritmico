using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Unit tests for <see cref="PortfolioService.GetTradesAsync"/> — combined member trades.
/// Uses the EF InMemory provider. Verifies combination across members, ordering, status
/// filtering, pagination, strategy-name projection, and not-found / empty edge cases.
/// </summary>
public class PortfolioServiceTradesTests
{
    private static readonly Guid StrategyAId = Guid.NewGuid();
    private static readonly Guid StrategyBId = Guid.NewGuid();
    private static readonly Guid PortfolioId = Guid.NewGuid();

    /// <summary>
    /// Seeds a portfolio with two member strategies, each with a mix of open and closed trades.
    /// Returns the live context — caller disposes.
    /// </summary>
    private static AppDbContext SeedContext()
    {
        var db = InMemoryDbContextFactory.Create();

        var strategyA = new Strategy { Id = StrategyAId, Name = "Strategy A" };
        var strategyB = new Strategy { Id = StrategyBId, Name = "Strategy B" };
        db.Strategies.AddRange(strategyA, strategyB);

        // Strategy A: 1 open + 2 closed.
        db.StrategyTrades.AddRange(
            MakeTrade(StrategyAId, ticket: 1, isOpen: false,
                openTime: new DateTime(2026, 1, 1), closeTime: new DateTime(2026, 1, 2)),
            MakeTrade(StrategyAId, ticket: 2, isOpen: false,
                openTime: new DateTime(2026, 1, 3), closeTime: new DateTime(2026, 1, 5)),
            MakeTrade(StrategyAId, ticket: 3, isOpen: true,
                openTime: new DateTime(2026, 2, 1), closeTime: null));

        // Strategy B: 1 open + 1 closed.
        db.StrategyTrades.AddRange(
            MakeTrade(StrategyBId, ticket: 4, isOpen: false,
                openTime: new DateTime(2026, 1, 4), closeTime: new DateTime(2026, 1, 6)),
            MakeTrade(StrategyBId, ticket: 5, isOpen: true,
                openTime: new DateTime(2026, 2, 3), closeTime: null));

        var portfolio = new Portfolio
        {
            Id = PortfolioId,
            Name = "Combined",
            Broker = "Darwinex",
            InitialCapital = 100_000m,
            Members =
            {
                new PortfolioStrategy { StrategyId = StrategyAId, Weight = 1m },
                new PortfolioStrategy { StrategyId = StrategyBId, Weight = 1m }
            }
        };
        db.Portfolios.Add(portfolio);

        db.SaveChanges();
        return db;
    }

    private static StrategyTrade MakeTrade(
        Guid strategyId, long ticket, bool isOpen, DateTime openTime, DateTime? closeTime) => new()
        {
            StrategyId = strategyId,
            Ticket = ticket,
            OpenTime = openTime,
            CloseTime = closeTime,
            Type = "buy",
            Size = 0.10m,
            Item = "EURUSD",
            OpenPrice = 1.1000m,
            ClosePrice = isOpen ? null : 1.1050m,
            StopLoss = 1.0950m,
            TakeProfit = 1.1100m,
            Commission = -1m,
            Taxes = 0m,
            Swap = -0.5m,
            Profit = isOpen ? 0m : 50m,
            CloseReason = isOpen ? null : "TP",
            IsOpen = isOpen
        };

    [Fact]
    public async Task GetTradesAsync_AllStatus_CombinesTradesFromBothMembers()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.All, page: 1, pageSize: 50);

        // Assert — 3 from A + 2 from B
        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(5);
        result.Items.Select(t => t.StrategyId).Distinct()
            .Should().BeEquivalentTo(new[] { StrategyAId, StrategyBId });
    }

    [Fact]
    public async Task GetTradesAsync_AllStatus_OrdersIsOpenDescThenCloseTimeDescThenOpenTimeDesc()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.All, page: 1, pageSize: 50);

        // Assert — open trades first (IsOpen DESC). Among opens, OpenTime DESC (CloseTime null ties).
        // Open: ticket 5 (2026-02-03), ticket 3 (2026-02-01).
        // Closed: ticket 4 (close 2026-01-06), ticket 2 (close 2026-01-05), ticket 1 (close 2026-01-02).
        result.Items.Select(t => t.Ticket).Should().ContainInOrder(5L, 3L, 4L, 2L, 1L);
    }

    [Fact]
    public async Task GetTradesAsync_OpenStatus_ReturnsOnlyOpenTrades()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.Open, page: 1, pageSize: 50);

        // Assert — ticket 3 (A) + ticket 5 (B)
        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(t => t.IsOpen);
        result.Items.Select(t => t.Ticket).Should().BeEquivalentTo(new[] { 3L, 5L });
    }

    [Fact]
    public async Task GetTradesAsync_ClosedStatus_ReturnsOnlyClosedTrades()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.Closed, page: 1, pageSize: 50);

        // Assert — tickets 1, 2 (A) + ticket 4 (B)
        result.TotalCount.Should().Be(3);
        result.Items.Should().OnlyContain(t => !t.IsOpen);
        result.Items.Select(t => t.Ticket).Should().BeEquivalentTo(new[] { 1L, 2L, 4L });
    }

    [Fact]
    public async Task GetTradesAsync_Paginated_ReturnsRequestedPageButFullTotalCount()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act — page 2 with size 2 → third+... rows in canonical order (5,3,4,2,1)
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.All, page: 2, pageSize: 2);

        // Assert
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(t => t.Ticket).Should().ContainInOrder(4L, 2L);
    }

    [Fact]
    public async Task GetTradesAsync_EachDto_HasStrategyNameMatchingItsSourceStrategy()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(PortfolioId, TradeStatusFilter.All, page: 1, pageSize: 50);

        // Assert
        result.Items.Should().OnlyContain(t =>
            (t.StrategyId == StrategyAId && t.StrategyName == "Strategy A") ||
            (t.StrategyId == StrategyBId && t.StrategyName == "Strategy B"));
    }

    [Fact]
    public async Task GetTradesAsync_UnknownPortfolio_ThrowsKeyNotFoundException()
    {
        // Arrange
        await using var db = SeedContext();
        var sut = new PortfolioService(db);

        // Act
        var act = async () => await sut.GetTradesAsync(Guid.NewGuid(), TradeStatusFilter.All, 1, 50);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetTradesAsync_EmptyPortfolio_ReturnsEmptyPagedResult()
    {
        // Arrange
        await using var db = InMemoryDbContextFactory.Create();
        var emptyPortfolioId = Guid.NewGuid();
        db.Portfolios.Add(new Portfolio
        {
            Id = emptyPortfolioId,
            Name = "Empty",
            Broker = "Darwinex",
            InitialCapital = 100_000m
        });
        await db.SaveChangesAsync();
        var sut = new PortfolioService(db);

        // Act
        var result = await sut.GetTradesAsync(emptyPortfolioId, TradeStatusFilter.All, page: 1, pageSize: 50);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
    }
}
