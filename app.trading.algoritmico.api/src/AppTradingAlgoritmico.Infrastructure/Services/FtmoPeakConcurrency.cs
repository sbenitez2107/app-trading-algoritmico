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

    [ThreadStatic]
    private static long[]? opens;

    [ThreadStatic]
    private static long[]? closes;

    internal static int Compute(IReadOnlyList<ProjectedTrade> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // Thread-owned scratch: a search calls this on every candidate, and an event list per call was most of its garbage.
        if (opens is null || opens.Length < rows.Count)
        {
            opens = new long[Math.Max(rows.Count, 256)];
            closes = new long[opens.Length];
        }

        var openTicks = opens;
        var closeTicks = closes!;
        var count = 0;
        foreach (var row in rows)
        {
            if (row.Net is null || row.CloseSource <= row.OpenSource)
                continue;

            openTicks[count] = row.OpenSource.Ticks;
            closeTicks[count] = row.CloseSource.Ticks;
            count++;
        }

        Array.Sort(openTicks, 0, count);
        Array.Sort(closeTicks, 0, count);

        // A close at the same instant as an open is processed first, so the two do not overlap.
        int o = 0, c = 0, open = 0, peak = 0;
        while (o < count)
        {
            if (closeTicks[c] <= openTicks[o])
            {
                open--;
                c++;
                continue;
            }

            open++;
            o++;
            if (open > peak)
                peak = open;
        }

        return peak;
    }
}
