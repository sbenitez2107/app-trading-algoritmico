using AppTradingAlgoritmico.Application.DTOs.AnalyzerRules;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/analyzer-rules")]
[Authorize]
[Produces("application/json")]
public class AnalyzerRulesController(
    IAnalyzerRuleService service,
    ILogger<AnalyzerRulesController> logger) : ControllerBase
{
    /// <summary>Returns all analyzer rules.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AnalyzerRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AnalyzerRuleDto>>> GetAll(CancellationToken ct)
    {
        var result = await service.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Returns an analyzer rule by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnalyzerRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalyzerRuleDto>> GetById(Guid id, CancellationToken ct)
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

    /// <summary>Creates an analyzer rule.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AnalyzerRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalyzerRuleDto>> Create(
        [FromBody] CreateAnalyzerRuleDto dto,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Updates an analyzer rule (partial update).</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AnalyzerRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalyzerRuleDto>> Update(
        Guid id,
        [FromBody] UpdateAnalyzerRuleDto dto,
        CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateAsync(id, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes an analyzer rule.</summary>
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
