using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class BatchStageConfiguration : IEntityTypeConfiguration<BatchStage>
{
    public void Configure(EntityTypeBuilder<BatchStage> builder)
    {
        builder.ToTable("BatchStages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageType)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Order)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.HasOne(x => x.Batch)
            .WithMany(b => b.Stages)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BatchId, x.StageType })
            .IsUnique();
    }
}
