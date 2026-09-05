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

    /// <summary>
    /// Correlation and VaR over ONE caller-named group of strategies, computed over the ONE sample
    /// the caller names (design.md D8/D8a/D8b).
    /// <para>
    /// <b>Every refusal keeps its own status code AND its body.</b> The response is the same
    /// <see cref="GroupRiskAnalysisDto"/> whether the analysis completed or was refused, because the
    /// per-member evidence is what the operator acts on: which member, which run, which weight. A
    /// bare status code would say only that something failed.
    /// </para>
    /// <para>
    /// The 400s are about the REQUEST — it did not name a sample, named the one label that means
    /// "unclassified", named no strategies, or described an impossible lot grid. The 422s are about
    /// the DATA — the rows exist and are readable, but they cannot support the figure that was
    /// asked for. They are separated so a client can tell "fix your query" from "import something".
    /// </para>
    /// </summary>
    [HttpGet("portfolio-risk")]
    [ProducesResponseType(typeof(GroupRiskAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GroupRiskAnalysisDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GroupRiskAnalysisDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GroupRiskAnalysisDto), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GroupRiskAnalysisDto>> GetPortfolioRisk(
        [FromQuery] GroupRiskAnalysisRequest request, CancellationToken ct = default)
    {
        var analysis = await service.GetGroupRiskAnalysisAsync(request, ct);

        return analysis.Status switch
        {
            GroupRiskAnalysisStatus.Completed => Ok(analysis),

            // The request could not name a sample, or named one that means "unclassified".
            GroupRiskAnalysisStatus.SegmentNotSpecified
                or GroupRiskAnalysisStatus.UnknownSegmentNotSelectable
                or GroupRiskAnalysisStatus.NoStrategiesRequested
                or GroupRiskAnalysisStatus.InvalidLotGrid
                or GroupRiskAnalysisStatus.InvalidInitialCapital => BadRequest(analysis),

            GroupRiskAnalysisStatus.StrategyNotFound => NotFound(analysis),

            // The rows are readable but cannot support the figure that was asked for.
            _ => UnprocessableEntity(analysis),
        };
    }
}
