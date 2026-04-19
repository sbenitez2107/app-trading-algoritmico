using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class StrategyComment : BaseEntity
{
    public Guid StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public required string Content { get; set; }
}
