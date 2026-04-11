using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class Strategy : BaseEntity
{
    /// <summary>Strategy name from SQX (e.g. "Strategy 1.3.260")</summary>
    public required string Name { get; set; }

    /// <summary>Human-readable pseudocode extracted from settings.xml</summary>
    public string? Pseudocode { get; set; }

    // --- KPIs (manual input for now, nullable) ---

    public decimal? SharpeRatio { get; set; }
    public decimal? ReturnDrawdownRatio { get; set; }
    public decimal? WinRate { get; set; }
    public decimal? ProfitFactor { get; set; }
    public int? TotalTrades { get; set; }
    public decimal? NetProfit { get; set; }
    public decimal? MaxDrawdown { get; set; }

    public Guid BatchStageId { get; set; }
    public BatchStage BatchStage { get; set; } = null!;
}
