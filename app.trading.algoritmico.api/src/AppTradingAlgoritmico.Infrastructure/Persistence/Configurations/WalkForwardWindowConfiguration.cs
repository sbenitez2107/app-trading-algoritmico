using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class WalkForwardWindowConfiguration : IEntityTypeConfiguration<WalkForwardWindow>
{
    public void Configure(EntityTypeBuilder<WalkForwardWindow> builder)
    {
        builder.ToTable("WalkForwardWindows");

        builder.HasKey(x => x.Id);

        // The export's row order is meaningful — OosFromDate is the SECOND-TO-LAST row's OOS start
        // — so the ordinal is part of the key, not incidental.
        builder.HasIndex(x => new { x.ExportId, x.RowIndex }).IsUnique();

        builder.Property(x => x.Parameters).IsRequired().HasMaxLength(BacktestFieldLengths.WalkForwardParameters);

        // Money and ratios — (18,4). The export publishes 2 decimals; the extra headroom costs
        // nothing and keeps a future export from silently rounding.
        builder.Property(x => x.NetProfitIs).HasPrecision(18, 4);
        builder.Property(x => x.RetDdRatioIs).HasPrecision(18, 4);
        builder.Property(x => x.DrawdownIs).HasPrecision(18, 4);
        builder.Property(x => x.AvgTradesPerMonthIs).HasPrecision(18, 4);
        builder.Property(x => x.NetProfitOos).HasPrecision(18, 4);
        builder.Property(x => x.RetDdRatioOos).HasPrecision(18, 4);
        builder.Property(x => x.DrawdownOos).HasPrecision(18, 4);
        builder.Property(x => x.AvgTradesPerMonthOos).HasPrecision(18, 4);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
