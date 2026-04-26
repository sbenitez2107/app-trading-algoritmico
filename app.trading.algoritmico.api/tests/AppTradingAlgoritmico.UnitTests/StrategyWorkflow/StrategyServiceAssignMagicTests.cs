using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategyService.AssignMagicNumberAsync — used by the import-trades modal
/// to manually link a magic number to an existing strategy.
/// </summary>
public class StrategyServiceAssignMagicTests
{
    private static StrategyService CreateSut(out AppDbContext db)
    {
        db = InMemoryDbContextFactory.Create();
        return new StrategyService(
            db,
            Mock.Of<ISqxParserService>(),
            Mock.Of<IHtmlReportParserService>());
    }

    private static TradingAccount MakeAccount(Guid id) => new()
    {
        Id = id,
        Name = "Acc",
        Broker = "Darwinex",
        AccountType = AccountType.Demo,
        Platform = PlatformType.MT4,
        AccountNumber = 1,
        Login = 1,
        PasswordEncrypted = "e",
        Server = "s",
        IsEnabled = true
    };

    private static Strategy MakeStrategy(Guid id, Guid accountId, int? magic = null, string name = "Strategy_X") =>
        new()
        {
            Id = id,
            Name = name,
            TradingAccountId = accountId,
            MagicNumber = magic
        };

    [Fact]
    public async Task AssignMagicNumberAsync_StrategyWithoutMagic_AssignsAndPersists()
    {
        // Arrange
        var sut = CreateSut(out var db);
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int magic = 12345;

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId));
        await db.SaveChangesAsync();

        // Act
        var dto = await sut.AssignMagicNumberAsync(accountId, strategyId, magic, default);

        // Assert
        dto.MagicNumber.Should().Be(magic);
        (await db.Strategies.FindAsync(strategyId))!.MagicNumber.Should().Be(magic);
    }

    [Fact]
    public async Task AssignMagicNumberAsync_StrategyNotFound_ThrowsKeyNotFound()
    {
        var sut = CreateSut(out var db);
        var accountId = Guid.NewGuid();
        db.TradingAccounts.Add(MakeAccount(accountId));
        await db.SaveChangesAsync();

        var act = async () =>
            await sut.AssignMagicNumberAsync(accountId, Guid.NewGuid(), 1, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AssignMagicNumberAsync_StrategyBelongsToDifferentAccount_ThrowsKeyNotFound()
    {
        var sut = CreateSut(out var db);
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        db.TradingAccounts.AddRange(MakeAccount(accountA), MakeAccount(accountB));
        db.Strategies.Add(MakeStrategy(strategyId, accountA));
        await db.SaveChangesAsync();

        // Trying to assign through the WRONG account → 404 (do not leak existence)
        var act = async () =>
            await sut.AssignMagicNumberAsync(accountB, strategyId, 99, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AssignMagicNumberAsync_SameMagicAlreadyAssigned_IsIdempotent()
    {
        var sut = CreateSut(out var db);
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        const int magic = 7777;

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, magic));
        await db.SaveChangesAsync();

        var dto = await sut.AssignMagicNumberAsync(accountId, strategyId, magic, default);

        dto.MagicNumber.Should().Be(magic);
    }

    [Fact]
    public async Task AssignMagicNumberAsync_StrategyHasDifferentMagic_ThrowsInvalidOperation()
    {
        var sut = CreateSut(out var db);
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.Add(MakeStrategy(strategyId, accountId, magic: 100));
        await db.SaveChangesAsync();

        var act = async () =>
            await sut.AssignMagicNumberAsync(accountId, strategyId, 200, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has magic number 100*");
    }

    [Fact]
    public async Task AssignMagicNumberAsync_MagicUsedByAnotherStrategyInSameAccount_ThrowsInvalidOperation()
    {
        var sut = CreateSut(out var db);
        var accountId = Guid.NewGuid();
        var existingStrategyId = Guid.NewGuid();
        var targetStrategyId = Guid.NewGuid();
        const int magic = 5555;

        db.TradingAccounts.Add(MakeAccount(accountId));
        db.Strategies.AddRange(
            MakeStrategy(existingStrategyId, accountId, magic, name: "Owner"),
            MakeStrategy(targetStrategyId, accountId, magic: null, name: "Target"));
        await db.SaveChangesAsync();

        var act = async () =>
            await sut.AssignMagicNumberAsync(accountId, targetStrategyId, magic, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{magic}*already used*");

        (await db.Strategies.FindAsync(targetStrategyId))!.MagicNumber.Should().BeNull();
    }
}
