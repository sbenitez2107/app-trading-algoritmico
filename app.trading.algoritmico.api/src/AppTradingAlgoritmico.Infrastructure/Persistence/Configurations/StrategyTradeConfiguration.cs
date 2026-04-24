using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyTradeConfiguration : IEntityTypeConfiguration<StrategyTrade>
{
    public void Configure(EntityTypeBuilder<StrategyTrade> builder)
    {
        builder.ToTable("StrategyTrades");

        builder.HasKey(x => x.Id);

        // Unique key for upsert: one trade ticket per strategy
        builder.HasIndex(x => new { x.StrategyId, x.Ticket })
            .IsUnique();

        // String lengths
        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Item)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.CloseReason)
            .HasMaxLength(20);

        // Price fields — higher precision (18,5) to handle forex / index prices
        builder.Property(x => x.OpenPrice).HasPrecision(18, 5);
        builder.Property(x => x.ClosePrice).HasPrecision(18, 5);
        builder.Property(x => x.StopLoss).HasPrecision(18, 5);
        builder.Property(x => x.TakeProfit).HasPrecision(18, 5);

        // Money fields — standard (18,2)
        builder.Property(x => x.Size).HasPrecision(18, 2);
        builder.Property(x => x.Commission).HasPrecision(18, 2);
        builder.Property(x => x.Taxes).HasPrecision(18, 2);
        builder.Property(x => x.Swap).HasPrecision(18, 2);
        builder.Property(x => x.Profit).HasPrecision(18, 2);

        // IsOpen — plain bit column; set by service on every upsert (not computed)
        builder.Property(x => x.IsOpen)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        // FK is configured from the Strategy side (HasMany → WithOne); declared here for clarity
        builder.HasOne(x => x.Strategy)
            .WithMany(s => s.Trades)
            .HasForeignKey(x => x.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
