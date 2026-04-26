using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

/// <summary>
/// Nested-resource controller for strategies attached directly to a trading account.
/// Route: api/trading-accounts/{accountId}/strategies
/// </summary>
[ApiController]
[Route("api/trading-accounts/{accountId:guid}/strategies")]
[Authorize]
[Produces("application/json")]
public class TradingAccountStrategiesController(IStrategyService service) : ControllerBase
{
    /// <summary>Returns all strategies attached to the given trading account (paginated).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StrategyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StrategyDto>>> GetStrategies(
        [FromRoute] Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await service.GetByAccountAsync(accountId, page, pageSize, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Uploads a new strategy to the given trading account.
    /// Requires: name (string), sqxFile (.sqx), htmlFile (.html). Optional: magicNumber (int).
    /// Returns the created StrategyDto with 201 Created.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyDto>> CreateStrategy(
        [FromRoute] Guid accountId,
        [FromForm] string name,
        [FromForm] IFormFile? sqxFile,
        [FromForm] IFormFile? htmlFile,
        [FromForm] int? magicNumber = null,
        CancellationToken ct = default)
    {
        if (sqxFile is null)
            return BadRequest("sqxFile is required.");

        if (htmlFile is null)
            return BadRequest("htmlFile is required.");

        Stream? sqxStream = sqxFile.OpenReadStream();
        Stream? htmlStream = htmlFile.OpenReadStream();

        try
        {
            var dto = await service.AddToAccountAsync(accountId, name, sqxStream, htmlStream, magicNumber, ct);
            return CreatedAtAction(nameof(GetStrategies), new { accountId }, dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            await sqxStream.DisposeAsync();
            await htmlStream.DisposeAsync();
        }
    }

    /// <summary>
    /// Assigns a magic number to an existing strategy attached to the given trading account.
    /// </summary>
    [HttpPost("{strategyId:guid}/magic-number")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StrategyDto>> AssignMagicNumber(
        [FromRoute] Guid accountId,
        [FromRoute] Guid strategyId,
        [FromBody] AssignMagicNumberDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var result = await service.AssignMagicNumberAsync(accountId, strategyId, dto.MagicNumber, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
