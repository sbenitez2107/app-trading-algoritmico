using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Read side of the FTMO multi-start replay (ftmo-multi-start, design.md Data Flow, PR4). Reuses the
/// shipped guard chain and projection (<c>FtmoSimulationInputs</c>) — the same refusal path as
/// <see cref="IFtmoBreachSimulationReadService"/>, never a second, divergent one.
/// </summary>
public interface IFtmoMultiStartReadService
{
    Task<FtmoMultiStartDto> SimulateAsync(FtmoBreachSimulationRequest request, CancellationToken ct);
}
