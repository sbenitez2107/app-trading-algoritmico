using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>Read side of the FTMO 2-Step breach simulation (PR P4, design.md Data Flow).</summary>
public interface IFtmoBreachSimulationReadService
{
    Task<FtmoBreachSimulationDto> SimulateAsync(FtmoBreachSimulationRequest request, CancellationToken ct);
}
