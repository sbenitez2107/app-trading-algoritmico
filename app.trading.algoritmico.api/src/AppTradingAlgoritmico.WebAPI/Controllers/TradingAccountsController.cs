using AppTradingAlgoritmico.Application.DTOs.TradingAccounts;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/trading-accounts")]
[Authorize]
[Produces("application/json")]
public class TradingAccountsController : ControllerBase
{
    private readonly ITradingAccountService _service;
    private readonly ILogger<TradingAccountsController> _logger;

    public TradingAccountsController(ITradingAccountService service, ILogger<TradingAccountsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Returns all trading accounts, optionally filtered by broker and/or account type.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TradingAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TradingAccountDto>>> GetAll(
        [FromQuery] string? broker,
        [FromQuery] AccountType? accountType,
        CancellationToken ct)
    {
        var accounts = await _service.GetAllAsync(broker, accountType, ct);
        return Ok(accounts);
    }

    /// <summary>Returns a single trading account by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TradingAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TradingAccountDto>> GetById(Guid id, CancellationToken ct)
    {
        var account = await _service.GetByIdAsync(id, ct);
        return account is null ? NotFound() : Ok(account);
    }

    /// <summary>Creates a new trading account with encrypted password.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TradingAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TradingAccountDto>> Create(
        [FromBody] CreateTradingAccountDto dto,
        CancellationToken ct)
    {
        var account = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    /// <summary>Updates an existing trading account. Password is updated only if provided.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TradingAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TradingAccountDto>> Update(
        Guid id,
        [FromBody] UpdateTradingAccountDto dto,
        CancellationToken ct)
    {
        try
        {
            var account = await _service.UpdateAsync(id, dto, ct);
            return Ok(account);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Toggles the enabled/disabled state of a trading account.</summary>
    [HttpPatch("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.ToggleEnabledAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Permanently deletes a trading account.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
