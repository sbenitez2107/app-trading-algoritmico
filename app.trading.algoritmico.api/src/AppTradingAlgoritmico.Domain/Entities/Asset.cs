using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class Asset : BaseEntity
{
    /// <summary>Display name (e.g. "Oro", "Nasdaq", "DAX")</summary>
    public required string Name { get; set; }

    /// <summary>Trading symbol (e.g. "XAUUSD", "NQ", "DE40")</summary>
    public required string Symbol { get; set; }

    public ICollection<Batch> Batches { get; set; } = [];
}
