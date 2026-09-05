using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Backtests;

/// <summary>
/// The outcome of choosing one member's run, with the evidence a refusal has to name (design.md D8a).
/// </summary>
/// <param name="Status">Whether a run was selected, and if not, why not.</param>
/// <param name="Run">The selected run, or null. Null for every non-<c>Resolved</c> status.</param>
/// <param name="CandidateKinds">
/// The kinds that matched the requested segment. Populated for
/// <see cref="GroupRiskMemberStatus.AmbiguousRunSelection"/> so the refusal can name BOTH slots
/// rather than merely reporting that two existed.
/// </param>
/// <param name="DisagreeingRunIds">
/// Runs whose own trades carry more than one segment. Named so the operator can go and look at the
/// rows that were edited.
/// </param>
public sealed record RunSelectionResult(
    GroupRiskMemberStatus Status,
    BacktestRunSegmentRow? Run,
    IReadOnlyList<BacktestRunKind> CandidateKinds,
    IReadOnlyList<Guid> DisagreeingRunIds);

/// <summary>
/// How a member's run is chosen, and it is a bounded two-row question (design.md D8a/D8b).
/// <para>
/// <c>BacktestRunConfiguration</c> declares <c>(StrategyId, Kind)</c> unique, so a strategy has at
/// most one <c>Deploy</c> run and one <c>Evaluation</c> run: this is a CHOICE among at most two
/// rows, never a search.
/// </para>
/// <para>
/// <b><c>Kind</c> and <c>Segment</c> are different axes and nothing maps one to the other.</b>
/// <c>Kind</c> says which parameter set backs the run; <c>Segment</c> says what SQX labelled its
/// trades. A <c>Deploy</c> run's trades can be <c>InSampleTest</c> — that is the AlgoWizard
/// full-period export and the committed IST fixture. Anything that derives one enum from the other
/// is a bug, so nothing here reads <c>Kind</c> except the caller-supplied disambiguator, which is
/// an explicit operator instruction rather than an inference.
/// </para>
/// <para>
/// It takes query sources as arguments rather than a <c>DbContext</c> so this type stays in Domain
/// with no EF dependency — the <c>OosWindow.Resolver</c> precedent.
/// </para>
/// </summary>
public static class RunSegmentSelection
{
    /// <summary>
    /// Every requested strategy's runs with their derived segment aggregates, as ONE server-side
    /// projection for the WHOLE group — the <c>ReadinessRows</c> precedent, one query per analysis
    /// rather than one per member.
    /// <para>
    /// There is no date comparison here and none anywhere else in this slice: the segment is a
    /// property OF the run, so selection happens at run granularity and there is nothing to
    /// partition.
    /// </para>
    /// </summary>
    public static IQueryable<BacktestRunSegmentRow> SegmentRows(
        IQueryable<BacktestRun> runs,
        IQueryable<BacktestTrade> trades,
        IReadOnlyCollection<Guid> strategyIds)
        => runs
            .Where(r => strategyIds.Contains(r.StrategyId))
            .Select(r => new BacktestRunSegmentRow(
                r.Id,
                r.StrategyId,
                r.Kind,
                // (int?) is load-bearing: Min over an empty set must stay NULL rather than
                // collapsing onto 0, which is BacktestSegment.Unknown.
                trades.Where(t => t.BacktestRunId == r.Id).Min(t => (int?)t.Segment),
                trades.Where(t => t.BacktestRunId == r.Id).Max(t => (int?)t.Segment)));

    /// <summary>
    /// Chooses the run that carries <paramref name="requested"/> from one strategy's rows.
    /// <para>
    /// Order of decision, and it matters. A DISAGREEING run refuses the member first, before any
    /// matching, because a run holding two sample types means the store has been edited and no
    /// figure drawn from that strategy can be trusted — including one drawn from its other run.
    /// Trade-less runs are then EXCLUDED rather than refused: a half-populated strategy (one slot
    /// imported, the other not yet) is the normal intermediate state of the two-row constraint, and
    /// the member must still resolve from whichever run does match. Only then is the segment
    /// matched, and a run whose derived segment is <see cref="BacktestSegment.Unknown"/> is dropped
    /// from the candidates whatever was asked for.
    /// </para>
    /// </summary>
    /// <param name="rows">One strategy's rows — at most two, by the unique index.</param>
    /// <param name="requested">The sample the operator asked for.</param>
    /// <param name="runKind">
    /// The operator's explicit disambiguator, or null. It only ever NARROWS the candidates that
    /// already matched the segment; it never selects a run whose segment does not match, because
    /// that would let <c>Kind</c> override <c>Segment</c>.
    /// </param>
    public static RunSelectionResult Select(
        IReadOnlyList<BacktestRunSegmentRow> rows,
        BacktestSegment requested,
        BacktestRunKind? runKind)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var disagreeing = rows
            .Where(r => r.State == BacktestRunSegmentState.Disagreeing)
            .Select(r => r.RunId)
            .ToList();

        if (disagreeing.Count > 0)
            return new RunSelectionResult(GroupRiskMemberStatus.RunSegmentsDisagree, null, [], disagreeing);

        var candidates = rows
            // A trade-less run contributes NOTHING to the match. It is not evidence and it is not
            // an error either — it is simply absent from the question.
            .Where(r => r.State == BacktestRunSegmentState.Resolved)
            // An Unknown run carries a label the parser could not classify. Its raw text survives,
            // but its meaning is unestablished, so it is never selected — including when Unknown is
            // itself what was requested.
            .Where(r => r.Segment != BacktestSegment.Unknown)
            .Where(r => r.Segment == requested)
            .ToList();

        if (runKind is not null)
            candidates = candidates.Where(r => r.Kind == runKind.Value).ToList();

        return candidates.Count switch
        {
            1 => new RunSelectionResult(GroupRiskMemberStatus.Resolved, candidates[0], [candidates[0].Kind], []),
            0 => new RunSelectionResult(GroupRiskMemberStatus.NoEvidenceForSegment, null, [], []),
            _ => new RunSelectionResult(
                GroupRiskMemberStatus.AmbiguousRunSelection,
                null,
                [.. candidates.Select(c => c.Kind).OrderBy(k => k)],
                []),
        };
    }

    /// <summary>
    /// The members whose selected runs do not all carry the same segment, or an empty list when
    /// they do.
    /// <para>
    /// A correlation or VaR figure implies ONE sample label. When the members disagree there is no
    /// label that is true of the whole group, so the analysis is refused and every member is named
    /// with its own segment — the operator has to be able to see WHICH ones disagreed, not merely
    /// that someone did. Computing it with a "mixed" label or taking the majority segment are both
    /// rejected: the first publishes a number that means nothing in particular, the second invents
    /// a claim about the minority.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Label, BacktestSegment Segment)> DisagreeingSegments(
        IReadOnlyList<(string Label, BacktestSegment Segment)> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        return members.Select(m => m.Segment).Distinct().Count() > 1 ? members : [];
    }
}
