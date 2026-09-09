namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discloses HOW a reported net P/L figure was computed, per side. Mirrors
/// <see cref="ComparabilityBasis"/> and <see cref="BreachBasis"/> — a caveat encoded as a type
/// rather than a comment, so a call site cannot drop it.
/// <para>
/// The two sides do NOT compute "net" the same way, and this enum exists so that asymmetry cannot
/// be flattened into a single implied basis: a demo trade records the pure price move in
/// <c>Profit</c> with its costs in separate signed columns, while a backtest <c>Profit</c> already
/// carries commission and spread and carries no swap at all. The figures are therefore reported
/// separately and are NOT comparable to one another (`demo-backtest-comparability` spec,
/// Requirement "The Trade-Set Difference Is Reported As A Set Relationship, Never As A Score").
/// </para>
/// </summary>
public enum NetPlBasis
{
    /// <summary>
    /// Demo side: <c>Profit + Commission + Swap + Taxes</c>, the cost columns being signed — the
    /// same per-trade net used by <c>AnalyticsSeries.NetOf</c>.
    /// </summary>
    DemoProfitPlusCommissionSwapTaxes = 0,

    /// <summary>
    /// Backtest side: the stored <c>Profit</c> exactly as imported. It ALREADY includes commission
    /// and spread, so nothing is added to it, and it EXCLUDES swap, which SQX does not model — an
    /// omission this figure discloses rather than repairs.
    /// </summary>
    BacktestProfitInclusiveOfCommissionAndSpreadExcludingSwap = 1,
}
