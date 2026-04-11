using AppTradingAlgoritmico.Application.DTOs.Batches;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/batches")]
[Authorize]
[Produces("application/json")]
public class BatchesController(IBatchService service, ILogger<BatchesController> logger) : ControllerBase
{
    /// <summary>Returns all batches, optionally filtered by asset and timeframe.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BatchDto>>> GetAll(
        [FromQuery] Guid? assetId,
        [FromQuery] Timeframe? timeframe,
        CancellationToken ct)
    {
        var result = await service.GetAllAsync(assetId, timeframe, ct);
        return Ok(result);
    }

    /// <summary>Returns a batch with all its pipeline stages.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await service.GetByIdAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Creates a new batch in the Builder stage with strategies from a ZIP of .sqx files.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchDto>> Create(
        [FromForm] CreateBatchDto dto,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        try
        {
            var result = await service.CreateAsync(dto, stream, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Batch creation failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Advances a batch to the next pipeline stage with strategies from a ZIP of .sqx files.</summary>
    [HttpPost("{id:guid}/advance")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchDto>> Advance(
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        try
        {
            var result = await service.AdvanceAsync(id, stream, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Batch advance failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
