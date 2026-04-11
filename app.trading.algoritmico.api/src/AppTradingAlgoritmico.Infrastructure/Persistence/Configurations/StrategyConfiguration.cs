using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> builder)
    {
        builder.ToTable("Strategies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Pseudocode)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.SharpeRatio)
            .HasPrecision(18, 6);

        builder.Property(x => x.ReturnDrawdownRatio)
            .HasPrecision(18, 6);

        builder.Property(x => x.WinRate)
            .HasPrecision(18, 6);

        builder.Property(x => x.ProfitFactor)
            .HasPrecision(18, 6);

        builder.Property(x => x.NetProfit)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxDrawdown)
            .HasPrecision(18, 2);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.HasOne(x => x.BatchStage)
            .WithMany(bs => bs.Strategies)
            .HasForeignKey(x => x.BatchStageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
