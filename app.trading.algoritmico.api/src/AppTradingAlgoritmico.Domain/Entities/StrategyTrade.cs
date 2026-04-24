using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class StrategyTrade : BaseEntity
{
    public Guid StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public long Ticket { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }

    /// <summary>Order type as reported by MT4 (e.g. "buy", "sell").</summary>
    public string Type { get; set; } = string.Empty;

    public decimal Size { get; set; }

    /// <summary>Symbol/item as reported by the broker — stored as-is, no normalization.</summary>
    public string Item { get; set; } = string.Empty;

    public decimal OpenPrice { get; set; }
    public decimal? ClosePrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal Commission { get; set; }
    public decimal Taxes { get; set; }
    public decimal Swap { get; set; }
    public decimal Profit { get; set; }

    /// <summary>Mapped close reason: SL, TP, Other, or null when not applicable.</summary>
    public string? CloseReason { get; set; }

    /// <summary>Persisted as plain bit column. Set by the import service on every upsert (true when CloseTime == null).</summary>
    public bool IsOpen { get; set; }
}
