using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class PortfolioStrategyConfiguration : IEntityTypeConfiguration<PortfolioStrategy>
{
    public void Configure(EntityTypeBuilder<PortfolioStrategy> builder)
    {
        builder.ToTable("PortfolioStrategies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Weight)
            .HasPrecision(18, 6);

        // A strategy in a portfolio. Deleting the strategy is RESTRICTED while it is a member
        // (remove it from the portfolio first) — avoids silently corrupting combined risk numbers.
        builder.HasOne(x => x.Strategy)
            .WithMany()
            .HasForeignKey(x => x.StrategyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One membership row per (portfolio, strategy).
        builder.HasIndex(x => new { x.PortfolioId, x.StrategyId }).IsUnique();
    }
}
