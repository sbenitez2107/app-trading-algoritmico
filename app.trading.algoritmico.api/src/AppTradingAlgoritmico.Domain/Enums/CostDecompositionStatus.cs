namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discloses which component of the cost decomposition was producible at all for a given strategy
/// and run kind (design D9). <c>CoverageComponentOnly = 0</c> is the member that asserts least,
/// mirroring <see cref="ComparabilityReadoutStatus.NoPairedOpens"/>: it is what makes PR B1
/// shippable on its own — B1 emits this status and its DTO physically omits the swap,
/// embedded-cost and residual members, so a field that does not exist cannot silently report an
/// incomplete residual. <see cref="Decomposed"/> is produced only once PR B2 adds those members
/// purely additively.
/// </summary>
public enum CostDecompositionStatus
{
    /// <summary>Only the coverage component was produced; swap, embedded cost and residual do not exist on this readout.</summary>
    CoverageComponentOnly = 0,

    /// <summary>All four components — coverage, swap, embedded cost, and the execution residual — were produced. Produced by PR B2.</summary>
    Decomposed,

    /// <summary>The strategy has no demo (<c>StrategyTrade</c>) rows at all; no window or coverage rows are produced.</summary>
    NoDemoTrades,

    /// <summary>No <c>BacktestRun</c> exists for the caller-named <c>BacktestRunKind</c> slot; no window, coverage row, or cost figure is produced.</summary>
    NoRunForKind,
}
