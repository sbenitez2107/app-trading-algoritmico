using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.TradingAccounts;

public record TradingAccountDto(
    Guid Id,
    string Name,
    string Broker,
    AccountType AccountType,
    PlatformType Platform,
    long AccountNumber,
    long Login,
    string Server,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
