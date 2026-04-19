using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyMonthlyPerformanceConfiguration : IEntityTypeConfiguration<StrategyMonthlyPerformance>
{
    public void Configure(EntityTypeBuilder<StrategyMonthlyPerformance> builder)
    {
        builder.ToTable("StrategyMonthlyPerformances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Year).IsRequired();
        builder.Property(x => x.Month).IsRequired();

        builder.Property(x => x.Profit)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.StrategyId, x.Year, x.Month }).IsUnique();
    }
}
