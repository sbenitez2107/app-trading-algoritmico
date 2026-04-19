using AppTradingAlgoritmico.Application.DTOs.GridPresets;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.GridPresets;

/// <summary>
/// Tests for GridPresetService — #4 column preset management.
/// Uses EF InMemory for isolation.
/// </summary>
public class GridPresetServiceTests
{
    private static AppDbContext CreateDb(string? name = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task GetByUserAsync_UserHasPresets_ReturnsAll()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        using (var db = CreateDb(dbName))
        {
            db.StrategyGridPresets.AddRange(
                new StrategyGridPreset
                {
                    Name = "Performance",
                    UserId = userId,
                    VisibleColumnsJson = "[\"totalProfit\"]",
                    ColumnOrderJson = "[\"totalProfit\"]",
                    CreatedAt = DateTime.UtcNow
                },
                new StrategyGridPreset
                {
                    Name = "Risk",
                    UserId = userId,
                    VisibleColumnsJson = "[\"drawdown\"]",
                    ColumnOrderJson = "[\"drawdown\"]",
                    CreatedAt = DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        using var readDb = CreateDb(dbName);
        var sut = new GridPresetService(readDb);

        // Act
        var result = await sut.GetByUserAsync(userId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserAsync_NoPresets_ReturnsEmpty()
    {
        // Arrange
        using var db = CreateDb();
        var sut = new GridPresetService(db);

        // Act
        var result = await sut.GetByUserAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ValidDto_PersistsAndReturnsPreset()
    {
        // Arrange
        using var db = CreateDb();
        var sut = new GridPresetService(db);
        var userId = Guid.NewGuid();
        var dto = new CreateGridPresetDto("Performance", ["totalProfit", "sharpeRatio"], ["totalProfit", "sharpeRatio"]);

        // Act
        var result = await sut.CreateAsync(userId, dto);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Performance");
        result.VisibleColumns.Should().BeEquivalentTo(["totalProfit", "sharpeRatio"]);
        result.ColumnOrder.Should().BeEquivalentTo(["totalProfit", "sharpeRatio"]);

        db.StrategyGridPresets.Count().Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsArgumentException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        using (var db = CreateDb(dbName))
        {
            db.StrategyGridPresets.Add(new StrategyGridPreset
            {
                Name = "Performance",
                UserId = userId,
                VisibleColumnsJson = "[\"totalProfit\"]",
                ColumnOrderJson = "[\"totalProfit\"]",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var writeDb = CreateDb(dbName);
        var sut = new GridPresetService(writeDb);
        var dto = new CreateGridPresetDto("Performance", ["totalProfit"], ["totalProfit"]);

        // Act
        var act = () => sut.CreateAsync(userId, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Performance*");
    }

    [Fact]
    public async Task DeleteAsync_ExistingPreset_RemovesIt()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var presetId = Guid.NewGuid();

        using (var db = CreateDb(dbName))
        {
            db.StrategyGridPresets.Add(new StrategyGridPreset
            {
                Id = presetId,
                Name = "Performance",
                UserId = userId,
                VisibleColumnsJson = "[\"totalProfit\"]",
                ColumnOrderJson = "[\"totalProfit\"]",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var writeDb = CreateDb(dbName);
        var sut = new GridPresetService(writeDb);

        // Act
        await sut.DeleteAsync(userId, presetId);

        // Assert
        using var verifyDb = CreateDb(dbName);
        verifyDb.StrategyGridPresets.Find(presetId).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentPreset_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = CreateDb();
        var sut = new GridPresetService(db);

        // Act
        var act = () => sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_PresetBelongsToOtherUser_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var presetId = Guid.NewGuid();

        using (var db = CreateDb(dbName))
        {
            db.StrategyGridPresets.Add(new StrategyGridPreset
            {
                Id = presetId,
                Name = "Performance",
                UserId = ownerUserId,
                VisibleColumnsJson = "[\"totalProfit\"]",
                ColumnOrderJson = "[\"totalProfit\"]",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var db2 = CreateDb(dbName);
        var sut = new GridPresetService(db2);

        // Act — attempt to delete using another user's id
        var act = () => sut.DeleteAsync(otherUserId, presetId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
