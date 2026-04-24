using AppTradingAlgoritmico.Application.DTOs.Trades;
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
    private readonly ITradeImportService _tradeImportService;
    private readonly ILogger<TradingAccountsController> _logger;

    public TradingAccountsController(
        ITradingAccountService service,
        ITradeImportService tradeImportService,
        ILogger<TradingAccountsController> logger)
    {
        _service = service;
        _tradeImportService = tradeImportService;
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

    /// <summary>
    /// Imports MT4 trades from an HTML statement file into the specified trading account.
    /// Parses the statement, upserts matched trades, and records an equity snapshot.
    /// </summary>
    /// <param name="id">The trading account ID.</param>
    /// <param name="file">The MT4 HTML statement file (.htm / .html).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TradeImportResultDto"/> with counts and snapshot details.</returns>
    /// <response code="200">Import completed. Returns counts and snapshot.</response>
    /// <response code="400">HTML could not be parsed (unrecognised or empty file).</response>
    /// <response code="404">Trading account not found.</response>
    [HttpPost("{id:guid}/trades/import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(TradeImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TradeImportResultDto>> ImportTrades(
        [FromRoute] Guid id,
        [FromForm] IFormFile file,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _tradeImportService.ImportAsync(id, stream, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
