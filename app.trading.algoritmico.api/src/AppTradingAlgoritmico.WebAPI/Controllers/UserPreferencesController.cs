using AppTradingAlgoritmico.Application.DTOs.UserPreferences;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/user/preferences")]
[Authorize]
[Produces("application/json")]
public class UserPreferencesController(
    IUserPreferencesService preferencesService,
    ILogger<UserPreferencesController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the current user's language and theme preferences.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserPreferencesDto>> Get(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await preferencesService.GetAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the current user's language and/or theme preferences.
    /// Only provided fields are changed.
    /// </summary>
    [HttpPatch]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserPreferencesDto>> Update(
        [FromBody] UpdateUserPreferencesDto dto,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await preferencesService.UpdateAsync(userId.Value, dto, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid preference update for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
