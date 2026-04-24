using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategyService.AddToAccountAsync — spec: account-strategies R2, strategy-model M4.
/// Uses EF InMemory + Moq for ISqxParserService and IHtmlReportParserService.
/// </summary>
public class StrategyServiceAddToAccountTests
{
    private static ParsedReportDto ValidReport() => new(
        Symbol: "EURUSD",
        Timeframe: "H1",
        BacktestFrom: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        BacktestTo: new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Kpis: new UpdateStrategyKpisDto(
            TotalProfit: 1234.56m,
            ProfitInPips: null,
            YearlyAvgProfit: null,
            YearlyAvgReturn: null,
            Cagr: null,
            NumberOfTrades: 150,
            SharpeRatio: 1.5m,
            ProfitFactor: 1.8m,
            ReturnDrawdownRatio: null,
            WinningPercentage: 60m,
            Drawdown: 500m,
            DrawdownPercent: null,
            DailyAvgProfit: null,
            MonthlyAvgProfit: null,
            AverageTrade: null,
            AnnualReturnMaxDdRatio: null,
            RExpectancy: null,
            RExpectancyScore: null,
            StrQualityNumber: null,
            SqnScore: null,
            WinsLossesRatio: null,
            PayoutRatio: null,
            AverageBarsInTrade: null,
            Ahpr: null,
            ZScore: null,
            ZProbability: null,
            Expectancy: null,
            Deviation: null,
            Exposure: null,
            StagnationInDays: null,
            StagnationPercent: null,
            NumberOfWins: null,
            NumberOfLosses: null,
            NumberOfCancelled: null,
            GrossProfit: null,
            GrossLoss: null,
            AverageWin: null,
            AverageLoss: null,
            LargestWin: null,
            LargestLoss: null,
            MaxConsecutiveWins: null,
            MaxConsecutiveLosses: null,
            AverageConsecutiveWins: null,
            AverageConsecutiveLosses: null,
            AverageBarsInWins: null,
            AverageBarsInLosses: null
        ),
        MonthlyPerformance: []
    );

    [Fact]
    public async Task AddToAccountAsync_HappyPath_PersistsStrategyWithTradingAccountId()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var setup = InMemoryDbContextFactory.Create(dbName))
        {
            setup.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Acc",
                Broker = "Darwinex",
                AccountNumber = 1,
                Login = 1,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        sqxMock.Setup(x => x.ExtractStrategyMetadataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Application.DTOs.Strategies.ParsedSqxMetadataDto(
                   "some pseudocode", "SMA", "SMA", "SMA(Period=20)"));

        var htmlMock = new Mock<IHtmlReportParserService>();
        htmlMock.Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ValidReport());

        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.AddToAccountAsync(
            accountId, "MyStrategy", Stream.Null, Stream.Null);

        // Assert — spec R2 scenario 1: TradingAccountId set, BatchStageId null, KPIs from report
        result.Should().NotBeNull();
        result.Name.Should().Be("MyStrategy");
        result.TotalProfit.Should().Be(1234.56m);
        result.SharpeRatio.Should().Be(1.5m);

        // Verify persistence
        using var verify = InMemoryDbContextFactory.Create(dbName);
        var saved = await verify.Strategies.FirstOrDefaultAsync(s => s.Name == "MyStrategy");
        saved.Should().NotBeNull();
        saved!.TradingAccountId.Should().Be(accountId);
        saved.BatchStageId.Should().BeNull();
    }

    [Fact]
    public async Task AddToAccountAsync_AccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = InMemoryDbContextFactory.Create();
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act — spec R2 scenario 5
        var act = () => sut.AddToAccountAsync(Guid.NewGuid(), "X", Stream.Null, Stream.Null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddToAccountAsync_HtmlParserReturnsNull_ThrowsArgumentException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var setup = InMemoryDbContextFactory.Create(dbName))
        {
            setup.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Acc",
                Broker = "Darwinex",
                AccountNumber = 2,
                Login = 2,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        sqxMock.Setup(x => x.ExtractStrategyMetadataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Application.DTOs.Strategies.ParsedSqxMetadataDto("pseudocode", null, null, null));

        var htmlMock = new Mock<IHtmlReportParserService>();
        htmlMock.Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ParsedReportDto?)null);

        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act — spec R2 scenario 4
        var act = () => sut.AddToAccountAsync(accountId, "X", Stream.Null, Stream.Null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddToAccountAsync_WithMagicNumber_PersistsMagicNumber()
    {
        // Arrange — spec R-M2: magicNumber provided on create is persisted on Strategy
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var setup = InMemoryDbContextFactory.Create(dbName))
        {
            setup.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Acc",
                Broker = "Darwinex",
                AccountNumber = 42,
                Login = 42,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        sqxMock.Setup(x => x.ExtractStrategyMetadataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Application.DTOs.Strategies.ParsedSqxMetadataDto("pseudocode", null, null, null));

        var htmlMock = new Mock<IHtmlReportParserService>();
        htmlMock.Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ValidReport());

        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.AddToAccountAsync(
            accountId, "WithMagic", Stream.Null, Stream.Null, magicNumber: 2333376);

        // Assert — DTO carries magicNumber
        result.MagicNumber.Should().Be(2333376);

        // Verify persistence
        using var verify = InMemoryDbContextFactory.Create(dbName);
        var saved = await verify.Strategies.FirstOrDefaultAsync(s => s.Name == "WithMagic");
        saved.Should().NotBeNull();
        saved!.MagicNumber.Should().Be(2333376);
    }

    [Fact]
    public async Task AddToAccountAsync_WithoutMagicNumber_PersistsNullMagicNumber()
    {
        // Arrange — spec R-M2: magicNumber is optional; null is valid
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var setup = InMemoryDbContextFactory.Create(dbName))
        {
            setup.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Acc",
                Broker = "Darwinex",
                AccountNumber = 43,
                Login = 43,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        using var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        sqxMock.Setup(x => x.ExtractStrategyMetadataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Application.DTOs.Strategies.ParsedSqxMetadataDto("pseudocode", null, null, null));

        var htmlMock = new Mock<IHtmlReportParserService>();
        htmlMock.Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ValidReport());

        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act — call overload without magicNumber (default = null)
        var result = await sut.AddToAccountAsync(
            accountId, "NoMagic", Stream.Null, Stream.Null);

        // Assert
        result.MagicNumber.Should().BeNull();

        using var verify = InMemoryDbContextFactory.Create(dbName);
        var saved = await verify.Strategies.FirstOrDefaultAsync(s => s.Name == "NoMagic");
        saved!.MagicNumber.Should().BeNull();
    }

    [Fact]
    public async Task AddToAccountAsync_BothFksNull_ThrowsException()
    {
        // Arrange — attempt AddToAccountAsync with non-existent accountId
        // Service should throw KeyNotFoundException BEFORE persisting any orphaned strategy (spec M4)
        using var db = InMemoryDbContextFactory.Create();
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act — empty Guid → no account found
        var act = () => sut.AddToAccountAsync(Guid.Empty, "X", Stream.Null, Stream.Null);

        // Assert — spec M4: service prevents orphan creation
        await act.Should().ThrowAsync<Exception>("no account found means service stops before persisting an orphan");
    }
}
