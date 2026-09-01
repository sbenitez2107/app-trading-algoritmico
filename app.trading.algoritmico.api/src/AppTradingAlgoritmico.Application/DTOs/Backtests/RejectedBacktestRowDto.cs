namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>A single data row excluded from persistence, with the reason it was rejected.</summary>
public sealed record RejectedBacktestRowDto(int RowIndex, string Reason);
