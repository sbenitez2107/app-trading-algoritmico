using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

/// <summary>
/// FTMO simulations that are not scoped to one strategy (ftmo-group-simulation, design.md D5). Route
/// <c>api/ftmo-simulations</c>. A strategy-scoped simulation lives on <see cref="StrategyBacktestsController"/>;
/// this one takes a caller-chosen GROUP of strategies in the body.
/// </summary>
[ApiController]
[Route("api/ftmo-simulations")]
[Authorize]
[Produces("application/json")]
public class FtmoSimulationsController(IFtmoGroupSimulationReadService groupSimulationReadService) : ControllerBase
{
    /// <summary>
    /// Replays one manually chosen group of strategies, sharing ONE FTMO account, as a single merged series per
    /// run kind (Deploy and Evaluation, never merged with each other).
    /// <para>
    /// Validation mirrors the single-strategy endpoints' split (<c>StrategyBacktestsController</c>, which is not
    /// edited): a MISSING required field, an empty list, an empty GUID or more distinct members than
    /// <see cref="FtmoGroupSimulationLimits.MaxMembers"/> is a 400. A value that is present but unusable
    /// (non-positive capital or risk, an invalid source grid) is NOT a 400: the service refuses the whole
    /// request as <c>InvalidRequest</c>. An unknown strategy id is not a 400 either (<c>MemberNotFound</c>).
    /// <c>sizeDecimals = 0</c> is a legal whole-lot grid, which is why every scalar is nullable on the wire.
    /// </para>
    /// </summary>
    [HttpPost("group")]
    [ProducesResponseType(typeof(FtmoGroupSimulationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FtmoGroupSimulationDto>> SimulateGroup(
        [FromBody] FtmoGroupSimulationRequest? request, CancellationToken ct)
    {
        if (!TryValidateGroupRequest(request, out var parameters, out var error))
            return BadRequest(new { message = error });

        return Ok(await groupSimulationReadService.SimulateAsync(parameters!, ct));
    }

    /// <summary>
    /// The group picker's facts for ONE account: its strategies with, per kind (Deploy and Evaluation), the held
    /// run's presence, trade count, range, spec/calibration flags, currency and zone, plus whether the name also
    /// exists on another account, and the member cap. A missing or empty <c>tradingAccountId</c> is a 400.
    /// The service is bound with <c>[FromServices]</c> so the controller constructor, and the tests that build
    /// it, stay as they were.
    /// </summary>
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(FtmoGroupCandidatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FtmoGroupCandidatesDto>> GetCandidates(
        [FromQuery] Guid? tradingAccountId,
        [FromServices] IFtmoGroupCandidatesReadService candidatesReadService,
        CancellationToken ct)
    {
        if (tradingAccountId is null || tradingAccountId == Guid.Empty)
            return BadRequest(new { message = "The 'tradingAccountId' query parameter is required." });

        return Ok(await candidatesReadService.GetCandidatesAsync(tradingAccountId.Value, ct));
    }

    /// <summary>
    /// Mirrors <c>StrategyBacktestsController.TryValidateFtmoBreachQuery</c> (private there, and that file is
    /// out of scope). The ids are forwarded AS SENT: the service deduplicates them and echoes the removed ones.
    /// </summary>
    private static bool TryValidateGroupRequest(
        FtmoGroupSimulationRequest? request, out FtmoGroupSimulationParameters? parameters, out string? error)
    {
        parameters = null;
        error = null;

        if (request is null
            || request.MemberStrategyIds is not { Count: > 0 }
            || string.IsNullOrWhiteSpace(request.Broker)
            || request.InitialCapital is null || request.TargetRiskPerTrade is null
            || request.SizeDecimals is null || request.Step is null || request.MinLot is null || request.MaxLots is null)
        {
            error = "The 'memberStrategyIds' (at least one), 'broker', 'initialCapital', 'targetRiskPerTrade', "
                + "'sizeDecimals', 'step', 'minLot' and 'maxLots' fields are required. There is no default.";
            return false;
        }

        if (request.MemberStrategyIds.Any(id => id == Guid.Empty))
        {
            error = "'memberStrategyIds' must not contain the empty GUID.";
            return false;
        }

        var distinct = request.MemberStrategyIds.Distinct().Count();
        if (distinct > FtmoGroupSimulationLimits.MaxMembers)
        {
            error = $"A group may have at most {FtmoGroupSimulationLimits.MaxMembers} distinct members; {distinct} were sent.";
            return false;
        }

        parameters = new FtmoGroupSimulationParameters(
            request.MemberStrategyIds,
            request.Broker,
            request.InitialCapital.Value,
            request.TargetRiskPerTrade.Value,
            request.FxLow,
            request.FxHigh,
            request.SizeDecimals.Value,
            request.Step.Value,
            request.MinLot.Value,
            request.MaxLots.Value);
        return true;
    }
}
