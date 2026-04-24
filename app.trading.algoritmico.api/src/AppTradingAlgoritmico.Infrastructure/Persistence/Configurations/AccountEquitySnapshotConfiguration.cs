using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class AccountEquitySnapshotConfiguration : IEntityTypeConfiguration<AccountEquitySnapshot>
{
    public void Configure(EntityTypeBuilder<AccountEquitySnapshot> builder)
    {
        builder.ToTable("AccountEquitySnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReportTime)
            .IsRequired();

        // All financial fields — standard (18,2)
        builder.Property(x => x.Balance).HasPrecision(18, 2);
        builder.Property(x => x.Equity).HasPrecision(18, 2);
        builder.Property(x => x.FloatingPnL).HasPrecision(18, 2);
        builder.Property(x => x.Margin).HasPrecision(18, 2);
        builder.Property(x => x.FreeMargin).HasPrecision(18, 2);
        builder.Property(x => x.ClosedTradePnL).HasPrecision(18, 2);

        // Currency: required, max 10 chars (e.g. "USD", "EUR")
        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        // Snapshots are immutable history — no UpdatedAt constraint needed
        builder.HasOne(x => x.TradingAccount)
            .WithMany()
            .HasForeignKey(x => x.TradingAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
