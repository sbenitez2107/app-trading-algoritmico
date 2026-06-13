using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Broker)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AccountType)
            .HasConversion<int>();

        builder.Property(x => x.InitialCapital)
            .HasPrecision(18, 2);

        builder.Property(x => x.BaseCurrency)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Portfolio)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AccountType);
    }
}
