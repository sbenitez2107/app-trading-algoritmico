using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Backtests;

/// <summary>
/// One strategy's backtest evidence, as two booleans read straight from the database, plus THE
/// rule that turns them into a marker.
/// <para>
/// The rule lives here rather than in the query so it is stated once, in words, and can be read
/// without reconstructing a LINQ expression: no run at all is <see cref="BacktestReadiness.None"/>;
/// a run without a usable out-of-sample boundary is <see cref="BacktestReadiness.SizingOnly"/> —
/// the strategy can be sized but not honestly evaluated; both together are
/// <see cref="BacktestReadiness.Evaluable"/>.
/// </para>
/// <para>
/// <see cref="HasOosEvidence"/> deliberately requires all three of an Evaluation run, a
/// walk-forward export and at least one trade at or after the boundary. Missing any one of them is
/// amber, not green: an Evaluation run whose export has not been imported yet has an UNKNOWN
/// boundary, and treating unknown as satisfied is the one mistake this marker exists to prevent.
/// </para>
/// </summary>
public sealed record BacktestReadinessRow(Guid StrategyId, bool HasAnyRun, bool HasOosEvidence)
{
    public BacktestReadiness Readiness =>
        !HasAnyRun ? BacktestReadiness.None
        : HasOosEvidence ? BacktestReadiness.Evaluable
        : BacktestReadiness.SizingOnly;
}
