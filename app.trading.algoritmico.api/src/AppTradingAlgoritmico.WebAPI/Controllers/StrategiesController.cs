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
}
