using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Factory for creating an in-memory AppDbContext for StrategyService unit tests.
/// Uses EF InMemory provider — does NOT enforce FK constraints or SetNull.
/// For FK/SetNull tests, use StrategyTestDbContext (SQLite) instead.
/// </summary>
public static class InMemoryDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }
}
