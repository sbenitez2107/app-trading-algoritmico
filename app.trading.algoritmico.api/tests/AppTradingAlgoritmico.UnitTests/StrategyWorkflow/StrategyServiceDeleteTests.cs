using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategyService.DeleteAsync — #3 hard delete.
/// </summary>
public class StrategyServiceDeleteTests
{
    [Fact]
    public async Task DeleteAsync_ExistingStrategy_RemovesFromDb()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();

        using (var db = InMemoryDbContextFactory.Create(dbName))
        {
            db.Strategies.Add(new Strategy
            {
                Id = strategyId,
                Name = "S1",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var writeDb = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(writeDb, sqxMock.Object, htmlMock.Object);

        // Act
        await sut.DeleteAsync(strategyId, default);

        // Assert — strategy should no longer exist
        using var verifyDb = InMemoryDbContextFactory.Create(dbName);
        verifyDb.Strategies.Find(strategyId).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentStrategy_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = InMemoryDbContextFactory.Create();
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var act = () => sut.DeleteAsync(Guid.NewGuid(), default);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
