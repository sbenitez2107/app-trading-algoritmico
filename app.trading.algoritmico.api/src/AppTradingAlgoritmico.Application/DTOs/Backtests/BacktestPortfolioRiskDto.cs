using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// Value-at-Risk over a group of BACKTEST-derived dated series (design.md D4/D4a/D4c/D5).
/// <para>
/// <b>A NEW DTO, not a widened shipped one.</b> <c>PortfolioRiskDto.Var95</c>/<c>Var99</c> are
/// non-nullable <see cref="decimal"/>, so reusing them would FORCE a <c>0</c> for a figure the data
/// cannot support — the exact failure this slice exists to prevent. Widening the shipped record
/// instead would move a shipped contract and put the "no shipped number changes" property at risk.
/// </para>
/// <para>
/// Every VaR field is <c>decimal?</c> and every one is paired with a
/// <see cref="VarWithholdReason"/>. A withheld figure is <c>null</c>, NEVER <c>0</c>, and the
/// density that withheld it is reported beside it.
/// </para>
/// </summary>
/// <param name="InitialCapital">The percentage denominator, reported so the percentages can be read.</param>
/// <param name="Method">Always <c>"Historical"</c> — a percentile of realized closes, no model.</param>
/// <param name="WindowDays">
/// Always <c>0</c>: NO trim. The shipped live path trims to the most recent 250 observations, which
/// answers "what is my risk now" — the right question for a live account and the wrong one for a
/// fixed backtest sample. Trimming IST to 250 leaves 5 negative days against a threshold of 13,
/// a completely different gate evaluation over ~7% of the data.
/// </param>
/// <param name="ObservationDays">Elements actually used — equal to <c>Density.DenseDayCount</c> because nothing is trimmed.</param>
/// <param name="Segment">Which sample every figure below was computed over. Metadata, never a filter.</param>
/// <param name="MonthlyVarOverlappingWindows">Rolling 30-day windows available: <c>ObservationDays - 30 + 1</c>.</param>
/// <param name="MonthlyVarIndependentWindows">Non-overlapping equivalent, reported for the overlap caveat.</param>
/// <param name="VarTarget">
/// The shipped band readout, or null. This slice never DERIVES a band position from its own
/// currency figure: the KB's target-VaR determination walks up to six months of historical VaR
/// until the max/min ratio reaches 2:1, over the last 45 days of OPEN positions, and neither is
/// implemented here. The only percentage published is <c>MonthlyVar95 / InitialCapital</c>.
/// </param>
public sealed record BacktestPortfolioRiskDto(
    decimal InitialCapital,
    string Method,
    int WindowDays,
    int ObservationDays,
    BacktestSegment Segment,
    decimal? DailyVar95,
    decimal? DailyVar95Percent,
    VarWithholdReason DailyVar95Withheld,
    decimal? DailyVar99,
    decimal? DailyVar99Percent,
    VarWithholdReason DailyVar99Withheld,
    decimal? MonthlyVar95,
    decimal? MonthlyVar95Percent,
    VarWithholdReason MonthlyVar95Withheld,
    int MonthlyVarOverlappingWindows,
    int MonthlyVarIndependentWindows,
    SeriesDensityDto Density,
    IReadOnlyList<BacktestServiceRiskDto> ByService,
    VarTargetReadoutDto? VarTarget);
