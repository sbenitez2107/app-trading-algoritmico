namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why a backtest-derived VaR figure is absent (design.md D4/D4b). A withheld figure is
/// <c>null</c>, NEVER <c>0</c> — a numeric zero reads as "this portfolio loses nothing at the 5th
/// percentile", which is a claim the data does not make.
/// <para>
/// This enum accompanies the figure rather than replacing it: the operator is shown the reason AND
/// the density counts that produced it, exactly as slice 2a's <c>RunRiskEstimate</c> keeps the
/// evidence that survived a rejection.
/// </para>
/// </summary>
public enum VarWithholdReason
{
    /// <summary>Nothing was withheld — the figure beside this value is present.</summary>
    None = 0,

    /// <summary>
    /// There is no dated series at all (no member contributed a net), so no percentile exists to
    /// take. Distinct from a series that exists but cannot support the read.
    /// </summary>
    NoSeries,

    /// <summary>
    /// The series is shorter than <c>AnalyticsSeries.MinHistoryDays</c>, so the monthly estimator
    /// produces no figure. This is the SHIPPED history floor, unchanged by this slice, and it is
    /// not the density gate.
    /// </summary>
    InsufficientHistory,

    /// <summary>
    /// The density gate: the series does not hold enough strictly-negative observations for the
    /// percentile's read index to land on a loss at all
    /// (<c>negativeCount &lt; floor(p * (N-1)) + 1</c>). Evaluated independently PER confidence
    /// level, so a run can withhold VaR95 while reporting VaR99.
    /// </summary>
    InsufficientNegativeObservations,
}
