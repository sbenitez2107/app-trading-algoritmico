using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class BacktestTradeConfiguration : IEntityTypeConfiguration<BacktestTrade>
{
    public void Configure(EntityTypeBuilder<BacktestTrade> builder)
    {
        builder.ToTable("BacktestTrades");

        builder.HasKey(x => x.Id);

        // Unique key — the only identifier the export guarantees. Never Ticket (see class remarks).
        builder.HasIndex(x => new { x.BacktestRunId, x.RowIndex }).IsUnique();

        // Informational only — 27 verified collisions across the two committed fixtures.
        builder.HasIndex(x => x.Ticket);

        // Slice 3 filters by segment without re-parsing SampleTypeRaw.
        builder.HasIndex(x => new { x.BacktestRunId, x.Segment });

        // The grid's readiness marker asks "does this run have a trade at or after the walk-forward
        // boundary?" once per page load, over every run of the page's strategies. Without this the
        // aggregate scans the whole trade table (design.md D12).
        builder.HasIndex(x => new { x.BacktestRunId, x.CloseTime });

        // Widths come from BacktestFieldLengths, which the CSV parser also reads: an
        // over-length value is rejected as a named row while it is still data, instead of
        // failing here as a non-transient "String or binary data would be truncated".
        builder.Property(x => x.Symbol).IsRequired().HasMaxLength(BacktestFieldLengths.Symbol);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(BacktestFieldLengths.TradeType);
        builder.Property(x => x.SampleTypeRaw).IsRequired().HasMaxLength(BacktestFieldLengths.SampleTypeRaw);
        builder.Property(x => x.CloseType).IsRequired().HasMaxLength(BacktestFieldLengths.CloseType);
        builder.Property(x => x.Comment).HasMaxLength(BacktestFieldLengths.Comment);

        // Prices — (18,5), diverges from StrategyTrade's (18,2): the export carries 5 decimals.
        builder.Property(x => x.OpenPrice).HasPrecision(18, 5);
        builder.Property(x => x.ClosePrice).HasPrecision(18, 5);
        builder.Property(x => x.StopLoss).HasPrecision(18, 5);
        builder.Property(x => x.Size).HasPrecision(18, 5);

        // Money — (18,2).
        builder.Property(x => x.Profit).HasPrecision(18, 2);
        builder.Property(x => x.Balance).HasPrecision(18, 2);
        builder.Property(x => x.RealizedRisk).HasPrecision(18, 2);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
