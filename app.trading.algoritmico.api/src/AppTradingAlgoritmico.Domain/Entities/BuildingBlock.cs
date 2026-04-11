using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

public class BuildingBlock : BaseEntity
{
    /// <summary>Display name (e.g. "BB1 IMOX Base")</summary>
    public required string Name { get; set; }

    /// <summary>What this BB focuses on (e.g. "Generalista", "Seguimiento de tendencia")</summary>
    public string? Description { get; set; }

    /// <summary>BB classification type</summary>
    public BuildingBlockType Type { get; set; }

    /// <summary>Raw XML config extracted from the .sqb file</summary>
    public string? XmlConfig { get; set; }

    public ICollection<Batch> Batches { get; set; } = [];
}
