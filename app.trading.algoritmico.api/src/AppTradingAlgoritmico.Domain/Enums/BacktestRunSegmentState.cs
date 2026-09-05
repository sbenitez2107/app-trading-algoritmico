namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// What a run's own trades say about which sample the run belongs to (design.md D8/D8a).
/// <para>
/// <c>BacktestRun</c> carries NO segment column — the segment lives solely on
/// <c>BacktestTrade.Segment</c> — so a run's segment is DERIVED from <c>Min</c>/<c>Max</c> over its
/// trades, and the derivation has three outcomes rather than one. Collapsing them onto a single
/// nullable segment would lose the distinction between "there is nothing to read" and "what is
/// there contradicts itself", which are handled differently: the first is non-fatal to the member,
/// the second is a refusal.
/// </para>
/// </summary>
public enum BacktestRunSegmentState
{
    /// <summary>
    /// The run holds no trades, so it has NO segment and offers NO evidence. It is never coerced to
    /// <see cref="BacktestSegment.Unknown"/>: <c>Unknown</c> is a label the parser ASSIGNS to a
    /// sample type it could not classify, not a stand-in for absence. A run ROW is not evidence —
    /// its trades are.
    /// </summary>
    NoTrades = 0,

    /// <summary>
    /// The run's trades carry more than one segment. <c>SqxTradeListParserService</c> rejects any
    /// file holding more than one <c>Sample type</c>, so this is reachable only through a
    /// hand-edited database — an invariant this design DEPENDS on, checked rather than assumed.
    /// </summary>
    Disagreeing,

    /// <summary>Every trade agrees, so the run carries exactly one segment.</summary>
    Resolved,
}
