using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class AnalyzerRule : BaseEntity
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}
