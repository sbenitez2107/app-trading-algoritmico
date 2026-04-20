using AppTradingAlgoritmico.Application.DTOs.GridPresets;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/users/me/grid-presets")]
[Authorize]
[Produces("application/json")]
public class GridPresetsController(IGridPresetService presetService) : ControllerBase
{
    /// <summary>Returns all grid presets for the current user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GridPresetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GridPresetDto>>> GetPresets(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await presetService.GetByUserAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>Creates a new grid preset for the current user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GridPresetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GridPresetDto>> CreatePreset(
        [FromBody] CreateGridPresetDto dto,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await presetService.CreateAsync(userId.Value, dto, ct);
            return CreatedAtAction(nameof(GetPresets), result);
        }
        catch (ArgumentException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Overwrites an existing grid preset's columns. Name is preserved.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GridPresetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GridPresetDto>> UpdatePreset(
        Guid id,
        [FromBody] UpdateGridPresetDto dto,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await presetService.UpdateAsync(userId.Value, id, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes a grid preset for the current user.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePreset(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await presetService.DeleteAsync(userId.Value, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
