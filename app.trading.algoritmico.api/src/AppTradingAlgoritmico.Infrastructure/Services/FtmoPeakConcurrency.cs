using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D3 — the peak number of simultaneously open rows of one series. <c>internal static</c>, pure.
/// <para>
/// It MIRRORS the private sweep in <c>FtmoGroupDiagnostics.Peak</c> (that file is not edited, so the sweep is
/// copied here rather than shared): only scalable rows (<c>Net != null</c>) with <c>Close &gt; Open</c> count, and
/// at an identical instant closes are processed BEFORE opens, so a close and an open at the same instant do not
/// overlap. A parity test pins this to the peak <c>ComputeGroup</c> reports.
/// </para>
/// </summary>
internal static class FtmoPeakConcurrency
{
    /// <summary>The peak over a merged series' low end (open and close instants do not depend on the FX end).</summary>
    internal static int Compute(MergedSeries merged)
    {
        ArgumentNullException.ThrowIfNull(merged);
        return Compute(merged.Low);
    }

    internal static int Compute(IReadOnlyList<ProjectedTrade> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var events = new List<(DateTime Instant, int Kind)>();
        foreach (var row in rows)
        {
            if (row.Net is null || row.CloseSource <= row.OpenSource)
                continue;

            events.Add((row.OpenSource, 1));
            events.Add((row.CloseSource, 0));
        }

        // Kind 0 (close) sorts before kind 1 (open) at the same instant.
        events.Sort((x, y) =>
        {
            var byInstant = x.Instant.CompareTo(y.Instant);
            return byInstant != 0 ? byInstant : x.Kind.CompareTo(y.Kind);
        });

        var open = 0;
        var peak = 0;
        foreach (var (_, kind) in events)
        {
            if (kind == 0)
            {
                open--;
                continue;
            }

            open++;
            if (open > peak)
                peak = open;
        }

        return peak;
    }
}
