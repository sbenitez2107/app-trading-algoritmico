using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyCommentConfiguration : IEntityTypeConfiguration<StrategyComment>
{
    public void Configure(EntityTypeBuilder<StrategyComment> builder)
    {
        builder.ToTable("StrategyComments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.StrategyId, x.CreatedAt });
    }
}
