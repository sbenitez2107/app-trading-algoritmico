using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

public class AccountEquitySnapshot : BaseEntity
{
    public Guid TradingAccountId { get; set; }
    public TradingAccount TradingAccount { get; set; } = null!;

    /// <summary>Timestamp parsed from the MT4 report header (e.g. "2026 April 21, 07:06").</summary>
    public DateTime ReportTime { get; set; }

    public decimal Balance { get; set; }
    public decimal Equity { get; set; }
    public decimal FloatingPnL { get; set; }
    public decimal Margin { get; set; }
    public decimal FreeMargin { get; set; }
    public decimal ClosedTradePnL { get; set; }

    /// <summary>Account currency as reported in the Summary section (e.g. "USD"). Required, max length 10.</summary>
    public string Currency { get; set; } = string.Empty;
}
