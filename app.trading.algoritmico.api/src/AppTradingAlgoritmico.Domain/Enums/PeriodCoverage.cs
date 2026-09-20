namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// One calendar month's coverage classification inside the cost-decomposition window (design D2,
/// D11), derived purely from the zero/non-zero demo and backtest trade counts for that month — no
/// magnitude rule, no minimum-count rule. <see cref="NoTradesEitherSide"/> is reachable because the
/// window is the DENSE calendar-month span from the earliest to the latest open timestamp across
/// the union of both sides, not the set of months that happen to contain a trade — an interior
/// month with zero trades on either side is classified, never omitted.
/// </summary>
public enum PeriodCoverage
{
    NoTradesEitherSide = 0,
    BothSidesTraded,
    DemoOnlyNoBacktestTrades,
    BacktestOnlyNoDemoTrades,
}
