using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class BacktestRunConfiguration : IEntityTypeConfiguration<BacktestRun>
{
    public void Configure(EntityTypeBuilder<BacktestRun> builder)
    {
        builder.ToTable("BacktestRuns");

        builder.HasKey(x => x.Id);

        // Identity is the SLOT. A strategy has at most one Deploy run and one Evaluation run, and
        // re-importing into an occupied slot replaces it rather than creating a second row.
        builder.HasIndex(x => new { x.StrategyId, x.Kind }).IsUnique();

        // ContentHash is a DE-DUP key and is deliberately NOT unique. The same bytes legitimately
        // back two runs when one SQX strategy is deployed under two Strategy rows; a unique index
        // here would fail the second account with an opaque constraint violation. Calibration is
        // the consumer — it counts one run per distinct hash (design.md D4) — and it needs the
        // index to do that without a scan.
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(BacktestFieldLengths.ContentHash);
        builder.HasIndex(x => x.ContentHash);

        builder.Property(x => x.SourceFileName).IsRequired().HasMaxLength(BacktestFieldLengths.FileNameOrKey);
        builder.Property(x => x.Symbol).HasMaxLength(BacktestFieldLengths.Symbol);
        builder.Property(x => x.Kind).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasMany(x => x.Trades)
            .WithOne(t => t.BacktestRun)
            .HasForeignKey(t => t.BacktestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Attribution. Declared WITHOUT a navigation property on BacktestRun: the importer's
        // persistence surface must keep no compile-time path from a run to a tracked Strategy
        // (design.md D2). Cascade is deliberate and is a behaviour change from the previous
        // revision — deleting a strategy now removes its runs and, through the relationship above,
        // their trades, instead of leaving orphaned runs behind a stale "matched" status.
        builder.HasOne<Strategy>()
            .WithMany()
            .HasForeignKey(x => x.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
