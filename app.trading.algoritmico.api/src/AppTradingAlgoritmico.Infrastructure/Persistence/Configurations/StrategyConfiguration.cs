using AppTradingAlgoritmico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Configurations;

public class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> builder)
    {
        builder.ToTable("Strategies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.Pseudocode)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.EntryIndicators)
            .HasMaxLength(1000);

        builder.Property(x => x.PriceIndicators)
            .HasMaxLength(1000);

        builder.Property(x => x.IndicatorParameters)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Symbol).HasMaxLength(50);
        builder.Property(x => x.Timeframe).HasMaxLength(20);

        // Summary: top-left
        builder.Property(x => x.TotalProfit).HasPrecision(18, 2);
        builder.Property(x => x.ProfitInPips).HasPrecision(18, 4);
        builder.Property(x => x.YearlyAvgProfit).HasPrecision(18, 2);
        builder.Property(x => x.YearlyAvgReturn).HasPrecision(18, 6);
        builder.Property(x => x.Cagr).HasPrecision(18, 6);

        // Summary: grid
        builder.Property(x => x.SharpeRatio).HasPrecision(18, 6);
        builder.Property(x => x.ProfitFactor).HasPrecision(18, 6);
        builder.Property(x => x.ReturnDrawdownRatio).HasPrecision(18, 6);
        builder.Property(x => x.WinningPercentage).HasPrecision(18, 6);
        builder.Property(x => x.Drawdown).HasPrecision(18, 2);
        builder.Property(x => x.DrawdownPercent).HasPrecision(18, 6);
        builder.Property(x => x.DailyAvgProfit).HasPrecision(18, 2);
        builder.Property(x => x.MonthlyAvgProfit).HasPrecision(18, 2);
        builder.Property(x => x.AverageTrade).HasPrecision(18, 2);
        builder.Property(x => x.AnnualReturnMaxDdRatio).HasPrecision(18, 6);
        builder.Property(x => x.RExpectancy).HasPrecision(18, 6);
        builder.Property(x => x.RExpectancyScore).HasPrecision(18, 6);
        builder.Property(x => x.StrQualityNumber).HasPrecision(18, 6);
        builder.Property(x => x.SqnScore).HasPrecision(18, 6);

        // Stats: Strategy
        builder.Property(x => x.WinsLossesRatio).HasPrecision(18, 6);
        builder.Property(x => x.PayoutRatio).HasPrecision(18, 6);
        builder.Property(x => x.AverageBarsInTrade).HasPrecision(18, 4);
        builder.Property(x => x.Ahpr).HasPrecision(18, 6);
        builder.Property(x => x.ZScore).HasPrecision(18, 6);
        builder.Property(x => x.ZProbability).HasPrecision(18, 6);
        builder.Property(x => x.Expectancy).HasPrecision(18, 6);
        builder.Property(x => x.Deviation).HasPrecision(18, 4);
        builder.Property(x => x.Exposure).HasPrecision(18, 6);
        builder.Property(x => x.StagnationPercent).HasPrecision(18, 6);

        // Stats: Trades
        builder.Property(x => x.GrossProfit).HasPrecision(18, 2);
        builder.Property(x => x.GrossLoss).HasPrecision(18, 2);
        builder.Property(x => x.AverageWin).HasPrecision(18, 2);
        builder.Property(x => x.AverageLoss).HasPrecision(18, 2);
        builder.Property(x => x.LargestWin).HasPrecision(18, 2);
        builder.Property(x => x.LargestLoss).HasPrecision(18, 2);
        builder.Property(x => x.AverageConsecutiveWins).HasPrecision(18, 4);
        builder.Property(x => x.AverageConsecutiveLosses).HasPrecision(18, 4);
        builder.Property(x => x.AverageBarsInWins).HasPrecision(18, 4);
        builder.Property(x => x.AverageBarsInLosses).HasPrecision(18, 4);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(256);

        builder.HasOne(x => x.BatchStage)
            .WithMany(bs => bs.Strategies)
            .HasForeignKey(x => x.BatchStageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TradingAccount)
            .WithMany(t => t.Strategies)
            .HasForeignKey(x => x.TradingAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.MonthlyPerformance)
            .WithOne(mp => mp.Strategy)
            .HasForeignKey(mp => mp.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Comments)
            .WithOne(c => c.Strategy)
            .HasForeignKey(c => c.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
