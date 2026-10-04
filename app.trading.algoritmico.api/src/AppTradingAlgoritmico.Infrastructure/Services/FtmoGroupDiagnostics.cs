using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B3 (design.md D6) — pure diagnostics of one SUCCESSFUL kind, derived from the merged
/// series, its row map and the kind's <see cref="FtmoMultiStartRunDto"/>. Nothing here reads or modifies the
/// evaluator: close instants and null nets do not depend on the FX end, so the low series answers every
/// question that is not a net.
/// </summary>
internal static class FtmoGroupDiagnostics
{
    /// <param name="members">Index == <see cref="MemberSeries.MemberOrder"/> (ascending StrategyId).</param>
    internal static FtmoGroupDiagnosticsDto Compute(
        IReadOnlyList<(Guid StrategyId, string Name)> members, MergedSeries merged, FtmoMultiStartRunDto run)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(run);

        return new FtmoGroupDiagnosticsDto(
            Contributions(members, merged), Attribute(members, merged, run), Peak(members, merged));
    }

    private static List<FtmoGroupMemberContributionDto> Contributions(
        IReadOnlyList<(Guid StrategyId, string Name)> members, MergedSeries merged)
    {
        var inWindow = new int[members.Count];
        var scalable = new int[members.Count];
        var raised = new int[members.Count];
        var capped = new int[members.Count];
        var unscalable = new int[members.Count];
        var netLow = new decimal[members.Count];
        var netHigh = new decimal[members.Count];

        for (var r = 0; r < merged.Low.Count; r++)
        {
            var member = merged.RowMap[r].MemberOrder;
            var low = merged.Low[r];

            inWindow[member]++;
            netLow[member] += low.Net ?? 0m;
            netHigh[member] += merged.High[r].Net ?? 0m;
            switch (low.Outcome)
            {
                case ResizeOutcome.Unscalable:
                    unscalable[member]++;
                    break;
                case ResizeOutcome.RaisedToMinimum:
                    scalable[member]++;
                    raised[member]++;
                    break;
                case ResizeOutcome.CappedAtMaximum:
                    scalable[member]++;
                    capped[member]++;
                    break;
                default:
                    scalable[member]++;
                    break;
            }
        }

        return [.. members.Select((m, i) => new FtmoGroupMemberContributionDto(
            m.StrategyId, m.Name, inWindow[i], scalable[i], netLow[i], netHigh[i], raised[i], capped[i], unscalable[i]))];
    }

    /// <summary>
    /// The deciding breach of a start is its Phase 1 breach, else its Phase 2 breach, else its Funded breach.
    /// Every member with a candidate row at that close is credited (the evaluator does not report which row
    /// breached): same close instant, <c>Net</c> present, <c>Open &gt;= start anchor</c>, and the phase-subset rule
    /// <c>Open &gt;= T &amp;&amp; Close &gt; T</c> (T = the previous phase's close; same as the race and funded phase).
    /// </summary>
    private static FtmoGroupAttributionDto Attribute(
        IReadOnlyList<(Guid StrategyId, string Name)> members, MergedSeries merged, FtmoMultiStartRunDto run)
    {
        var rowsByClose = new Dictionary<DateTime, List<int>>();
        for (var r = 0; r < merged.Low.Count; r++)
        {
            if (merged.Low[r].Net is null)
                continue;

            if (!rowsByClose.TryGetValue(merged.Low[r].CloseSource, out var rows))
                rowsByClose[merged.Low[r].CloseSource] = rows = [];
            rows.Add(r);
        }

        var phase1 = new int[members.Count];
        var phase2 = new int[members.Count];
        var funded = new int[members.Count];
        var sole = new int[members.Count];
        var shared = new int[members.Count];
        var deciding = 0;
        var sharedStarts = 0;
        var unattributed = 0;

        foreach (var start in run.Starts)
        {
            var (phaseCounts, close, subsetFloor) = DecidingBreach(start, phase1, phase2, funded);
            if (close is null)
                continue;

            deciding++;
            var credited = new SortedSet<int>();
            if (rowsByClose.TryGetValue(close.Value, out var candidates))
            {
                foreach (var r in candidates)
                {
                    var row = merged.Low[r];
                    if (row.OpenSource >= start.StartSourceOpen
                        && (subsetFloor is null || (row.OpenSource >= subsetFloor && row.CloseSource > subsetFloor)))
                    {
                        credited.Add(merged.RowMap[r].MemberOrder);
                    }
                }
            }

            if (credited.Count == 0)
            {
                unattributed++;
                continue;
            }

            foreach (var member in credited)
                phaseCounts[member]++;

            if (credited.Count == 1)
            {
                sole[credited.Min]++;
            }
            else
            {
                sharedStarts++;
                foreach (var member in credited)
                    shared[member]++;
            }
        }

        return new FtmoGroupAttributionDto(
            deciding, sharedStarts, unattributed,
            [.. members.Select((m, i) => new FtmoGroupMemberAttributionDto(
                m.StrategyId, m.Name, phase1[i], phase2[i], funded[i], sole[i], shared[i]))]);
    }

    private static (int[] PhaseCounts, DateTime? Close, DateTime? SubsetFloor) DecidingBreach(
        FtmoMultiStartRowDto start, int[] phase1, int[] phase2, int[] funded)
    {
        if (start.Phase1.Outcome == FtmoPhaseOutcome.BreachedFirst)
            return (phase1, start.Phase1.OutcomeSourceClose, null);

        if (start.Phase2.Outcome == FtmoPhaseOutcome.BreachedFirst)
            return (phase2, start.Phase2.OutcomeSourceClose, start.Phase1.OutcomeSourceClose);

        if (start.Funded.Outcome == FtmoFundedOutcome.BreachedFirst)
            return (funded, start.Funded.OutcomeSourceClose, start.Phase2.OutcomeSourceClose);

        return (phase1, null, null);
    }

    /// <summary>
    /// Sweep over in-window scalable rows with <c>Close &gt; Open</c> (a zero-duration row is never open). At an
    /// identical instant closes are processed BEFORE opens, so a close and an open at the same instant do not
    /// overlap (the open-position sweep's convention and the evaluator's strict overlap).
    /// </summary>
    private static FtmoGroupPeakConcurrencyDto Peak(IReadOnlyList<(Guid StrategyId, string Name)> members, MergedSeries merged)
    {
        var events = new List<(DateTime Instant, int Kind, int Row)>();
        for (var r = 0; r < merged.Low.Count; r++)
        {
            var row = merged.Low[r];
            if (row.Net is null || row.CloseSource <= row.OpenSource)
                continue;

            events.Add((row.OpenSource, 1, r));
            events.Add((row.CloseSource, 0, r));
        }

        events.Sort((x, y) =>
        {
            var byInstant = x.Instant.CompareTo(y.Instant);
            if (byInstant != 0)
                return byInstant;

            var byKind = x.Kind.CompareTo(y.Kind);
            return byKind != 0 ? byKind : x.Row.CompareTo(y.Row);
        });

        var open = new HashSet<int>();
        var peak = 0;
        DateTime? first = null;
        var atPeak = new SortedSet<int>();
        foreach (var (instant, kind, row) in events)
        {
            if (kind == 0)
            {
                open.Remove(row);
                continue;
            }

            open.Add(row);
            if (open.Count > peak)
            {
                peak = open.Count;
                first = instant;
                atPeak = [.. open.Select(r => merged.RowMap[r].MemberOrder)];
            }
        }

        return new FtmoGroupPeakConcurrencyDto(peak, first, [.. atPeak.Select(m => members[m].StrategyId)]);
    }
}
