namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// One month's compounding return for a strategy.
/// `ReturnPercent` is computed against `EquityStart` (the equity at the start of the month),
/// so values compound month-over-month — matching SQX-style monthly performance tables.
/// </summary>
public sealed record MonthlyReturnDto(
    int Year,
    int Month,
    decimal EquityStart,
    decimal EquityEnd,
    decimal Profit,
    decimal ReturnPercent,
    int TradeCount);
