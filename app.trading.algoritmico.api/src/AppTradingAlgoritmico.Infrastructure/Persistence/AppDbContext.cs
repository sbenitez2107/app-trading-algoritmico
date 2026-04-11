using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TradingAccount> TradingAccounts => Set<TradingAccount>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<BuildingBlock> BuildingBlocks => Set<BuildingBlock>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchStage> BatchStages => Set<BatchStage>();
    public DbSet<Strategy> Strategies => Set<Strategy>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
