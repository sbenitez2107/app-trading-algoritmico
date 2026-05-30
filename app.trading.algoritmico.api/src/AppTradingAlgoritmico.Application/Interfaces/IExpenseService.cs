using AppTradingAlgoritmico.Application.DTOs.Expenses;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> GetAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<ExpenseDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExpenseDto> CreateAsync(CreateExpenseDto dto, CancellationToken ct = default);
    Task<ExpenseDto> UpdateAsync(Guid id, UpdateExpenseDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ExpenseMonthSummaryDto> GetMonthSummaryAsync(int year, int month, CancellationToken ct = default);
    Task<IEnumerable<ExpenseMonthSummaryDto>> GetYearSummaryAsync(int year, CancellationToken ct = default);
    Task<IEnumerable<ExpenseProjectionDto>> GetProjectionsAsync(int forecastMonths = 12, CancellationToken ct = default);
    Task<Dictionary<ExpenseCategory, decimal>> GetCategoryTotalsAsync(CancellationToken ct = default);
}
