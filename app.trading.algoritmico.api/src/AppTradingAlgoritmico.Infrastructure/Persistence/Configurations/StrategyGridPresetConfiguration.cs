using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyGridPresetConfiguration : IEntityTypeConfiguration<StrategyGridPreset>
{
    public void Configure(EntityTypeBuilder<StrategyGridPreset> builder)
    {
        builder.ToTable("StrategyGridPresets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.VisibleColumnsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ColumnOrderJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.Name })
            .IsUnique();
    }
}
