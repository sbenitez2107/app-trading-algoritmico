namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// A run whose <c>Â</c> was successfully estimated, together with every trade labelled by risk
/// basis (design.md D4).
/// <para>
/// Holding one of these is PROOF the estimate passed. It is the only shape in which per-trade rows
/// exist, and <c>TradeRiskNormalizer.TryNormalize</c> is the only way to obtain it — a refused run
/// yields <c>null</c> rather than a profile whose <see cref="Trades"/> are all
/// <c>RiskBasis.Unavailable</c>. That distinction is the whole point: an empty-of-information
/// collection is still a collection, and a caller would iterate, count and average it exactly as if
/// it carried evidence.
/// </para>
/// </summary>
/// <param name="Estimate">The run's <c>Â</c> and the fractions that justified it.</param>
/// <param name="Trades">One row per input trade, in input order.</param>
public sealed record RunRiskProfile(
    RunRiskEstimate Estimate,
    IReadOnlyList<NormalizedTrade> Trades);
