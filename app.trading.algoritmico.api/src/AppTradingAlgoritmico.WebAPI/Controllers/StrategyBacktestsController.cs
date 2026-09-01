using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

/// <summary>
/// Nested-resource controller for the three artifacts a strategy can own: a Deploy run, an
/// Evaluation run and a walk-forward export. Route: <c>api/strategies/{strategyId}</c>, following
/// the <see cref="TradingAccountStrategiesController"/> precedent — <see cref="StrategiesController"/>
/// stays at its existing endpoint count.
/// <para>
/// Attribution is the ROUTE. The strategy is known before a single byte of the file is read, which
/// is the whole point of the revision: there is no filename convention, no name matching, and no
/// way to produce a run that belongs to nobody. See design.md D7.
/// </para>
/// </summary>
[ApiController]
[Route("api/strategies/{strategyId:guid}")]
[Authorize]
[Produces("application/json")]
public class StrategyBacktestsController(
    IBacktestImportService importService,
    IWalkForwardImportService walkForwardImportService,
    IBacktestReadService readService) : ControllerBase
{
    private const string AllowedExtension = ".csv";

    /// <summary>
    /// Imports one trade-list CSV into the strategy's <c>deploy</c> or <c>evaluation</c> slot.
    /// <para>
    /// The kind is a ROUTE SEGMENT rather than a form field so the declaration is visible in the
    /// URL, in access logs and in the route table, and so an unknown value is refused before
    /// anything is read. Whether the file's actual parameters match the declared kind is
    /// deliberately NOT checked: nothing in a 16-column trade list identifies the parameters that
    /// produced it, so a check here would be a guess dressed as a validation (design.md D11).
    /// </para>
    /// </summary>
    [HttpPost("backtests/{kind}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BacktestImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BacktestImportResultDto>> ImportTradeList(
        [FromRoute] Guid strategyId,
        [FromRoute] string kind,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (!TryParseKind(kind, out var runKind))
        {
            return BadRequest(new
            {
                message = $"Unknown backtest kind '{kind}'. Expected 'deploy' or 'evaluation'.",
            });
        }

        if (!TryAcceptCsv(file, out var upload, out var error))
            return BadRequest(new { message = error });

        return Ok(await importService.ImportTradeListAsync(strategyId, runKind, upload!, ct));
    }

    /// <summary>
    /// Imports the strategy's SQX Optimizer walk-forward export. A strategy has at most one, so a
    /// re-import replaces it and recomputes the out-of-sample boundary.
    /// </summary>
    [HttpPost("walk-forward")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(WalkForwardImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WalkForwardImportResultDto>> ImportWalkForward(
        [FromRoute] Guid strategyId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (!TryAcceptCsv(file, out var upload, out var error))
            return BadRequest(new { message = error });

        return Ok(await walkForwardImportService.ImportAsync(strategyId, upload!, ct));
    }

    /// <summary>Both run slots and the walk-forward export currently held by one strategy.</summary>
    [HttpGet("backtests")]
    [ProducesResponseType(typeof(StrategyBacktestsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategyBacktestsDto>> GetBacktests(
        [FromRoute] Guid strategyId, CancellationToken ct)
        => Ok(await readService.GetByStrategyAsync(strategyId, ct));

    /// <summary>
    /// Accepts ONLY the two declared names, case-insensitively. Deliberately not
    /// <c>Enum.TryParse</c>: that also accepts the underlying numbers, including values with no
    /// declared member — and <c>0</c> has none, so <c>POST .../backtests/0</c> would have created a
    /// run in a slot that does not exist.
    /// </summary>
    private static bool TryParseKind(string kind, out BacktestRunKind runKind)
    {
        if (string.Equals(kind, "deploy", StringComparison.OrdinalIgnoreCase))
        {
            runKind = BacktestRunKind.Deploy;
            return true;
        }

        if (string.Equals(kind, "evaluation", StringComparison.OrdinalIgnoreCase))
        {
            runKind = BacktestRunKind.Evaluation;
            return true;
        }

        runKind = default;
        return false;
    }

    /// <summary>
    /// Server-side extension whitelist plus <see cref="Path.GetFileName(string)"/> sanitisation,
    /// applied BEFORE the stream is opened. The parser sanitises the name again — defence in depth,
    /// because the name reaches a persisted column.
    /// </summary>
    private static bool TryAcceptCsv(IFormFile? file, out BacktestFileUploadDto? upload, out string? error)
    {
        upload = null;
        error = null;

        if (file is null || file.Length == 0)
        {
            error = "A file is required.";
            return false;
        }

        var sanitizedName = Path.GetFileName(file.FileName);
        if (!string.Equals(Path.GetExtension(sanitizedName), AllowedExtension, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only .csv files are accepted.";
            return false;
        }

        upload = new BacktestFileUploadDto(sanitizedName, file.OpenReadStream());
        return true;
    }
}
