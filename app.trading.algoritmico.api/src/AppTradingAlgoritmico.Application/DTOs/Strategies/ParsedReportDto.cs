namespace AppTradingAlgoritmico.Application.DTOs.Strategies;

public record ParsedReportDto(
    string? Symbol,
    string? Timeframe,
    DateTime? BacktestFrom,
    DateTime? BacktestTo,
    UpdateStrategyKpisDto Kpis,
    IList<MonthlyPerformanceDto> MonthlyPerformance
);
