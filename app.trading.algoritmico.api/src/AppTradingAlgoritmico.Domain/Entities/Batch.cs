using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

public class Batch : BaseEntity
{
    /// <summary>Optional display name for this batch</summary>
    public string? Name { get; set; }

    /// <summary>Timeframe for this batch (H1, H4, etc.)</summary>
    public Timeframe Timeframe { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public Guid BuildingBlockId { get; set; }
    public BuildingBlock BuildingBlock { get; set; } = null!;

    public ICollection<BatchStage> Stages { get; set; } = [];
}
