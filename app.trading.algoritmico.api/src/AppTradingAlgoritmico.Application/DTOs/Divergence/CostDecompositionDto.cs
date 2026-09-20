using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Divergence;

/// <summary>
/// The cost decomposition for one strategy and one caller-named <see cref="BacktestRunKind"/> slot
/// (`demo-backtest-cost-decomposition` spec, slice B). This is PR B1's shape: it carries slice A's
/// <see cref="Comparability"/> figures forward verbatim (spec "The Comparability Gate Is A
/// Structural Ordering Dependency…") plus the <see cref="Coverage"/> component, and PHYSICALLY
/// OMITS the swap, embedded-cost and residual members — not nulls, absent members entirely (design
/// D9). A field that does not exist cannot silently report an incomplete residual.
/// <see cref="Status"/> discloses which components were producible at all;
/// <see cref="CostDecompositionStatus.CoverageComponentOnly"/> is this PR's own disclosure.
/// </summary>
public sealed record CostDecompositionDto(
    Guid StrategyId,
    BacktestRunKind Kind,
    CostDecompositionStatus Status,
    PriceOffsetComparabilityDto Comparability,
    CoverageComponentDto Coverage,
    SwapComponentDto? Swap = null,
    EmbeddedCostComponentDto? EmbeddedCost = null,
    ExecutionResidualDto? Residual = null);

/// <summary>
/// The sum of <c>StrategyTrade.Swap</c> across the demo trade set for the window, isolated from
/// slice A's netting (spec "Swap Is Reported As An Isolated Component, Never Netted Silently").
/// <see cref="TotalSwap"/> is <c>0</c> (not <c>null</c>) when no trade pays swap, and <c>null</c>
/// only when the underlying demo trade set is empty.
/// </summary>
public sealed record SwapComponentDto(
    decimal? TotalSwap,
    int SwapPayingTradeCount,
    int TotalTradeCount);

/// <summary>
/// The embedded-cost readiness of the backtest symbol's calibration and, when and only when
/// <see cref="EmbeddedCostAvailability.Calibrated"/>, the derived estimate (spec "Embedded Backtest
/// Cost Surfaces Four Calibration States Distinctly, With No Fallback"; design D8). Every field
/// except <see cref="State"/> is <c>null</c> unless <see cref="State"/> is <c>Calibrated</c> — no
/// path ever substitutes, assumes, or falls back to a point value. <see cref="CalibratedAt"/> is
/// echoed verbatim and compared to nothing (no staleness/TTL concept exists).
/// </summary>
public sealed record EmbeddedCostComponentDto(
    EmbeddedCostAvailability State,
    decimal? PointValue,
    int? SampleCount,
    decimal? EmbeddedCostEstimate,
    DateTime? CalibratedAt);

/// <summary>
/// The execution residual over the exact-minute-paired subset only (spec "The Execution Residual Is
/// Computed Over The Paired Subset Only"; design D10). <see cref="DemoOnlyNetPl"/> and
/// <see cref="BacktestOnlyNetPl"/> travel alongside it for context and are PROVABLY NOT summands —
/// see <see cref="Basis"/>.
/// </summary>
public sealed record ExecutionResidualDto(
    decimal? Residual,
    decimal? DemoOnlyNetPl,
    decimal? BacktestOnlyNetPl,
    decimal? PairedDemoNetPl,
    decimal? PairedBacktestNetPl)
{
    /// <summary>
    /// Non-nullable, non-droppable disclosure of what <see cref="Residual"/> may and may not claim:
    /// the paired subset's demo-versus-backtest difference after swap and embedded cost are
    /// removed — not slippage, not a strategy-quality score, and not an accounting for the
    /// M1-versus-tick intrabar path assumption (design D10; spec "ResidualBasis states the claim
    /// boundary on the type").
    /// </summary>
    public ResidualBasis Basis => ResidualBasis.PairedSubsetAfterSwapAndEmbeddedCost;
}

/// <summary>
/// Every calendar month in the dense window (spec Definitions), classified per
/// <see cref="Domain.Enums.PeriodCoverage"/>. <see cref="CoverageBasis"/> is a non-nullable,
/// non-droppable computed disclosure — see <see cref="CoverageMonthDto"/> class remarks for what it
/// covers.
/// </summary>
public sealed record CoverageComponentDto(
    IReadOnlyList<CoverageMonthDto> Months)
{
    /// <summary>
    /// Non-nullable, non-droppable disclosure that every coverage-gap label in <see cref="Months"/>
    /// is a presumption from the absence of backtest trades, never a confirmed finding — covering
    /// both the one-sided cases and the <see cref="PeriodCoverage.NoTradesEitherSide"/> case, where
    /// the ambiguity of "no signal" versus "no data" is disclosed, not resolved (spec "Data Coverage
    /// Discloses Itself As A Presumption From Absence, Never As A Proof").
    /// </summary>
    public CoverageBasis CoverageBasis => CoverageBasis.PresumedFromBacktestTradeAbsence;
}

/// <summary>
/// One calendar month inside the dense window. <see cref="DemoOpenTimes"/>/<see cref="BacktestOpenTimes"/>
/// carry the underlying open timestamps verbatim, <see cref="DateTimeKind"/> untouched, with no
/// timezone conversion — an empty (<see cref="PeriodCoverage.NoTradesEitherSide"/>) month reports
/// empty lists, never <c>null</c> lists (spec requirement text).
/// </summary>
public sealed record CoverageMonthDto(
    int Year,
    int Month,
    PeriodCoverage PeriodCoverage,
    IReadOnlyList<DateTime> DemoOpenTimes,
    IReadOnlyList<DateTime> BacktestOpenTimes,
    int DemoTradeCount,
    int BacktestTradeCount);
