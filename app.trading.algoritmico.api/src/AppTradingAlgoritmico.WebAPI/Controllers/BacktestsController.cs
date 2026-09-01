using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

/// <summary>
/// READ-ONLY views over imported SQX/AlgoWizard backtest data: every run across every strategy,
/// one run's trades, and the per-symbol point-value calibrations (which are pooled per SYMBOL
/// across runs, so they belong here rather than on a strategy row).
/// <para>
/// Import lives on <see cref="StrategyBacktestsController"/>, because a run is strategy-scoped at
/// upload time now — the strategy is known from the route before the file is read. See design.md
/// D7.
/// </para>
/// </summary>
[ApiController]
[Route("api/backtests")]
[Authorize]
[Produces("application/json")]
public class BacktestsController(IBacktestReadService service) : ControllerBase
{
    /// <summary>Paged list of imported backtest runs, most recent first.</summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PagedResult<BacktestRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BacktestRunDto>>> GetRuns(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await service.GetRunsAsync(page, pageSize, ct));

    /// <summary>Paged trades for one run, optionally filtered by walk-forward segment.</summary>
    [HttpGet("runs/{id:guid}/trades")]
    [ProducesResponseType(typeof(PagedResult<BacktestTradeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BacktestTradeDto>>> GetTrades(
        Guid id,
        [FromQuery] BacktestSegment? segment,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await service.GetTradesByRunAsync(id, segment, page, pageSize, ct));

    /// <summary>Every symbol's current point-value calibration and evidence.</summary>
    [HttpGet("calibrations")]
    [ProducesResponseType(typeof(IReadOnlyList<SymbolCalibrationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SymbolCalibrationDto>>> GetCalibrations(CancellationToken ct)
        => Ok(await service.GetCalibrationsAsync(ct));
}
