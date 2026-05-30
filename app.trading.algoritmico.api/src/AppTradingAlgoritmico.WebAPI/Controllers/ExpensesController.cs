using AppTradingAlgoritmico.Application.DTOs.Expenses;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _expenseService.GetAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await _expenseService.GetByIdAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseDto dto, CancellationToken ct = default)
    {
        var result = await _expenseService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> Update(Guid id, [FromBody] UpdateExpenseDto dto, CancellationToken ct = default)
    {
        try
        {
            var result = await _expenseService.UpdateAsync(id, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _expenseService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("summaries/month")]
    [ProducesResponseType(typeof(ExpenseMonthSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpenseMonthSummaryDto>> GetMonthSummary(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var result = await _expenseService.GetMonthSummaryAsync(year, month, ct);
        return Ok(result);
    }

    [HttpGet("summaries/year")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseMonthSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseMonthSummaryDto>>> GetYearSummary(
        [FromQuery] int year,
        CancellationToken ct = default)
    {
        var result = await _expenseService.GetYearSummaryAsync(year, ct);
        return Ok(result);
    }

    [HttpGet("projections")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseProjectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseProjectionDto>>> GetProjections(
        [FromQuery] int forecastMonths = 12,
        CancellationToken ct = default)
    {
        var result = await _expenseService.GetProjectionsAsync(forecastMonths, ct);
        return Ok(result);
    }

    [HttpGet("categories/totals")]
    [ProducesResponseType(typeof(Dictionary<ExpenseCategory, decimal>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<ExpenseCategory, decimal>>> GetCategoryTotals(CancellationToken ct = default)
    {
        var result = await _expenseService.GetCategoryTotalsAsync(ct);
        return Ok(result);
    }
}
