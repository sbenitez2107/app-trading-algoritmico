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
    /// <summary>Starting equity used for return / drawdown / CAGR. Required for new accounts.</summary>
    [Required, Range(typeof(decimal), "0.01", "9999999999.99")] decimal InitialBalance,
    [MaxLength(10)] string? Currency = null,
    bool IsEnabled = true
);
