using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.Trades;

/// <summary>
/// The `mt-trade-import` spec states, under Edge Cases, that deleting a Strategy cascade-deletes
/// its <see cref="StrategyTrade"/> rows. That scenario shipped with no runtime coverage anywhere in
/// the suite — the EF configuration asserted it and nothing exercised it.
/// <para>
/// These tests run on SQLite with <c>Foreign Keys=True</c> so the constraint is enforced by the
/// DATABASE. The deletion is issued as raw SQL on purpose: deleting through the change tracker
/// would only prove EF's client-side cascade over entities it happens to have loaded, which is a
/// strictly weaker claim than the one the spec makes.
/// </para>
/// </summary>
public class StrategyTradeCascadeDeleteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TradeCascadeTestDbContext> _options;

    public StrategyTradeCascadeDeleteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TradeCascadeTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new TradeCascadeTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Strategy NewStrategy(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
    };

    private static StrategyTrade NewTrade(Guid strategyId, long ticket) => new()
    {
        Id = Guid.NewGuid(),
        StrategyId = strategyId,
        Ticket = ticket,
        OpenTime = new DateTime(2026, 4, 20, 14, 47, 17, DateTimeKind.Utc),
        CloseTime = new DateTime(2026, 4, 20, 17, 3, 33, DateTimeKind.Utc),
        Type = "buy",
        Size = 0.10m,
        Item = "XAUUSD",
        OpenPrice = 2610.5m,
        ClosePrice = 2615.1m,
        Profit = 20.96m,
    };

    [Fact]
    public void DeletingAStrategy_CascadeDeletesItsTrades_AtTheDatabaseLevel()
    {
        var strategy = NewStrategy("WF_8_34_NQ_SH_LIR_H1_2_33_3");

        using (var seed = new TradeCascadeTestDbContext(_options))
        {
            seed.Strategies.Add(strategy);
            seed.StrategyTrades.AddRange(
                NewTrade(strategy.Id, 263463718),
                NewTrade(strategy.Id, 263004851),
                NewTrade(strategy.Id, 263004852));
            seed.SaveChanges();
        }

        using (var act = new TradeCascadeTestDbContext(_options))
        {
            // Raw SQL, not Remove(): this must prove the DATABASE cascades, not that EF's change
            // tracker cascaded over entities it had loaded into memory.
            act.Database.ExecuteSqlRaw(
                "DELETE FROM Strategies WHERE Id = {0}", strategy.Id);
        }

        using var assert = new TradeCascadeTestDbContext(_options);
        assert.Strategies.Count().Should().Be(0);
        assert.StrategyTrades.Count().Should()
            .Be(0, "the spec's Edge Case says deleting a Strategy removes all of its StrategyTrade rows");
    }

    [Fact]
    public void DeletingOneStrategy_LeavesAnotherStrategysTradesUntouched()
    {
        var deleted = NewStrategy("WF_9_26_XAUUSD_H1_KAMA_BB_4_53");
        var kept = NewStrategy("WF_3_11_EURUSD_H4_RSI_2_7_1");

        using (var seed = new TradeCascadeTestDbContext(_options))
        {
            seed.Strategies.AddRange(deleted, kept);
            seed.StrategyTrades.AddRange(
                NewTrade(deleted.Id, 4533187),
                NewTrade(kept.Id, 7532499),
                NewTrade(kept.Id, 7532500));
            seed.SaveChanges();
        }

        using (var act = new TradeCascadeTestDbContext(_options))
        {
            act.Database.ExecuteSqlRaw("DELETE FROM Strategies WHERE Id = {0}", deleted.Id);
        }

        using var assert = new TradeCascadeTestDbContext(_options);
        assert.StrategyTrades.Count().Should().Be(2, "the cascade is scoped to the deleted strategy");
        assert.StrategyTrades.Should().OnlyContain(t => t.StrategyId == kept.Id);
    }
}

/// <summary>
/// Minimal context carrying the two entities under test. It applies the REAL
/// <see cref="StrategyTradeConfiguration"/> so the delete behaviour asserted here is production's,
/// not one restated by the test.
/// </summary>
public class TradeCascadeTestDbContext : DbContext
{
    public TradeCascadeTestDbContext(DbContextOptions<TradeCascadeTestDbContext> options)
        : base(options) { }

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyTrade> StrategyTrades => Set<StrategyTrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Strategy>(b =>
        {
            b.ToTable("Strategies");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
            b.Ignore(x => x.MonthlyPerformance);
            b.Ignore(x => x.Comments);
        });

        modelBuilder.ApplyConfiguration(new StrategyTradeConfiguration());
    }
}
