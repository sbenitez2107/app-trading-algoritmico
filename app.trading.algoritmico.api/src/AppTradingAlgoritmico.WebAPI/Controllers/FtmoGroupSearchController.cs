using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

/// <summary>
/// ftmo-group-search D7 - start, poll, re-attach to and cancel the single in-memory group search job. Route
/// <c>api/ftmo-simulations/group-search</c>. Nothing is computed in the request thread: a start only queues the job.
/// Shares, headroom and the ceiling are fractions on the wire; risk is money.
/// </summary>
[ApiController]
[Route("api/ftmo-simulations/group-search")]
[Authorize]
[Produces("application/json")]
public class FtmoGroupSearchController(IFtmoGroupSearchJobs jobs) : ControllerBase
{
    private const string LostMessage = "The search job was not found (it is lost when the API restarts).";

    /// <summary>
    /// 400 <c>{message}</c> for a missing or unusable value (nothing starts), 409 <c>{runningJobId}</c> while a job runs,
    /// else 202 with a Location and <c>{jobId, job}</c> (the initial snapshot). An unknown account is refused by the job
    /// itself (Failed), not here: validating it would need a query in the request thread.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Start([FromBody] FtmoGroupSearchRequest? request)
    {
        if (!TryValidate(request, out var error))
            return BadRequest(new { message = error });

        var start = jobs.TryStart(request!);
        if (!start.Started)
            return Conflict(new { runningJobId = start.JobId });

        return Accepted($"/api/ftmo-simulations/group-search/{start.JobId}", new { jobId = start.JobId, job = jobs.Get(start.JobId) });
    }

    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(FtmoGroupSearchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(Guid jobId)
        => jobs.Get(jobId) is { } job ? Ok(job) : NotFound(new { message = LostMessage });

    /// <summary>The retained job (running or the last terminal one): 200, or 204 when none was started since the API began.</summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(FtmoGroupSearchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetCurrent()
        => jobs.GetCurrent() is { } job ? Ok(job) : NoContent();

    /// <summary>Idempotent on a terminal job (204); 404 for an unknown id.</summary>
    [HttpDelete("{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Cancel(Guid jobId)
        => jobs.Cancel(jobId) ? NoContent() : NotFound(new { message = LostMessage });

    private static bool TryValidate(FtmoGroupSearchRequest? r, out string? error)
    {
        error = Check(r);
        return error is null;
    }

    private static string? Check(FtmoGroupSearchRequest? r)
    {
        if (r is null)
            return "A request body is required.";

        if (r.TradingAccountId is null || r.TradingAccountId == Guid.Empty)
            return "'tradingAccountId' is required.";
        if (string.IsNullOrWhiteSpace(r.Broker))
            return "'broker' is required.";
        if (r.InitialCapital is not { } capital)
            return "'initialCapital' is required.";
        if (capital <= 0m)
            return "'initialCapital' must be greater than 0.";
        if (r.TargetRiskPerTrade is not { } risk)
            return "'targetRiskPerTrade' is required.";
        if (risk <= 0m)
            return "'targetRiskPerTrade' must be greater than 0 (a money amount).";

        if (r.MinMembers is not { } min)
            return "'minMembers' is required.";
        if (r.MaxMembers is not { } max)
            return "'maxMembers' is required.";
        if (min < 2)
            return "'minMembers' must be at least 2.";
        if (max > FtmoGroupSimulationLimits.MaxMembers)
            return $"'maxMembers' must be at most {FtmoGroupSimulationLimits.MaxMembers}.";
        if (min > max)
            return "'minMembers' must not be greater than 'maxMembers'.";
        if (r.MaxPerInstrument is not { } perInstrument)
            return "'maxPerInstrument' is required.";
        if (perInstrument < 1)
            return "'maxPerInstrument' must be at least 1.";
        if (r.IncludeIdenticalDeployEval is null)
            return "'includeIdenticalDeployEval' is required.";
        if (r.OnePercentRule is null)
            return "'onePercentRule' is required.";

        if (r.SizeDecimals is not { } decimals || r.Step is not { } step || r.MinLot is not { } minLot || r.MaxLots is not { } maxLots)
            return "'sizeDecimals', 'step', 'minLot' and 'maxLots' are required (sizeDecimals = 0 is a legal whole-lot grid).";
        if (decimals < 0 || step <= 0m || minLot <= 0m || maxLots < minLot)
            return "The lot grid is invalid: 'sizeDecimals' >= 0, 'step' > 0, 'minLot' > 0 and 'maxLots' >= 'minLot'.";

        if ((r.FxLow is null) != (r.FxHigh is null))
            return "'fxLow' and 'fxHigh' must be sent together.";
        if (r.FxLow is { } low && r.FxHigh is { } high && (low <= 0m || high < low))
            return "The FX band is invalid: 0 < 'fxLow' <= 'fxHigh'.";

        if (r.EliminationCeiling is < 0m or > 1m)
            return "'eliminationCeiling' is a fraction between 0 and 1.";
        if (r.MaxFullSimulations is { } sims && (sims < 1 || sims > FtmoGroupSearchLimits.MaxFullSimulationsCeiling))
            return $"'maxFullSimulations' must be between 1 and {FtmoGroupSearchLimits.MaxFullSimulationsCeiling}.";
        if (r.MaxWallClockSeconds is { } seconds && (seconds < 1 || seconds > FtmoGroupSearchLimits.MaxWallClockSecondsCeiling))
            return $"'maxWallClockSeconds' must be between 1 and {FtmoGroupSearchLimits.MaxWallClockSecondsCeiling}.";

        return null;
    }
}
