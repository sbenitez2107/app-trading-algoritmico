using AppTradingAlgoritmico.Application.DTOs.BuildingBlocks;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/building-blocks")]
[Authorize]
[Produces("application/json")]
public class BuildingBlocksController(
    IBuildingBlockService service,
    ILogger<BuildingBlocksController> logger) : ControllerBase
{
    /// <summary>Returns all building blocks.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BuildingBlockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BuildingBlockDto>>> GetAll(CancellationToken ct)
    {
        var result = await service.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Returns a building block with its XML config.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BuildingBlockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BuildingBlockDetailDto>> GetById(Guid id, CancellationToken ct)
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

    /// <summary>Creates a building block, optionally with a .sqb config file.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BuildingBlockDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BuildingBlockDto>> Create(
        [FromForm] CreateBuildingBlockDto dto,
        IFormFile? file,
        CancellationToken ct)
    {
        Stream? sqbStream = file?.OpenReadStream();
        try
        {
            var result = await service.CreateAsync(dto, sqbStream, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        finally
        {
            sqbStream?.Dispose();
        }
    }

    /// <summary>Updates a building block, optionally replacing the .sqb config file.</summary>
    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BuildingBlockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BuildingBlockDto>> Update(
        Guid id,
        [FromForm] CreateBuildingBlockDto dto,
        IFormFile? file,
        CancellationToken ct)
    {
        Stream? sqbStream = file?.OpenReadStream();
        try
        {
            var result = await service.UpdateAsync(id, dto, sqbStream, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        finally
        {
            sqbStream?.Dispose();
        }
    }

    /// <summary>Deletes a building block.</summary>
    [HttpDelete("{id:guid}")]
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
}
