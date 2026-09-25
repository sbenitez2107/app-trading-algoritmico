namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Whether a breach simulation produced findings at all (design.md Decision 6). A refused run
/// carries NO findings — the <c>TryNormalize</c> null-profile precedent.
/// <para>
/// Zero-value choice: <see cref="Refused"/> is <c>0</c> because it is the safe null-object default
/// — it asserts nothing was computed and forces a caller to check the refusal reason before
/// touching any finding. <see cref="Evaluated"/> asserts a real computation happened, which a
/// default-initialized value must never claim for free.
/// </para>
/// </summary>
public enum FtmoSimulationStatus
{
    /// <summary>No findings were produced. See the accompanying <see cref="FtmoSimulationRefusal"/>.</summary>
    Refused = 0,

    /// <summary>Findings were produced for every evaluated limit.</summary>
    Evaluated = 1,
}
