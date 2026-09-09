using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class FundingStageLimitConfiguration : IEntityTypeConfiguration<FundingStageLimit>
{
    public void Configure(EntityTypeBuilder<FundingStageLimit> builder)
    {
        builder.ToTable("FundingStageLimits");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MaxLossLimitPct).HasPrecision(9, 6);
        builder.Property(x => x.ProfitTargetPct).HasPrecision(9, 6);

        // One stage per ordinal within a given parent row.
        builder.HasIndex(x => new { x.BrokerRiskLimitsId, x.StageOrdinal }).IsUnique();
    }
}
