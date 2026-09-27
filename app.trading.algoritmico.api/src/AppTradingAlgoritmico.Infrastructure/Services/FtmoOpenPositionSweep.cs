namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-challenge-race (design.md Decision 4) — an O(n log n) sweep answering "is any scalable
/// position open at instant <c>T</c>". <c>internal static</c>, pure: no I/O.
/// <para>
/// <c>openAt(T) = #{Open &lt; T} − #{Close ≤ T ∧ Open &lt; T}</c>, over scalable trades only
/// (<c>Net is not null</c>). <c>Unscalable</c> rows were never opened on FTMO, so they are excluded
/// entirely — never counted as an open sibling that would defer a target decision.
/// </para>
/// <para>
/// This is deliberately NOT the shipped <c>FtmoBreachEvaluator.HasConcurrentOpenPosition</c>, which
/// is O(n²) and counts <c>Unscalable</c> rows: that method stays byte-identical for the shipped
/// breach replay (hard rule 1), and new code has no reason to inherit its quadratic cost or its
/// different Unscalable treatment.
/// </para>
/// </summary>
internal static class FtmoOpenPositionSweep
{
    /// <summary>
    /// True when at least one scalable trade is open strictly before and closes strictly after
    /// <paramref name="cutoff"/> (<c>Open &lt; cutoff &lt; Close</c>). A trade closing exactly at
    /// <paramref name="cutoff"/> is treated as closed by then, not open.
    /// <para>
    /// Ordering precondition: NONE. This single linear pass tallies each trade against
    /// <paramref name="cutoff"/> independently — unlike a classic event-sweep (sort, then walk), it
    /// never relies on <paramref name="trades"/> being pre-sorted by <c>Open</c>/<c>Close</c>/<c>RowIndex</c>,
    /// and produces the identical result for any permutation of the same trade set. The caller is free
    /// to pass trades in <c>CloseSource</c>/<c>RowIndex</c> order (as <see cref="FtmoChallengeRace"/>
    /// does) purely for its OWN scanning convenience, not because this method requires it.
    /// </para>
    /// </summary>
    internal static bool OpenAt(IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades, DateTime cutoff)
    {
        ArgumentNullException.ThrowIfNull(trades);

        var opensBeforeCutoff = 0;
        var closesAtOrBeforeCutoffAmongThoseOpened = 0;

        foreach (var trade in trades)
        {
            if (trade.Net is null)
                continue; // Unscalable — never opened on FTMO.

            if (trade.OpenSource < cutoff)
            {
                opensBeforeCutoff++;

                if (trade.CloseSource <= cutoff)
                    closesAtOrBeforeCutoffAmongThoseOpened++;
            }
        }

        return opensBeforeCutoff - closesAtOrBeforeCutoffAmongThoseOpened > 0;
    }
}
