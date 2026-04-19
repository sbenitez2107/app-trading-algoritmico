using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class StrategyGridPreset : BaseEntity
{
    public required string Name { get; set; }
    public required Guid UserId { get; set; }

    /// <summary>JSON array of visible column field names, e.g. ["totalProfit","sharpeRatio"]</summary>
    public required string VisibleColumnsJson { get; set; }

    /// <summary>JSON array of ordered field names defining column display order.</summary>
    public required string ColumnOrderJson { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
