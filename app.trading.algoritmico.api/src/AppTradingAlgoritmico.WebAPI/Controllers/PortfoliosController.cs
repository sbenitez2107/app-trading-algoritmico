using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfoliosController(IPortfolioService portfolioService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PortfolioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PortfolioDto>>> Get(
        [FromQuery] string? broker = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await portfolioService.GetAllAsync(broker, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> GetById(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.GetByIdAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortfolioDto>> Create([FromBody] CreatePortfolioDto dto, CancellationToken ct = default)
    {
        var result = await portfolioService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> Update(Guid id, [FromBody] UpdatePortfolioDto dto, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try { await portfolioService.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ---- Membership ----

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> AddMember(Guid id, [FromBody] AddPortfolioMemberDto dto, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.AddMemberAsync(id, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/members/{strategyId:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> UpdateMemberWeight(
        Guid id, Guid strategyId, [FromBody] UpdateMemberWeightDto dto, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.UpdateMemberWeightAsync(id, strategyId, dto.Weight, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}/members/{strategyId:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> RemoveMember(Guid id, Guid strategyId, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.RemoveMemberAsync(id, strategyId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ---- Analytics (computed on demand) ----

    [HttpGet("{id:guid}/analytics")]
    [ProducesResponseType(typeof(PortfolioAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioAnalyticsDto>> GetAnalytics(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.GetAnalyticsAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/monthly-returns")]
    [ProducesResponseType(typeof(IReadOnlyList<MonthlyReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MonthlyReturnDto>>> GetMonthlyReturns(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.GetMonthlyReturnsAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/equity-curve")]
    [ProducesResponseType(typeof(IReadOnlyList<PortfolioEquityPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PortfolioEquityPointDto>>> GetEquityCurve(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.GetEquityCurveAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/risk")]
    [ProducesResponseType(typeof(PortfolioRiskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioRiskDto>> GetRisk(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await portfolioService.GetRiskAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
