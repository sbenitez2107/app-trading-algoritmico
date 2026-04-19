using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for StrategyService comment methods — bitácora trackable feature.
/// </summary>
public class StrategyServiceCommentsTests
{
    private static StrategyService CreateSut(string? dbName = null)
    {
        var db = InMemoryDbContextFactory.Create(dbName ?? Guid.NewGuid().ToString());
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        return new StrategyService(db, sqxMock.Object, htmlMock.Object);
    }

    private static async Task<AppTradingAlgoritmico.Infrastructure.Persistence.AppDbContext> SeedStrategyAsync(
        string dbName, Guid strategyId)
    {
        using var db = InMemoryDbContextFactory.Create(dbName);
        db.Strategies.Add(new Strategy
        {
            Id = strategyId,
            Name = "Test Strategy",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return db;
    }

    // --- GetCommentsAsync ---

    [Fact]
    public async Task GetCommentsAsync_StrategyNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GetCommentsAsync(Guid.NewGuid(), default);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetCommentsAsync_EmptyStrategy_ReturnsEmptyList()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        await SeedStrategyAsync(dbName, strategyId);

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.GetCommentsAsync(strategyId, default);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsCommentsOrderedByCreatedAtDesc()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (var seedDb = InMemoryDbContextFactory.Create(dbName))
        {
            seedDb.Strategies.Add(new Strategy
            {
                Id = strategyId,
                Name = "Test Strategy",
                CreatedAt = baseTime
            });
            seedDb.StrategyComments.Add(new StrategyComment
            {
                Id = Guid.NewGuid(),
                StrategyId = strategyId,
                Content = "First comment",
                CreatedAt = baseTime.AddDays(1),
                CreatedBy = "user-a"
            });
            seedDb.StrategyComments.Add(new StrategyComment
            {
                Id = Guid.NewGuid(),
                StrategyId = strategyId,
                Content = "Second comment",
                CreatedAt = baseTime.AddDays(2),
                CreatedBy = "user-a"
            });
            seedDb.StrategyComments.Add(new StrategyComment
            {
                Id = Guid.NewGuid(),
                StrategyId = strategyId,
                Content = "Third comment",
                CreatedAt = baseTime.AddDays(3),
                CreatedBy = "user-a"
            });
            await seedDb.SaveChangesAsync();
        }

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var result = (await sut.GetCommentsAsync(strategyId, default)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Content.Should().Be("Third comment");
        result[1].Content.Should().Be("Second comment");
        result[2].Content.Should().Be("First comment");
    }

    // --- AddCommentAsync ---

    [Fact]
    public async Task AddCommentAsync_StrategyNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.AddCommentAsync(Guid.NewGuid(), "some content", "user-1", default);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddCommentAsync_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        await SeedStrategyAsync(dbName, strategyId);

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var act = () => sut.AddCommentAsync(strategyId, "", "user-1", default);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddCommentAsync_WhitespaceContent_ThrowsArgumentException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        await SeedStrategyAsync(dbName, strategyId);

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var act = () => sut.AddCommentAsync(strategyId, "   ", "user-1", default);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddCommentAsync_ValidContent_PersistsWithCreatedBy()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        await SeedStrategyAsync(dbName, strategyId);

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        // Act
        var result = await sut.AddCommentAsync(strategyId, "Great strategy!", "user-42", default);

        // Assert
        result.Content.Should().Be("Great strategy!");
        result.CreatedBy.Should().Be("user-42");

        using var verifyDb = InMemoryDbContextFactory.Create(dbName);
        var saved = verifyDb.StrategyComments.FirstOrDefault(c => c.StrategyId == strategyId);
        saved.Should().NotBeNull();
        saved!.Content.Should().Be("Great strategy!");
        saved.CreatedBy.Should().Be("user-42");
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsDtoWithGeneratedIdAndTimestamp()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var strategyId = Guid.NewGuid();
        await SeedStrategyAsync(dbName, strategyId);

        var db = InMemoryDbContextFactory.Create(dbName);
        var sqxMock = new Mock<ISqxParserService>();
        var htmlMock = new Mock<IHtmlReportParserService>();
        var sut = new StrategyService(db, sqxMock.Object, htmlMock.Object);

        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await sut.AddCommentAsync(strategyId, "Test", null, default);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.CreatedAt.Should().BeAfter(before);
        result.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
