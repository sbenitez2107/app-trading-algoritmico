using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// Membership row linking a <see cref="Strategy"/> into a <see cref="Portfolio"/> with an
/// allocation weight. Weights are stored RAW (user intent — equal-weight leaves all at 1.0)
/// and normalized at read time (w_i / Σw). The weight is applied as a per-trade-net multiplier:
/// a 30%-weighted strategy contributes 30% of its dollar P/L — a proxy for lot-scaling the
/// trade data does not carry.
/// </summary>
public class PortfolioStrategy : BaseEntity
{
    public Guid PortfolioId { get; set; }

    public Portfolio Portfolio { get; set; } = null!;

    public Guid StrategyId { get; set; }

    public Strategy Strategy { get; set; } = null!;

    /// <summary>Raw allocation weight (>= 0). Normalized across the portfolio at read time.</summary>
    public decimal Weight { get; set; } = 1m;
}
