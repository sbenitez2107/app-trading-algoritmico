using AppTradingAlgoritmico.Application.DTOs.BatchStages;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/batches/{batchId:guid}/stages")]
[Authorize]
[Produces("application/json")]
public class BatchStagesController(
    IBatchStageService service,
    ILogger<BatchStagesController> logger) : ControllerBase
{
    /// <summary>Returns details of a specific pipeline stage.</summary>
    [HttpGet("{stageId:guid}")]
    [ProducesResponseType(typeof(BatchStageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchStageDetailDto>> GetDetail(Guid batchId, Guid stageId, CancellationToken ct)
    {
        try
        {
            var result = await service.GetDetailAsync(batchId, stageId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Updates a pipeline stage's status, notes, or output count.</summary>
    [HttpPatch("{stageId:guid}")]
    [ProducesResponseType(typeof(BatchStageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchStageDetailDto>> Update(
        Guid batchId, Guid stageId,
        [FromBody] UpdateBatchStageDto dto,
        CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateAsync(batchId, stageId, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
