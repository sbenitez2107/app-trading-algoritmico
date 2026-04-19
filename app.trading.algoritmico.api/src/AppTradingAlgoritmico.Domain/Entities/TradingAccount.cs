using AppTradingAlgoritmico.Domain.Common;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Entities;

public class TradingAccount : BaseEntity
{
    /// <summary>Display name for the account (e.g. "SBDEMO2|sbenitez2107")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Broker/PropFirm name (e.g. "Darwinex", "Axi")</summary>
    public string Broker { get; set; } = string.Empty;

    /// <summary>Demo or Live account</summary>
    public AccountType AccountType { get; set; }

    /// <summary>MT4 or MT5</summary>
    public PlatformType Platform { get; set; }

    /// <summary>Numeric account identifier</summary>
    public long AccountNumber { get; set; }

    /// <summary>Login credential (often same as AccountNumber)</summary>
    public long Login { get; set; }

    /// <summary>AES-256 encrypted password — never returned in plain text via API</summary>
    public string PasswordEncrypted { get; set; } = string.Empty;

    /// <summary>MT4/MT5 broker server address</summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>Whether this account is active for use</summary>
    public bool IsEnabled { get; set; } = true;

    public ICollection<Strategy> Strategies { get; set; } = [];
}
