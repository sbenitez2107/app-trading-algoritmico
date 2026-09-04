namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The outcome of converting an already-sized backtest series into a dated net projection
/// (design.md D3). It carries only the outcomes that are legitimate DATA conditions — a pairing
/// failure is a programming error and throws instead of appearing here.
/// </summary>
public enum BacktestNetSeriesStatus
{
    /// <summary>The series exists. Possession of it is proof the member's weight was checked.</summary>
    Built = 0,

    /// <summary>
    /// The member carries a <c>PortfolioStrategy.Weight != 1</c> and no series was produced. The
    /// refusal is unconditional on the value: <c>1.5</c> double-sizes, <c>0.5</c> half-sizes and
    /// <c>0</c> is an exclusion expressed in the wrong place — all three are the SAME error,
    /// because the series' own <c>TargetRiskPerTrade</c> is the sizing decision and there is no
    /// second one to make.
    /// </summary>
    NonUnitWeight,
}
