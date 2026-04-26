using AppTradingAlgoritmico.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppTradingAlgoritmico.Application.DTOs.TradingAccounts;

public record UpdateTradingAccountDto(
    [Required, MaxLength(200)] string Name,
    PlatformType Platform,
    [Range(1, long.MaxValue)] long AccountNumber,
    [Range(1, long.MaxValue)] long Login,
    /// <summary>If null or empty, the existing password is preserved.</summary>
    string? Password,
    [Required, MaxLength(300)] string Server,
    [Required, Range(typeof(decimal), "0.01", "9999999999.99")] decimal InitialBalance,
    [MaxLength(10)] string? Currency,
    bool IsEnabled
);
