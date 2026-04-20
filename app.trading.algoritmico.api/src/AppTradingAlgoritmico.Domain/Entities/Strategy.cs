using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class Strategy : BaseEntity
{
    public required string Name { get; set; }
    public string? Pseudocode { get; set; }

    // --- Indicator metadata (extracted from SQX settings.xml) ---
    public string? EntryIndicators { get; set; }
    public string? PriceIndicators { get; set; }
    public string? IndicatorParameters { get; set; }

    // --- Backtest metadata (from SQX report header) ---
    public string? Symbol { get; set; }
    public string? Timeframe { get; set; }
    public DateTime? BacktestFrom { get; set; }
    public DateTime? BacktestTo { get; set; }

    // --- Summary: top-left ---
    public decimal? TotalProfit { get; set; }
    public decimal? ProfitInPips { get; set; }
    public decimal? YearlyAvgProfit { get; set; }
    public decimal? YearlyAvgReturn { get; set; }
    public decimal? Cagr { get; set; }

    // --- Summary: grid ---
    public int? NumberOfTrades { get; set; }
    public decimal? SharpeRatio { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal? ReturnDrawdownRatio { get; set; }
    public decimal? WinningPercentage { get; set; }
    public decimal? Drawdown { get; set; }
    public decimal? DrawdownPercent { get; set; }
    public decimal? DailyAvgProfit { get; set; }
    public decimal? MonthlyAvgProfit { get; set; }
    public decimal? AverageTrade { get; set; }
    public decimal? AnnualReturnMaxDdRatio { get; set; }
    public decimal? RExpectancy { get; set; }
    public decimal? RExpectancyScore { get; set; }
    public decimal? StrQualityNumber { get; set; }
    public decimal? SqnScore { get; set; }

    // --- Stats: Strategy ---
    public decimal? WinsLossesRatio { get; set; }
    public decimal? PayoutRatio { get; set; }
    public decimal? AverageBarsInTrade { get; set; }
    public decimal? Ahpr { get; set; }
    public decimal? ZScore { get; set; }
    public decimal? ZProbability { get; set; }
    public decimal? Expectancy { get; set; }
    public decimal? Deviation { get; set; }
    public decimal? Exposure { get; set; }
    public int? StagnationInDays { get; set; }
    public decimal? StagnationPercent { get; set; }

    // --- Stats: Trades ---
    public int? NumberOfWins { get; set; }
    public int? NumberOfLosses { get; set; }
    public int? NumberOfCancelled { get; set; }
    public decimal? GrossProfit { get; set; }
    public decimal? GrossLoss { get; set; }
    public decimal? AverageWin { get; set; }
    public decimal? AverageLoss { get; set; }
    public decimal? LargestWin { get; set; }
    public decimal? LargestLoss { get; set; }
    public int? MaxConsecutiveWins { get; set; }
    public int? MaxConsecutiveLosses { get; set; }
    public decimal? AverageConsecutiveWins { get; set; }
    public decimal? AverageConsecutiveLosses { get; set; }
    public decimal? AverageBarsInWins { get; set; }
    public decimal? AverageBarsInLosses { get; set; }

    public Guid? BatchStageId { get; set; }
    public BatchStage? BatchStage { get; set; }

    public Guid? TradingAccountId { get; set; }
    public TradingAccount? TradingAccount { get; set; }

    public ICollection<StrategyMonthlyPerformance> MonthlyPerformance { get; set; } = [];
    public ICollection<StrategyComment> Comments { get; set; } = [];
}
