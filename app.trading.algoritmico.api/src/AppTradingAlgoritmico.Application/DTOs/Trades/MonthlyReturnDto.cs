namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// One month's performance bucket for a strategy or portfolio.
/// `ReturnPercent` is computed against `EquityStart` (the equity at the start of the month),
/// so values compound month-over-month — matching SQX-style monthly performance tables.
/// </summary>
/// <param name="MaxDrawdownPercent">
/// Worst drawdown produced INSIDE this month: the peak resets to <paramref name="EquityStart"/>
/// on the first of the month, so the value answers "how much did THIS month hurt" and is 0 for a
/// month that only went up. A drawdown straddling a month boundary is split across both buckets.
/// </param>
/// <param name="UnderwaterPercent">
/// Deepest distance below the all-time equity peak reached during this month, with the peak carried
/// across the whole series (same convention as the headline Max DD column). Seeded with the month's
/// opening state, so a month that merely recovers still reports the depth it inherited. A single
/// drawdown therefore repeats across every month until a new high is made — that is the metric's
/// intent, not a defect.
/// </param>
public sealed record MonthlyReturnDto(
    int Year,
    int Month,
    decimal EquityStart,
    decimal EquityEnd,
    decimal Profit,
    decimal ReturnPercent,
    int TradeCount,
    decimal MaxDrawdownPercent,
    decimal UnderwaterPercent,
    int WinCount,
    int LossCount);
