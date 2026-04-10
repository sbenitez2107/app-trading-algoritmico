using AppTradingAlgoritmico.Application.DTOs.TradingAccounts;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class TradingAccountService : ITradingAccountService
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;

    public TradingAccountService(AppDbContext db, IEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<IReadOnlyList<TradingAccountDto>> GetAllAsync(
        string? broker = null,
        AccountType? accountType = null,
        CancellationToken ct = default)
    {
        var query = _db.TradingAccounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(broker))
            query = query.Where(x => x.Broker == broker);

        if (accountType.HasValue)
            query = query.Where(x => x.AccountType == accountType.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToDto(x))
            .ToListAsync(ct);
    }

    public async Task<TradingAccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.TradingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<TradingAccountDto> CreateAsync(CreateTradingAccountDto dto, CancellationToken ct = default)
    {
        var entity = new TradingAccount
        {
            Name = dto.Name,
            Broker = dto.Broker,
            AccountType = dto.AccountType,
            Platform = dto.Platform,
            AccountNumber = dto.AccountNumber,
            Login = dto.Login,
            PasswordEncrypted = _encryption.Encrypt(dto.Password),
            Server = dto.Server,
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _db.TradingAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<TradingAccountDto> UpdateAsync(Guid id, UpdateTradingAccountDto dto, CancellationToken ct = default)
    {
        var entity = await _db.TradingAccounts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TradingAccount {id} not found.");

        entity.Name = dto.Name;
        entity.Platform = dto.Platform;
        entity.AccountNumber = dto.AccountNumber;
        entity.Login = dto.Login;
        entity.Server = dto.Server;
        entity.IsEnabled = dto.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        // Only re-encrypt if a new password was provided
        if (!string.IsNullOrWhiteSpace(dto.Password))
            entity.PasswordEncrypted = _encryption.Encrypt(dto.Password);

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task ToggleEnabledAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.TradingAccounts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TradingAccount {id} not found.");

        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.TradingAccounts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TradingAccount {id} not found.");

        _db.TradingAccounts.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static TradingAccountDto ToDto(TradingAccount x) => new(
        x.Id,
        x.Name,
        x.Broker,
        x.AccountType,
        x.Platform,
        x.AccountNumber,
        x.Login,
        x.Server,
        x.IsEnabled,
        x.CreatedAt,
        x.UpdatedAt
    );
}
