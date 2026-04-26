using System.Security.Claims;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public class StrategiesController(IStrategyService service, ITradeImportService tradeImportService) : ControllerBase
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

    /// <summary>
    /// Returns a paginated list of trades for a strategy, filtered by open/closed status.
    /// </summary>
    /// <param name="id">The strategy ID.</param>
    /// <param name="status">Trade status filter: <c>all</c>, <c>open</c>, or <c>closed</c>. Defaults to <c>all</c>.</param>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 50.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated <see cref="StrategyTradeDto"/> list ordered by close time descending.</returns>
    /// <response code="200">Returns paged trades for the strategy.</response>
    /// <response code="400">Invalid status filter value.</response>
    [HttpGet("api/strategies/{id:guid}/trades")]
    [ProducesResponseType(typeof(PagedResult<StrategyTradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StrategyTradeDto>>> GetTrades(
        [FromRoute] Guid id,
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<TradeStatusFilter>(status, ignoreCase: true, out var filter))
            return BadRequest($"Invalid status '{status}'. Valid values: all, open, closed.");

        var result = await tradeImportService.GetByStrategyAsync(id, filter, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns aggregated KPIs across every imported trade of the given strategy.
    /// Independent of the trades pagination window — values are computed in SQL.
    /// </summary>
    [HttpGet("api/strategies/{id:guid}/trades/summary")]
    [ProducesResponseType(typeof(StrategyTradeSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategyTradeSummaryDto>> GetTradesSummary(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var summary = await tradeImportService.GetSummaryByStrategyAsync(id, ct);
        return Ok(summary);
    }

    /// <summary>Returns the full performance analytics block for the given strategy.</summary>
    [HttpGet("api/strategies/{id:guid}/analytics")]
    [ProducesResponseType(typeof(StrategyAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategyAnalyticsDto>> GetAnalytics(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var analytics = await tradeImportService.GetAnalyticsByStrategyAsync(id, ct);
            return Ok(analytics);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Returns the month-by-month compounding return series for a strategy.</summary>
    [HttpGet("api/strategies/{id:guid}/monthly-returns")]
    [ProducesResponseType(typeof(IReadOnlyList<MonthlyReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MonthlyReturnDto>>> GetMonthlyReturns(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var months = await tradeImportService.GetMonthlyReturnsByStrategyAsync(id, ct);
            return Ok(months);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
