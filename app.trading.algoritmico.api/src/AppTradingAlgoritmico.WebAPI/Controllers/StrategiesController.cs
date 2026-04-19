using System.Security.Claims;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public class StrategiesController(IStrategyService service) : ControllerBase
{
    /// <summary>Returns paginated strategies for a specific batch stage.</summary>
    [HttpGet("api/batches/{batchId:guid}/stages/{stageId:guid}/strategies")]
    [ProducesResponseType(typeof(PagedResult<StrategyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StrategyDto>>> GetByStage(
        Guid batchId, Guid stageId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await service.GetByStageAsync(batchId, stageId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Updates KPI values for a specific strategy.</summary>
    [HttpPatch("api/strategies/{id:guid}")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyDto>> UpdateKpis(
        Guid id,
        [FromBody] UpdateStrategyKpisDto dto,
        CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateKpisAsync(id, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Permanently deletes a strategy by ID.</summary>
    [HttpDelete("api/strategies/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Returns all comments for a strategy, ordered by date descending.</summary>
    [HttpGet("api/strategies/{id:guid}/comments")]
    [ProducesResponseType(typeof(IEnumerable<StrategyCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StrategyCommentDto>>> GetComments(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await service.GetCommentsAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Adds a comment to a strategy (append-only).</summary>
    [HttpPost("api/strategies/{id:guid}/comments")]
    [ProducesResponseType(typeof(StrategyCommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyCommentDto>> AddComment(
        Guid id, [FromBody] CreateStrategyCommentDto dto, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await service.AddCommentAsync(id, dto.Content, userId, ct);
            return Created($"api/strategies/{id}/comments/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
