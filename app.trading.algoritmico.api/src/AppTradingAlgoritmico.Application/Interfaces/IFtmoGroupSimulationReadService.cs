using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Read side of the FTMO group simulation (ftmo-group-simulation, design.md D5): one caller-chosen group
/// of strategies sharing ONE FTMO account, replayed as one merged series per run kind through the shipped
/// multi-start machinery.
/// </summary>
public interface IFtmoGroupSimulationReadService
{
    Task<FtmoGroupSimulationDto> SimulateAsync(FtmoGroupSimulationParameters parameters, CancellationToken ct);
}
