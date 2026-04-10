using AppTradingAlgoritmico.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppTradingAlgoritmico.Application.DTOs.TradingAccounts;

public record CreateTradingAccountDto(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(100)] string Broker,
    AccountType AccountType,
    PlatformType Platform,
    [Range(1, long.MaxValue)] long AccountNumber,
    [Range(1, long.MaxValue)] long Login,
    [Required, MaxLength(200)] string Password,
    [Required, MaxLength(300)] string Server,
    bool IsEnabled = true
);
