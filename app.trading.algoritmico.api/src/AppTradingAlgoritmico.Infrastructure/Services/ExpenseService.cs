using AppTradingAlgoritmico.Application.DTOs.Expenses;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public ExpenseService(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PagedResult<ExpenseDto>> GetAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var totalCount = await _dbContext.Expenses.CountAsync(ct);
        var expenses = await _dbContext.Expenses
            .OrderByDescending(x => x.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ExpenseDto>(
            _mapper.Map<IReadOnlyList<ExpenseDto>>(expenses),
            totalCount,
            page,
            pageSize
        );
    }

    public async Task<ExpenseDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Expense with id {id} not found");
        return _mapper.Map<ExpenseDto>(expense);
    }

    public async Task<ExpenseDto> CreateAsync(CreateExpenseDto dto, CancellationToken ct = default)
    {
        var expense = _mapper.Map<Expense>(dto);
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync(ct);
        return _mapper.Map<ExpenseDto>(expense);
    }

    public async Task<ExpenseDto> UpdateAsync(Guid id, UpdateExpenseDto dto, CancellationToken ct = default)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Expense with id {id} not found");

        _mapper.Map(dto, expense);
        await _dbContext.SaveChangesAsync(ct);
        return _mapper.Map<ExpenseDto>(expense);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _dbContext.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Expense with id {id} not found");

        _dbContext.Expenses.Remove(expense);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<ExpenseMonthSummaryDto> GetMonthSummaryAsync(int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var expenses = await _dbContext.Expenses
            .Where(x => x.Date >= startDate && x.Date <= endDate)
            .ToListAsync(ct);

        var byCategory = new Dictionary<ExpenseCategory, decimal>();
        foreach (var category in Enum.GetValues(typeof(ExpenseCategory)).Cast<ExpenseCategory>())
        {
            var total = expenses.Where(x => x.Category == category).Sum(x => x.Amount);
            if (total > 0)
                byCategory[category] = total;
        }

        return new ExpenseMonthSummaryDto
        {
            Year = year,
            Month = month,
            TotalAmount = expenses.Sum(x => x.Amount),
            ByCategory = byCategory
        };
    }

    public async Task<IEnumerable<ExpenseMonthSummaryDto>> GetYearSummaryAsync(int year, CancellationToken ct = default)
    {
        var expenses = await _dbContext.Expenses
            .Where(x => x.Date.Year == year)
            .ToListAsync(ct);

        var summaries = new List<ExpenseMonthSummaryDto>();
        for (int month = 1; month <= 12; month++)
        {
            var monthExpenses = expenses.Where(x => x.Date.Month == month).ToList();

            var byCategory = new Dictionary<ExpenseCategory, decimal>();
            foreach (var category in Enum.GetValues(typeof(ExpenseCategory)).Cast<ExpenseCategory>())
            {
                var total = monthExpenses.Where(x => x.Category == category).Sum(x => x.Amount);
                if (total > 0)
                    byCategory[category] = total;
            }

            summaries.Add(new ExpenseMonthSummaryDto
            {
                Year = year,
                Month = month,
                TotalAmount = monthExpenses.Sum(x => x.Amount),
                ByCategory = byCategory
            });
        }

        return summaries.Where(x => x.TotalAmount > 0);
    }

    public async Task<IEnumerable<ExpenseProjectionDto>> GetProjectionsAsync(int forecastMonths = 12, CancellationToken ct = default)
    {
        var expenses = await _dbContext.Expenses.ToListAsync(ct);

        if (expenses.Count == 0)
            return Enumerable.Empty<ExpenseProjectionDto>();

        var lastDate = expenses.Max(x => x.Date);
        var last3Months = expenses
            .Where(x => x.Date >= lastDate.AddMonths(-3) && x.Date <= lastDate)
            .ToList();

        var projections = new List<ExpenseProjectionDto>();

        for (int i = 1; i <= forecastMonths; i++)
        {
            var projectionDate = lastDate.AddMonths(i);
            var projection = new ExpenseProjectionDto
            {
                Year = projectionDate.Year,
                Month = projectionDate.Month,
                ByCategory = new Dictionary<ExpenseCategory, decimal>(),
                ByPropFirm = new Dictionary<string, decimal>()
            };

            foreach (var category in Enum.GetValues(typeof(ExpenseCategory)).Cast<ExpenseCategory>())
            {
                var categoryExpenses = last3Months.Where(x => x.Category == category).ToList();
                if (categoryExpenses.Count > 0)
                {
                    var average = categoryExpenses.Sum(x => x.Amount) / 3;
                    projection.ByCategory[category] = average;
                    projection.ProjectedTotal += average;
                }
            }

            // Map categories to prop firms
            MapCategoriesToPropFirms(projection);

            projections.Add(projection);
        }

        return projections;
    }

    public async Task<Dictionary<ExpenseCategory, decimal>> GetCategoryTotalsAsync(CancellationToken ct = default)
    {
        var expenses = await _dbContext.Expenses.ToListAsync(ct);
        var totals = new Dictionary<ExpenseCategory, decimal>();

        foreach (var category in Enum.GetValues(typeof(ExpenseCategory)).Cast<ExpenseCategory>())
        {
            var total = expenses.Where(x => x.Category == category).Sum(x => x.Amount);
            if (total > 0)
                totals[category] = total;
        }

        return totals;
    }

    private static void MapCategoriesToPropFirms(ExpenseProjectionDto projection)
    {
        var propFirmMapping = new Dictionary<ExpenseCategory, string>
        {
            { ExpenseCategory.FTMO, "FTMO" },
            { ExpenseCategory.WSF, "WSF" },
            { ExpenseCategory.DarwinexZero, "Darwinex" },
            { ExpenseCategory.ServidorFxvsPro, "Servidores" },
            { ExpenseCategory.ServidorHetzner, "Infraestructura" },
            { ExpenseCategory.MentoriaImox, "Educación" }
        };

        foreach (var (category, propFirm) in propFirmMapping)
        {
            if (projection.ByCategory.TryGetValue(category, out var amount))
            {
                if (!projection.ByPropFirm.ContainsKey(propFirm))
                    projection.ByPropFirm[propFirm] = 0;
                projection.ByPropFirm[propFirm] += amount;
            }
        }
    }
}
