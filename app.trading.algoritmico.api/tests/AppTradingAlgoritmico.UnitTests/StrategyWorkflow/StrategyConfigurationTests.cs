using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests EF Core mapping for Strategy entity — FK nullability and SetNull delete behavior.
/// Uses SQLite in-memory because EF InMemory does NOT enforce FK constraints or SetNull.
/// </summary>
public class StrategyConfigurationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<StrategyTestDbContext> _options;

    public StrategyConfigurationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<StrategyTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new StrategyTestDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task Strategy_WithBothFks_PersistsSuccessfully()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        using (var db = new StrategyTestDbContext(_options))
        {
            var account = new TradingAccount
            {
                Id = accountId,
                Name = "Test Account",
                Broker = "Darwinex",
                AccountNumber = 12345,
                Login = 12345,
                PasswordEncrypted = "enc",
                Server = "srv",
                CreatedAt = DateTime.UtcNow
            };
            var stage = new BatchStage
            {
                Id = stageId,
                StageType = PipelineStageType.Builder,
                Order = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.TradingAccounts.Add(account);
            db.BatchStages.Add(stage);
            await db.SaveChangesAsync();

            var strategy = new Strategy
            {
                Id = strategyId,
                Name = "Dual-linked",
                BatchStageId = stageId,
                TradingAccountId = accountId,
                CreatedAt = DateTime.UtcNow
            };
            db.Strategies.Add(strategy);
            await db.SaveChangesAsync();
        }

        // Act — reload from DB
        using (var db = new StrategyTestDbContext(_options))
        {
            var loaded = await db.Strategies.FindAsync(strategyId);

            // Assert (spec M2: strategy linked to account)
            loaded.Should().NotBeNull();
            loaded!.BatchStageId.Should().Be(stageId);
            loaded.TradingAccountId.Should().Be(accountId);
        }
    }

    [Fact]
    public async Task DeleteBatchStage_DualLinkedStrategy_PreservesRowWithNullBatchStageId()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        using (var db = new StrategyTestDbContext(_options))
        {
            db.TradingAccounts.Add(new TradingAccount
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
            db.BatchStages.Add(new BatchStage
            {
                Id = stageId,
                StageType = PipelineStageType.Builder,
                Order = 0,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            db.Strategies.Add(new Strategy
            {
                Id = strategyId,
                Name = "Dual",
                BatchStageId = stageId,
                TradingAccountId = accountId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Act — delete the BatchStage
        using (var db = new StrategyTestDbContext(_options))
        {
            var stage = await db.BatchStages.FindAsync(stageId);
            db.BatchStages.Remove(stage!);
            await db.SaveChangesAsync();
        }

        // Assert (spec M3): strategy survives, BatchStageId set to null, TradingAccountId intact
        using (var db = new StrategyTestDbContext(_options))
        {
            var strategy = await db.Strategies.FindAsync(strategyId);
            strategy.Should().NotBeNull("dual-linked strategy must not be deleted when BatchStage is removed");
            strategy!.BatchStageId.Should().BeNull("SetNull must null the FK when the referenced stage is deleted");
            strategy.TradingAccountId.Should().Be(accountId, "TradingAccountId must remain intact");
        }
    }

    [Fact]
    public async Task Strategy_WithNullBatchStageId_PipelineOnly_IsValid()
    {
        // Arrange — strategy with only TradingAccountId set (spec M1)
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();

        using (var db = new StrategyTestDbContext(_options))
        {
            db.TradingAccounts.Add(new TradingAccount
            {
                Id = accountId,
                Name = "Solo Account",
                Broker = "Darwinex",
                AccountNumber = 99,
                Login = 99,
                PasswordEncrypted = "e",
                Server = "s",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            db.Strategies.Add(new Strategy
            {
                Id = strategyId,
                Name = "Account-only",
                BatchStageId = null,
                TradingAccountId = accountId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Act — reload
        using (var db = new StrategyTestDbContext(_options))
        {
            var loaded = await db.Strategies.FindAsync(strategyId);

            // Assert
            loaded.Should().NotBeNull();
            loaded!.BatchStageId.Should().BeNull();
            loaded.TradingAccountId.Should().Be(accountId);
        }
    }
}

/// <summary>
/// Minimal DbContext for EF configuration tests — excludes Identity to simplify SQLite schema.
/// </summary>
public class StrategyTestDbContext : DbContext
{
    public StrategyTestDbContext(DbContextOptions<StrategyTestDbContext> options) : base(options) { }

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<BatchStage> BatchStages => Set<BatchStage>();
    public DbSet<TradingAccount> TradingAccounts => Set<TradingAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply only the Strategy configuration (which wires BatchStage + TradingAccount FKs)
        modelBuilder.ApplyConfiguration(new StrategyConfiguration());

        // SQLite does not support nvarchar(max) — override column types for TEXT columns
        modelBuilder.Entity<Strategy>()
            .Property(x => x.Pseudocode)
            .HasColumnType("TEXT");

        modelBuilder.Entity<Strategy>()
            .Property(x => x.IndicatorParameters)
            .HasColumnType("TEXT");

        // Minimal TradingAccount config
        modelBuilder.Entity<TradingAccount>(b =>
        {
            b.ToTable("TradingAccounts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Broker).IsRequired().HasMaxLength(100);
            b.Property(x => x.PasswordEncrypted).IsRequired();
            b.Property(x => x.Server).IsRequired().HasMaxLength(200);
        });

        // Minimal BatchStage config (only what Strategy FK needs)
        modelBuilder.Entity<BatchStage>(b =>
        {
            b.ToTable("BatchStages");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.Batch); // no Batch table in this test context
        });

        // StrategyMonthlyPerformance — needed because Strategy has a collection
        modelBuilder.Entity<StrategyMonthlyPerformance>(b =>
        {
            b.ToTable("StrategyMonthlyPerformances");
            b.HasKey(x => x.Id);
        });
    }
}
