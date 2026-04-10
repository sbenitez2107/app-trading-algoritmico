using AppTradingAlgoritmico.Application.DTOs.TradingAccounts;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.Interfaces;

public interface ITradingAccountService
{
    Task<IReadOnlyList<TradingAccountDto>> GetAllAsync(
        string? broker = null,
        AccountType? accountType = null,
        CancellationToken ct = default);

    Task<TradingAccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<TradingAccountDto> CreateAsync(CreateTradingAccountDto dto, CancellationToken ct = default);

    Task<TradingAccountDto> UpdateAsync(Guid id, UpdateTradingAccountDto dto, CancellationToken ct = default);

    Task ToggleEnabledAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
