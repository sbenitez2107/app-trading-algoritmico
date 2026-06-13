using AppTradingAlgoritmico.Application.DTOs.Portfolios;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/risk-limits")]
[Authorize]
public class RiskLimitsController(IRiskLimitsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BrokerRiskLimitsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BrokerRiskLimitsDto>>> GetAll(CancellationToken ct = default)
        => Ok(await service.GetAllAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(BrokerRiskLimitsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BrokerRiskLimitsDto>> Upsert(
        [FromBody] UpsertBrokerRiskLimitsDto dto, CancellationToken ct = default)
    {
        try { return Ok(await service.UpsertAsync(dto, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
