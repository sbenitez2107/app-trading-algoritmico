using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Persistence;

/// <summary>
/// Builds a fresh <see cref="AppDbContext"/> from the same <see cref="DbContextOptions{TContext}"/>
/// the DI container registered, so every attempt of a retried unit of work gets its own change
/// tracker while still talking to the configured provider, connection string and retry policy.
/// <para>
/// Returned as <see cref="IBacktestDbContext"/>, never as <c>AppDbContext</c>: design.md D2's
/// structural isolation from <c>StrategyTrades</c> is preserved — the importer's compile-time
/// surface is unchanged.
/// </para>
/// </summary>
public sealed class BacktestDbContextFactory(DbContextOptions<AppDbContext> options) : IBacktestDbContextFactory
{
    public IBacktestDbContext Create() => new AppDbContext(options);
}
