using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class SymbolCalibrationConfiguration : IEntityTypeConfiguration<SymbolCalibration>
{
    public void Configure(EntityTypeBuilder<SymbolCalibration> builder)
    {
        builder.ToTable("SymbolCalibrations");

        builder.HasKey(x => x.Id);

        // Shares BacktestFieldLengths with the parser and the trade config: this column is written
        // OUTSIDE the per-file boundary, so a drifted width would fail after every file committed.
        builder.Property(x => x.Symbol).IsRequired().HasMaxLength(BacktestFieldLengths.Symbol);
        builder.HasIndex(x => x.Symbol).IsUnique();

        builder.Property(x => x.PointValue).HasPrecision(18, 6);
        builder.Property(x => x.MinObserved).HasPrecision(18, 6);
        builder.Property(x => x.MaxObserved).HasPrecision(18, 6);

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CalibratedAt).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
