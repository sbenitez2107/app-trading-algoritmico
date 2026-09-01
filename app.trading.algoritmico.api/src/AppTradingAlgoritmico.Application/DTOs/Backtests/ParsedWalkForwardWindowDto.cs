namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One parsed row of a walk-forward export. The four OOS numerics are nullable because the last
/// row's out-of-sample period has not elapsed: SQX writes the literal <c>N/A</c>, which becomes
/// null and NEVER zero — a zero would make the un-run window look like the worst one in the file.
/// </summary>
public sealed record ParsedWalkForwardWindowDto(
    int RowIndex,
    DateTime PeriodIsStart,
    DateTime PeriodIsEnd,
    DateTime PeriodOosStart,
    DateTime PeriodOosEnd,
    int DaysIs,
    int DaysOos,
    decimal NetProfitIs,
    decimal RetDdRatioIs,
    decimal DrawdownIs,
    decimal AvgTradesPerMonthIs,
    decimal? NetProfitOos,
    decimal? RetDdRatioOos,
    decimal? DrawdownOos,
    decimal? AvgTradesPerMonthOos,
    string Parameters,
    bool IsFutureWindow);
