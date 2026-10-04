namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D5 — the named constants that bound a search. PROVISIONAL: slice 1d sets them from the
/// benchmark, the same way <see cref="FtmoGroupSimulationLimits.MaxMembers"/> was set.
/// </summary>
public static class FtmoGroupSearchLimits
{
    /// <summary>How many candidates survive the proxy and get a full group computation.</summary>
    public const int ShortlistSize = 150;

    /// <summary>The default cap on full group simulations (one per candidate, both kinds).</summary>
    public const int DefaultMaxFullSimulations = ShortlistSize;

    public static readonly TimeSpan DefaultMaxWallClock = TimeSpan.FromMinutes(15);

    /// <summary>The largest eligible pool a search accepts (bounds the cache and the C(P, 2..4) proxy pass).</summary>
    public const int MaxPoolSize = 40;
}
