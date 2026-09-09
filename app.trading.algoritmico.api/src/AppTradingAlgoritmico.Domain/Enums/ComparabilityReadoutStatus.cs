namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why a demo-vs-backtest comparability readout holds the figures it holds — or holds none. The
/// default (<c>0</c>) is <see cref="NoPairedOpens"/>, deliberately: an omitted or forgotten status
/// must assert that NOTHING was observed, never <see cref="Measured"/> and never a run kind that
/// was never checked (design.md D3, D5).
/// </summary>
public enum ComparabilityReadoutStatus
{
    /// <summary>A run existed and demo trades existed, but zero exact-minute pairs were found.</summary>
    NoPairedOpens = 0,

    /// <summary>At least one exact-minute pair was found; the readout's figures reflect it.</summary>
    Measured,

    /// <summary>The strategy has no demo (<c>StrategyTrade</c>) rows at all.</summary>
    NoDemoTrades,

    /// <summary>The strategy has no <c>BacktestRun</c> in the caller-named <c>BacktestRunKind</c> slot — never a fallback to the other slot.</summary>
    NoRunForKind,
}
