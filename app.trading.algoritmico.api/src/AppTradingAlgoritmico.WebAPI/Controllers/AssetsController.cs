using AppTradingAlgoritmico.Application.DTOs.Assets;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
[Produces("application/json")]
public class AssetsController(IAssetService service, ILogger<AssetsController> logger) : ControllerBase
{
    /// <summary>Returns all trading assets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssetDto>>> GetAll(CancellationToken ct)
    {
        var result = await service.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Creates a new trading asset.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssetDto>> Create([FromBody] CreateAssetDto dto, CancellationToken ct)
    {
        try
        {
            var result = await service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetAll), result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Asset creation failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
