using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class StrategyMonthlyPerformance : BaseEntity
{
    public Guid StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Profit { get; set; }
}
