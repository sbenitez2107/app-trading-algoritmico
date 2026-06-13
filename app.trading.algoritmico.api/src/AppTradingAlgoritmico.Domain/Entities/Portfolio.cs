using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// A platform-level grouping of strategies drawn from ANY trading account / broker.
/// Scoped to a single <see cref="AccountType"/> (Demo or Live) so paper and real results
/// are never mixed inside one risk number. Combined KPIs, equity and VaR are computed
/// on demand from member trades — nothing is precomputed here.
/// </summary>
public class Portfolio : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Platform / broker this portfolio belongs to (e.g. "Darwinex", "FTMO", "Axi"). Members must
    /// be strategies of accounts on this broker. A portfolio lives under one platform's menu.
    /// </summary>
    public string Broker { get; set; } = string.Empty;

    /// <summary>Demo or Live. All member strategies' accounts must share this type.</summary>
    public AccountType AccountType { get; set; }

    /// <summary>
    /// Explicit combined capital baseline for return / drawdown / VaR-as-% calculations.
    /// Cross-account portfolios have no single account balance to derive from, so the user
    /// sets the combined notional once.
    /// </summary>
    public decimal InitialCapital { get; set; }

    /// <summary>Reporting currency for combined money figures (display only).</summary>
    public string BaseCurrency { get; set; } = "USD";

    public ICollection<PortfolioStrategy> Members { get; set; } = [];
}
