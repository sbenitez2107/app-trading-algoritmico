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
    /// <summary>
    /// Only the coverage component was produced; swap, embedded cost and residual do not exist on
    /// this readout.
    /// <para>
    /// As of PR B2, this member is UNREACHABLE from production code: <c>GetAsync</c>'s three exit
    /// paths are <see cref="NoRunForKind"/>, <see cref="NoDemoTrades"/>, and a hardcoded
    /// <see cref="Decomposed"/> — none of them return <c>CoverageComponentOnly</c> anymore. It is
    /// deliberately RETAINED anyway, purely to hold the zero slot with the value that asserts the
    /// LEAST about a decomposition's completeness. It MUST NOT be deleted as dead code: removing it
    /// would promote <see cref="Decomposed"/> to value 0, making a default-initialized
    /// <see cref="CostDecompositionStatus"/> silently assert a COMPLETE decomposition — the most
    /// dangerous possible default, and the same optimistic-enum-zero hazard this repo has already
    /// been bitten by twice (<c>PlatformType.MT4 = 0</c>, <c>FundingService.Other = 0</c>).
    /// </para>
    /// </summary>
    CoverageComponentOnly = 0,

    /// <summary>All four components — coverage, swap, embedded cost, and the execution residual — were produced. Produced by PR B2.</summary>
    Decomposed,

    /// <summary>The strategy has no demo (<c>StrategyTrade</c>) rows at all; no window or coverage rows are produced.</summary>
    NoDemoTrades,

    /// <summary>No <c>BacktestRun</c> exists for the caller-named <c>BacktestRunKind</c> slot; no window, coverage row, or cost figure is produced.</summary>
    NoRunForKind,
}
