namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B2 (spec.md "The Member Count Is Capped By A Named Constant Set From The
/// Benchmark") — the single source of truth for the member cap and the three group disclosures.
/// </summary>
public static class FtmoGroupSimulationLimits
{
    /// <summary>
    /// The most DISTINCT members one group request may carry: <c>min(8, the largest k whose group benchmark
    /// Never median is within 5 s in BOTH recorded runs)</c>. Measured (after per-start parallelism in
    /// <c>ComputeRun</c>): k=4 passes with 3.785 s and 3.468 s; k=6 fails with 7.081 s and 6.831 s. The group
    /// benchmark asserts at exactly this value, so raising it forces a new measurement.
    /// </summary>
    public const int MaxMembers = 4;

    /// <summary>The three group texts that accompany every group result and refusal (proposal D10).</summary>
    public static IReadOnlyList<string> Disclosures { get; } =
    [
        "In a group, breaches caused by a concurrent open position are expected to dominate, so the "
            + "Clean/Contingent split of a breach loses its usual meaning.",
        "When several trades close at an identical instant, the order in which those closes are applied is "
            + "a modelling choice that can create or remove a breach.",
        "Eligibility rules of the funding programme, for example a martingale or grid ban, are not modelled.",
    ];
}
