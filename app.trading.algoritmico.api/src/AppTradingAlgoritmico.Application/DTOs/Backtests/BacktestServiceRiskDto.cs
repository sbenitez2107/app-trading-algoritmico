using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// The standalone risk of one funding service within a backtest group — the counterpart of the
/// shipped <c>ServiceRiskDto</c>, which cannot express a withheld figure because its
/// <c>Var95</c> is a non-nullable <see cref="decimal"/> (design.md D5).
/// <para>
/// The density block is this service's own, not the group's: a service whose members are sparse
/// can withhold while the group reports, and the operator has to be able to see which.
/// </para>
/// </summary>
/// <param name="Service">The broker label, or <c>"—"</c> when the members carry none.</param>
/// <param name="StrategyCount">Members grouped under this service.</param>
/// <param name="NetProfit">Sum of the service's dated nets, gross of every unmodelled cost.</param>
public sealed record BacktestServiceRiskDto(
    string Service,
    int StrategyCount,
    decimal NetProfit,
    decimal? DailyVar95,
    decimal? DailyVar95Percent,
    VarWithholdReason DailyVar95Withheld,
    decimal? MonthlyVar95,
    decimal? MonthlyVar95Percent,
    VarWithholdReason MonthlyVar95Withheld,
    int MonthlyVarOverlappingWindows,
    int MonthlyVarIndependentWindows,
    SeriesDensityDto Density);
