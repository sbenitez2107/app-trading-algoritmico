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
    CoverageComponentDto Coverage);

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
