using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Read side of the FTMO group picker (ftmo-group-simulation, design.md D8): the strategies of ONE account with
/// the per-run facts that decide whether they can join a group.
/// </summary>
public interface IFtmoGroupCandidatesReadService
{
    Task<FtmoGroupCandidatesDto> GetCandidatesAsync(Guid tradingAccountId, CancellationToken ct);
}
