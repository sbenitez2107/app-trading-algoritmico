using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Backtests;

/// <summary>
/// One backtest run's segment, as the two aggregates a single server-side projection can read off
/// its trades, plus THE rule that turns them into a segment (design.md D8a).
/// <para>
/// The rule lives here rather than inside the query so it is stated once, in words, and can be read
/// without reconstructing a LINQ expression — the <c>BacktestReadinessRow</c> precedent.
/// </para>
/// <para>
/// <b>Both aggregates, not one.</b> <c>Min</c> alone would answer the question, and would also
/// silently accept a run whose trades disagree by reporting the smaller label. The parser rejects
/// any file carrying more than one <c>Sample type</c>, so this design DEPENDS on that invariant;
/// taking <c>Max</c> as well costs one extra SQL aggregate and converts a dependency into a check.
/// </para>
/// <para>
/// <b>The aggregates are <see cref="int"/>?, deliberately.</b> <c>Min</c> over an empty set is
/// null, and only a nullable projection keeps it null. A non-nullable one collapses it onto
/// <c>0</c> — which IS <see cref="BacktestSegment.Unknown"/> — turning "this run holds no trades"
/// into "this run's sample could not be classified". Those are different claims and the second one
/// is false.
/// </para>
/// </summary>
/// <param name="RunId">The run the row describes — what a refusal names.</param>
/// <param name="StrategyId">The owning strategy. A strategy has at most one row per <see cref="BacktestRunKind"/>.</param>
/// <param name="Kind">Which slot the run occupies. Independent of <see cref="Segment"/> — see <see cref="RunSegmentSelection"/>.</param>
/// <param name="MinSegment">Smallest <c>BacktestSegment</c> across the run's trades, or null when it has none.</param>
/// <param name="MaxSegment">Largest, for the same set. Equal to <paramref name="MinSegment"/> whenever the parser's invariant holds.</param>
public sealed record BacktestRunSegmentRow(
    Guid RunId,
    Guid StrategyId,
    BacktestRunKind Kind,
    int? MinSegment,
    int? MaxSegment)
{
    /// <summary>What the run's own trades say: nothing, something contradictory, or one segment.</summary>
    public BacktestRunSegmentState State =>
        MinSegment is null ? BacktestRunSegmentState.NoTrades
        : MinSegment != MaxSegment ? BacktestRunSegmentState.Disagreeing
        : BacktestRunSegmentState.Resolved;

    /// <summary>
    /// The run's segment, or null when there is not exactly one. Null is returned for BOTH
    /// non-resolved states on purpose — a caller that only needs "is there a segment" should not
    /// have to know which kind of absence it is, while a caller that must REFUSE reads
    /// <see cref="State"/>.
    /// </summary>
    public BacktestSegment? Segment =>
        State == BacktestRunSegmentState.Resolved ? (BacktestSegment)MinSegment!.Value : null;
}
