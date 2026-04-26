using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Unit tests for TradeImportService — R1, R8, R9, R10, R12.
/// Uses EF InMemory via InMemoryDbContextFactory and Moq for IMtStatementParserService.
/// TDD: written before TradeImportService.cs exists (RED phase).
/// </summary>
public class TradeImportServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ParsedMtStatementDto MakeStatement(
        ParsedSummaryDto? summary = null,
        params ParsedMtTradeDto[] trades)
    {
        summary ??= MakeSummary();
        return new ParsedMtStatementDto(trades.ToList().AsReadOnly(), summary);
    }

    private static ParsedSummaryDto MakeSummary(string currency = "USD") =>
        new(
            ReportTime: new DateTime(2026, 4, 21, 7, 6, 0),
            Balance: 100_000m,
            Equity: 100_500m,
            FloatingPnL: 500m,
            Margin: 1_000m,
            FreeMargin: 99_000m,
            ClosedTradePnL: 2_897.15m,
            Currency: currency);

    private static ParsedMtTradeDto MakeTrade(
        long ticket,
        int magicNumber,
        string strategyNameHint = "Strategy",
        bool isOpen = false) =>
        new(
            Ticket: ticket,
            MagicNumber: magicNumber,
            StrategyNameHint: strategyNameHint,
            CloseReason: isOpen ? null : "SL",
            OpenTime: new DateTime(2026, 4, 20, 10, 0, 0),
            CloseTime: isOpen ? null : new DateTime(2026, 4, 20, 12, 0, 0),
            Type: "buy",
            Size: 0.01m,
            Item: "xauusd",
            OpenPrice: 4_000m,
            ClosePrice: isOpen ? null : 4_050m,
            StopLoss: 3_900m,
            TakeProfit: 4_100m,
            Commission: -0.10m,
            Taxes: 0m,
            Swap: 0m,
            Profit: 50m,
            IsOpen: isOpen);

    private static TradingAccount MakeAccount(Guid id, string? currency = null) =>
        new()
        {
            Id = id,
            Name = "TestAccount",
            Broker = "Darwinex",
            AccountType = AccountType.Demo,
            Platform = PlatformType.MT4,
            AccountNumber = 2089130867,
            Login = 2089130867,
            PasswordEncrypted = "enc",
            Server = "Darwinex-Demo",
            IsEnabled = true,
            Currency = currency
        };

    private static Strategy MakeStrategy(Guid id, Guid accountId, int? magicNumber) =>
        new()
        {
            Id = id,
            Name = $"Strategy_{magicNumber}",
            TradingAccountId = accountId,
            MagicNumber = magicNumber
        };

    private static TradeImportService CreateSut(
        out AppTradingAlgoritmico.Infrastructure.Persistence.AppDbContext db,
        out Mock<IMtStatementParserService> parserMock)
    {
        db = InMemoryDbContextFactory.Create();
        parserMock = new Mock<IMtStatementParserService>();
        return new TradeImportService(db, parserMock.Object);
    }

    // -------------------------------------------------------------------------
    // R9 / Test 1: First import — inserts N rows, Imported=N, Updated=0
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_FirstImport_InsertsAllMatchedTrades()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int magic = 1111;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, magic));
        await db.SaveChangesAsync();

        var trades = new[] { MakeTrade(1001, magic), MakeTrade(1002, magic) };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(2);
        result.Updated.Should().Be(0);
        db.StrategyTrades.Count().Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // R9 / Test 2: Re-import same trades — updates, row count unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_ReImport_UpdatesExistingRows()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int magic = 2222;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, magic));
        await db.SaveChangesAsync();

        var trades = new[] { MakeTrade(2001, magic), MakeTrade(2002, magic) };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // First import
        await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Second import (same file / same tickets)
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(0);
        result.Updated.Should().Be(2);
        db.StrategyTrades.Count().Should().Be(2, "row count must not grow on re-import");
    }

    // -------------------------------------------------------------------------
    // R8 / Test 3: One magic matches strategy, one does not → one orphan
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_UnknownMagic_ProducesOrphanEntry()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int knownMagic = 3001;
        const int orphanMagic = 9999;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, knownMagic));
        await db.SaveChangesAsync();

        var trades = new[]
        {
            MakeTrade(3001, knownMagic),
            MakeTrade(3002, orphanMagic, "OrphanStrategy"),
            MakeTrade(3003, orphanMagic, "OrphanStrategy"),
        };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(1);
        result.Orphans.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                MagicNumber = orphanMagic,
                StrategyNameHint = "OrphanStrategy",
                TradeCount = 2
            });
    }

    // -------------------------------------------------------------------------
    // R8 / Test 4: All strategies have null MagicNumber → all trades are orphans
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_AllNullMagicStrategies_AllTradesAreOrphans()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, magicNumber: null));
        await db.SaveChangesAsync();

        var trades = new[]
        {
            MakeTrade(4001, 1111),
            MakeTrade(4002, 2222),
            MakeTrade(4003, 2222),
        };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Orphans.Should().HaveCount(2, "two distinct magic numbers");
        db.StrategyTrades.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // R10 / Test 5: Snapshot always written — one row per import call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_AlwaysWritesOneSnapshot()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        await db.SaveChangesAsync();

        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement()); // no trades, only summary

        // Act
        await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);
        await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert — 2 calls → 2 snapshots
        db.AccountEquitySnapshots.Count().Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // R1 / Test 6: Non-existent TradingAccountId → KeyNotFoundException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_NonExistentAccount_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = CreateSut(out _, out var parserMock);
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement());

        // Act
        var act = async () => await sut.ImportAsync(Guid.NewGuid(), Stream.Null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // -------------------------------------------------------------------------
    // R1 / Test 7: Parser returns null → ArgumentException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_ParserReturnsNull_ThrowsArgumentException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        await db.SaveChangesAsync();

        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParsedMtStatementDto?)null);

        // Act
        var act = async () => await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // R12 / Test 8: GetByStrategyAsync — Open filter returns only IsOpen=true
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByStrategyAsync_OpenFilter_ReturnsOnlyOpenTrades()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var sut = CreateSut(out var db, out _);

        db.StrategyTrades.AddRange(
            new StrategyTrade { StrategyId = strategyId, Ticket = 8001, IsOpen = true, OpenTime = DateTime.UtcNow, Type = "buy", Item = "xauusd", Size = 0.01m, OpenPrice = 4000m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 0m },
            new StrategyTrade { StrategyId = strategyId, Ticket = 8002, IsOpen = false, OpenTime = DateTime.UtcNow, CloseTime = DateTime.UtcNow.AddHours(-1), ClosePrice = 4050m, Type = "buy", Item = "xauusd", Size = 0.01m, OpenPrice = 4000m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 50m });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetByStrategyAsync(strategyId, TradeStatusFilter.Open, 1, 10, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.All(t => t.IsOpen).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // R12 / Test 9: GetByStrategyAsync — Closed filter returns only IsOpen=false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByStrategyAsync_ClosedFilter_ReturnsOnlyClosedTrades()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var sut = CreateSut(out var db, out _);

        db.StrategyTrades.AddRange(
            new StrategyTrade { StrategyId = strategyId, Ticket = 9001, IsOpen = true, OpenTime = DateTime.UtcNow, Type = "buy", Item = "xauusd", Size = 0.01m, OpenPrice = 4000m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 0m },
            new StrategyTrade { StrategyId = strategyId, Ticket = 9002, IsOpen = false, OpenTime = DateTime.UtcNow, CloseTime = DateTime.UtcNow.AddHours(-1), ClosePrice = 4050m, Type = "buy", Item = "xauusd", Size = 0.01m, OpenPrice = 4000m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 50m },
            new StrategyTrade { StrategyId = strategyId, Ticket = 9003, IsOpen = false, OpenTime = DateTime.UtcNow.AddHours(-2), CloseTime = DateTime.UtcNow.AddMinutes(-30), ClosePrice = 4060m, Type = "sell", Item = "xauusd", Size = 0.01m, OpenPrice = 4100m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = -40m });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetByStrategyAsync(strategyId, TradeStatusFilter.Closed, 1, 10, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.All(t => !t.IsOpen).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // R12 / Test 10: GetByStrategyAsync — Open trades appear before closed (null CloseTime sorts first)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByStrategyAsync_AllFilter_OpenTradesAppearBeforeClosed()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var sut = CreateSut(out var db, out _);

        var closedAt = new DateTime(2026, 4, 20, 12, 0, 0);
        var openedAt = new DateTime(2026, 4, 21, 8, 0, 0);

        db.StrategyTrades.AddRange(
            new StrategyTrade { StrategyId = strategyId, Ticket = 10001, IsOpen = false, OpenTime = new DateTime(2026, 4, 20, 10, 0, 0), CloseTime = closedAt, ClosePrice = 4050m, Type = "buy", Item = "xauusd", Size = 0.01m, OpenPrice = 4000m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 50m },
            new StrategyTrade { StrategyId = strategyId, Ticket = 10002, IsOpen = true, OpenTime = openedAt, CloseTime = null, Type = "buy", Item = "ndx", Size = 0.08m, OpenPrice = 26500m, StopLoss = 0m, TakeProfit = 0m, Commission = 0m, Taxes = 0m, Swap = 0m, Profit = 0m });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetByStrategyAsync(strategyId, TradeStatusFilter.All, 1, 10, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items[0].Ticket.Should().Be(10002, "open trade (null CloseTime) must appear first");
        result.Items[1].Ticket.Should().Be(10001, "closed trade must appear after open");
    }

    // -------------------------------------------------------------------------
    // Locked decision: snapshot currency fallback
    // When parsed Summary.Currency is empty → snapshot.Currency is empty string
    // (TradingAccount has no Currency property; parser always provides it in practice)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_EmptyParsedCurrency_FallsBackToAccountCurrency()
    {
        // Arrange: parser returns empty currency; account has Currency = "EUR"
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId, currency: "EUR"));
        await db.SaveChangesAsync();

        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(summary: MakeSummary(currency: "")));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert — service falls back to TradingAccount.Currency when parser returns empty
        result.Snapshot.Currency.Should().Be("EUR");
        db.AccountEquitySnapshots.Single().Currency.Should().Be("EUR");
    }

    // -------------------------------------------------------------------------
    // Auto-assign by name / Test A: hint matches single strategy without magic →
    // strategy gets MagicNumber assigned, trades imported, AutoAssigned reported,
    // orphan list does NOT include the magic.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_HintMatchesSingleStrategyWithoutMagic_AutoAssignsAndImportsTrades()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int unknownMagic = 5050;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(new Strategy
        {
            Id = strategyId,
            Name = "Strategy 1.15.198",
            TradingAccountId = accountId,
            MagicNumber = null
        });
        await db.SaveChangesAsync();

        var trades = new[]
        {
            MakeTrade(5001, unknownMagic, "Strategy 1.15.198"),
            MakeTrade(5002, unknownMagic, "Strategy 1.15.198"),
        };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(2);
        result.Orphans.Should().BeEmpty("the orphan was auto-assigned");
        result.AutoAssigned.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                StrategyId = strategyId,
                StrategyName = "Strategy 1.15.198",
                MagicNumber = unknownMagic,
                TradeCount = 2
            });

        var refreshed = await db.Strategies.FindAsync(strategyId);
        refreshed!.MagicNumber.Should().Be(unknownMagic, "the magic must be persisted on the strategy");
        db.StrategyTrades.Count().Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Auto-assign / Test B: hint matches but strategy already has a different magic →
    // NEVER overwrite. Bucket stays as orphan, no auto-assign.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_HintMatchesStrategyWithExistingMagic_KeepsAsOrphan()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int existingMagic = 1111;
        const int statementMagic = 2222;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(new Strategy
        {
            Id = strategyId,
            Name = "WF_5_20_XAUUSD",
            TradingAccountId = accountId,
            MagicNumber = existingMagic
        });
        await db.SaveChangesAsync();

        var trades = new[] { MakeTrade(6001, statementMagic, "WF_5_20_XAUUSD") };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert — anti-destructive: do not overwrite an existing magic
        result.Imported.Should().Be(0);
        result.AutoAssigned.Should().BeEmpty();
        result.Orphans.Should().ContainSingle()
            .Which.MagicNumber.Should().Be(statementMagic);

        var refreshed = await db.Strategies.FindAsync(strategyId);
        refreshed!.MagicNumber.Should().Be(existingMagic, "existing magic must NOT be overwritten");
    }

    // -------------------------------------------------------------------------
    // Auto-assign / Test C: hint matches multiple strategies (ambiguous) →
    // keep as orphan, do not auto-assign any of them.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_HintMatchesMultipleStrategies_KeepsAsOrphan()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        const int unknownMagic = 7777;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.AddRange(
            new Strategy { Id = Guid.NewGuid(), Name = "DuplicateName", TradingAccountId = accountId, MagicNumber = null },
            new Strategy { Id = Guid.NewGuid(), Name = "DuplicateName", TradingAccountId = accountId, MagicNumber = null });
        await db.SaveChangesAsync();

        var trades = new[] { MakeTrade(7001, unknownMagic, "DuplicateName") };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(0);
        result.AutoAssigned.Should().BeEmpty("ambiguous match — multiple strategies share the name");
        result.Orphans.Should().ContainSingle()
            .Which.MagicNumber.Should().Be(unknownMagic);

        var allStrategies = db.Strategies.ToList();
        allStrategies.All(s => s.MagicNumber == null).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Auto-assign / Test D: name match is case-insensitive and ignores surrounding whitespace.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_HintMatchesCaseInsensitiveTrimmed_AutoAssigns()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int unknownMagic = 8080;

        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(new Strategy
        {
            Id = strategyId,
            Name = "Strategy 1.15.198",
            TradingAccountId = accountId,
            MagicNumber = null
        });
        await db.SaveChangesAsync();

        var trades = new[] { MakeTrade(8001, unknownMagic, "  strategy 1.15.198  ") };
        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(trades: trades));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert
        result.AutoAssigned.Should().ContainSingle()
            .Which.StrategyId.Should().Be(strategyId);
        result.Imported.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // GetSummaryByStrategyAsync — aggregates across all imported trades.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSummaryByStrategyAsync_StrategyWithoutTrades_ReturnsZeroes()
    {
        var sut = CreateSut(out _, out _);

        var summary = await sut.GetSummaryByStrategyAsync(Guid.NewGuid(), CancellationToken.None);

        summary.TradeCount.Should().Be(0);
        summary.ClosedCount.Should().Be(0);
        summary.WinRate.Should().Be(0m);
        summary.TotalProfit.Should().Be(0m);
        summary.NetProfit.Should().Be(0m);
    }

    [Fact]
    public async Task GetSummaryByStrategyAsync_MixedTrades_AggregatesCorrectly()
    {
        // Arrange — 3 wins (profit 50, 30, 100), 2 losses (-40, -20), 1 breakeven (0), 1 open
        var strategyId = Guid.NewGuid();
        var sut = CreateSut(out var db, out _);

        StrategyTrade Make(long ticket, decimal profit, decimal commission, decimal swap, decimal taxes, bool isOpen) => new()
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Ticket = ticket,
            OpenTime = new DateTime(2026, 4, 20, 10, 0, 0),
            CloseTime = isOpen ? null : new DateTime(2026, 4, 20, 12, 0, 0),
            Type = "buy",
            Size = 0.01m,
            Item = "ndx",
            OpenPrice = 100m,
            ClosePrice = isOpen ? null : 101m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = commission,
            Taxes = taxes,
            Swap = swap,
            Profit = profit,
            IsOpen = isOpen,
        };

        db.StrategyTrades.AddRange(
            Make(1, profit: 50m, commission: -0.5m, swap: 0m, taxes: 0m, isOpen: false),
            Make(2, profit: 30m, commission: -0.5m, swap: -1m, taxes: 0m, isOpen: false),
            Make(3, profit: 100m, commission: -0.5m, swap: 0m, taxes: -2m, isOpen: false),
            Make(4, profit: -40m, commission: -0.5m, swap: 0m, taxes: 0m, isOpen: false),
            Make(5, profit: -20m, commission: -0.5m, swap: 0m, taxes: 0m, isOpen: false),
            Make(6, profit: 0m, commission: -0.5m, swap: 0m, taxes: 0m, isOpen: false),
            Make(7, profit: 0m, commission: 0m, swap: 0m, taxes: 0m, isOpen: true)); // open trade
        await db.SaveChangesAsync();

        // Act
        var summary = await sut.GetSummaryByStrategyAsync(strategyId, CancellationToken.None);

        // Assert
        summary.TradeCount.Should().Be(7, "7 trades total (open + closed)");
        summary.ClosedCount.Should().Be(6);
        summary.WinCount.Should().Be(3, "wins: profit > 0 AND closed");
        summary.LossCount.Should().Be(2);
        summary.BreakevenCount.Should().Be(1);
        summary.WinRate.Should().BeApproximately(0.5m, 0.0001m, "3 wins / 6 closed");
        summary.TotalProfit.Should().Be(120m, "50 + 30 + 100 - 40 - 20 + 0 + 0");
        summary.TotalCommission.Should().Be(-3.0m, "6 closed × -0.5; the open trade has 0 commission");
        summary.TotalSwap.Should().Be(-1m);
        summary.TotalTaxes.Should().Be(-2m);
        summary.NetProfit.Should().Be(120m - 3.0m - 1m - 2m, "TotalProfit + Commission + Swap + Taxes");
    }

    [Fact]
    public async Task GetSummaryByStrategyAsync_OnlyOpenTrades_WinRateZero()
    {
        // Arrange — winRate denominator excludes open trades, so win rate must be 0
        var strategyId = Guid.NewGuid();
        var sut = CreateSut(out var db, out _);

        db.StrategyTrades.Add(new StrategyTrade
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            Ticket = 1,
            OpenTime = new DateTime(2026, 4, 20, 10, 0, 0),
            Type = "buy",
            Size = 0.01m,
            Item = "ndx",
            OpenPrice = 100m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = 0m,
            Taxes = 0m,
            Swap = 0m,
            Profit = 50m, // unrealised
            IsOpen = true,
        });
        await db.SaveChangesAsync();

        // Act
        var summary = await sut.GetSummaryByStrategyAsync(strategyId, CancellationToken.None);

        // Assert
        summary.TradeCount.Should().Be(1);
        summary.ClosedCount.Should().Be(0);
        summary.WinRate.Should().Be(0m, "no closed trades — denominator is 0, no division");
        summary.TotalProfit.Should().Be(50m);
    }

    [Fact]
    public async Task ImportAsync_NonEmptyParsedCurrency_PrefersParserOverAccount()
    {
        // Arrange: parser has "USD", account has "EUR" — parser wins
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var db, out var parserMock);

        db.TradingAccounts.Add(MakeAccount(accountId, currency: "EUR"));
        await db.SaveChangesAsync();

        parserMock
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStatement(summary: MakeSummary(currency: "USD")));

        // Act
        var result = await sut.ImportAsync(accountId, Stream.Null, CancellationToken.None);

        // Assert — parsed currency takes precedence
        result.Snapshot.Currency.Should().Be("USD");
        db.AccountEquitySnapshots.Single().Currency.Should().Be("USD");
    }
}
