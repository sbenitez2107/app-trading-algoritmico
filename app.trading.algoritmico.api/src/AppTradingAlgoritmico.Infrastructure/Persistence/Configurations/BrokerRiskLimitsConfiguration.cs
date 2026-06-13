using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class BrokerRiskLimitsConfiguration : IEntityTypeConfiguration<BrokerRiskLimits>
{
    public void Configure(EntityTypeBuilder<BrokerRiskLimits> builder)
    {
        builder.ToTable("BrokerRiskLimits");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Broker)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FundingService).HasConversion<int>();
        builder.Property(x => x.DrawdownModel).HasConversion<int>();

        builder.Property(x => x.DailyLossLimitPct).HasPrecision(9, 6);
        builder.Property(x => x.MaxLossLimitPct).HasPrecision(9, 6);
        builder.Property(x => x.ProfitTargetPct).HasPrecision(9, 6);

        // One limits row per broker.
        builder.HasIndex(x => x.Broker).IsUnique();
    }
}
