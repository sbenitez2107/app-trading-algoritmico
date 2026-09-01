using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyWalkForwardExportConfiguration : IEntityTypeConfiguration<StrategyWalkForwardExport>
{
    public void Configure(EntityTypeBuilder<StrategyWalkForwardExport> builder)
    {
        builder.ToTable("StrategyWalkForwardExports");

        builder.HasKey(x => x.Id);

        // At most one export per strategy — re-import replaces it wholesale.
        builder.HasIndex(x => x.StrategyId).IsUnique();

        builder.Property(x => x.OosFromDate).IsRequired();

        // Widths come from BacktestFieldLengths so the parser and the column cannot drift apart.
        builder.Property(x => x.DeployParameters).IsRequired().HasMaxLength(BacktestFieldLengths.WalkForwardParameters);
        builder.Property(x => x.EvaluationParameters).IsRequired().HasMaxLength(BacktestFieldLengths.WalkForwardParameters);
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(BacktestFieldLengths.ContentHash);
        builder.Property(x => x.SourceFileName).IsRequired().HasMaxLength(BacktestFieldLengths.FileNameOrKey);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasMany(x => x.Windows)
            .WithOne(w => w.Export)
            .HasForeignKey(w => w.ExportId)
            .OnDelete(DeleteBehavior.Cascade);

        // No navigation to Strategy, for the same reason as BacktestRun: the importer must keep no
        // compile-time path to a tracked Strategy (design.md D2).
        builder.HasOne<Strategy>()
            .WithMany()
            .HasForeignKey(x => x.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
