namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>Transport-agnostic input to <see cref="AppTradingAlgoritmico.Application.Interfaces.IBacktestImportService"/> — no IFormFile dependency in Application.</summary>
public sealed record BacktestFileUploadDto(string FileName, Stream Content);
