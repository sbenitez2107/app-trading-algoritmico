namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Which question a <see cref="AppTradingAlgoritmico.Domain.Entities.BacktestRun"/> is able to
/// answer. The name describes the ANSWER, not which walk-forward window produced the parameters:
/// a strategy running the SQX Optimizer's "original" parameters is still a <see cref="Deploy"/>
/// run, and is correctly reported as not evaluable.
/// <para>
/// There is deliberately NO <c>0</c> member. An unset kind must not silently name a valid slot,
/// and <c>(StrategyId, Kind)</c> is a unique key — a default-valued <c>Kind</c> would quietly
/// collide with a real one.
/// </para>
/// <para>
/// The enum alone stops nothing. What stops a deploy run being read as out-of-sample evidence is
/// <c>OosWindow.Resolver</c>: it is the only way to obtain an OOS boundary, and it refuses to
/// produce one for <see cref="Deploy"/>. See design.md D8.
/// </para>
/// </summary>
public enum BacktestRunKind
{
    /// <summary>Parameters actually running live. Answers sizing, R-normalization, correlation, breach probability — never anything out-of-sample.</summary>
    Deploy = 1,

    /// <summary>Produced from the PREVIOUS walk-forward window's parameters, so trades at/after the export's boundary are genuinely out-of-sample.</summary>
    Evaluation = 2,
}
