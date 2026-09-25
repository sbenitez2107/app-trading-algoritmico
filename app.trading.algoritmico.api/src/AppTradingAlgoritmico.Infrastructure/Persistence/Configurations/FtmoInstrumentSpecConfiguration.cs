using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class FtmoInstrumentSpecConfiguration : IEntityTypeConfiguration<FtmoInstrumentSpec>
{
    public void Configure(EntityTypeBuilder<FtmoInstrumentSpec> builder)
    {
        builder.ToTable("FtmoInstrumentSpecs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SqxSymbol).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.SqxSymbol).IsUnique();

        builder.Property(x => x.FtmoSymbol).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContractSize).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.ProfitCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.SizeDecimals).IsRequired();
        builder.Property(x => x.Step).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.MinLot).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.MaxLots).IsRequired().HasPrecision(18, 6);
        builder.Property(x => x.SourceTimeZoneId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Provenance).IsRequired();
        builder.Property(x => x.CapturedOn).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
