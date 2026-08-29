namespace AppTradingAlgoritmico.Application.DTOs.Trades;

/// <summary>
/// One point on a strategy's equity curve: running equity walked from the initial balance,
/// plus the drawdown from the running peak, stamped at each closed trade's close time.
/// Same shape as the portfolio equity point so the frontend equity chart is reused as-is.
/// </summary>
public sealed record StrategyEquityPointDto(
    DateTime Date,
    decimal Equity,
    decimal Drawdown,
    decimal DrawdownPercent);
