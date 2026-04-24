using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class TradingAccountConfiguration : IEntityTypeConfiguration<TradingAccount>
{
    public void Configure(EntityTypeBuilder<TradingAccount> builder)
    {
        builder.ToTable("TradingAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Broker)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AccountType)
            .IsRequired();

        builder.Property(x => x.Platform)
            .IsRequired();

        builder.Property(x => x.AccountNumber)
            .IsRequired();

        builder.Property(x => x.Login)
            .IsRequired();

        builder.Property(x => x.PasswordEncrypted)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Server)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Currency)
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        // Index for fast broker+type filtering
        builder.HasIndex(x => new { x.Broker, x.AccountType });
    }
}
