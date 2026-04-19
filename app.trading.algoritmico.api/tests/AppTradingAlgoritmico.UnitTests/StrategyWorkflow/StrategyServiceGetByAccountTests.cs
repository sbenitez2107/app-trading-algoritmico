using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategyService.GetByAccountAsync — spec: account-strategies R1.
/// Uses EF InMemory via AppDbContext.
/// </summary>
public class StrategyServiceGetByAccountTests
{
    [Fact]
    public async Task GetByAccountAsync_AccountWithStrategies_ReturnsPaginatedResult()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var db = InMemoryDbContextFactory.Create(dbName))
        {
            db.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Acc1",
                Broker = "Darwinex",
                AccountNumber = 1,
                Login = 1,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            db.Strategies.AddRange(
                new Strategy { Name = "S1", TradingAccountId = accountId, CreatedAt = DateTime.UtcNow },
                new Strategy { Name = "S2", TradingAccountId = accountId, CreatedAt = DateTime.UtcNow },
                new Strategy { Name = "S3", TradingAccountId = accountId, CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        using var readDb = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(readDb, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.GetByAccountAsync(accountId);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetByAccountAsync_AccountExistsNoStrategies_ReturnsEmpty()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        using (var db = InMemoryDbContextFactory.Create(dbName))
        {
            db.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Empty",
                Broker = "Darwinex",
                AccountNumber = 2,
                Login = 2,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var readDb = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(readDb, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.GetByAccountAsync(accountId);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetByAccountAsync_AccountNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = InMemoryDbContextFactory.Create();
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var act = () => sut.GetByAccountAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
