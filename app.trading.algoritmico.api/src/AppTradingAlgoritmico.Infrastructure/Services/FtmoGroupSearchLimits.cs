namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D5 — the named constants that bound a search, set from the 1d benchmark the same way
/// <see cref="FtmoGroupSimulationLimits.MaxMembers"/> was (table in <c>FtmoGroupSearchBenchmarkTests</c>, relations
/// pinned by <c>FtmoGroupSearchLimitsTests</c>). Measured in Release on 12 logical cores: the proxy stage at P = 24
/// (12,926 candidates) takes about 56 s, and one candidate's full computation (both kinds) about 3 s at its slowest.
/// </summary>
public static class FtmoGroupSearchLimits
{
    /// <summary>
    /// How many candidates survive the proxy and get a full group computation. 75 = a per-size quota of 25 over sizes
    /// 2..4: the calibration's DEPTH d100 = 25 for k = 2, 3 and 4 (design D9), and a real Debug run (about 18 s per full
    /// simulation) reached only 51 of the old 150 at its budget.
    /// </summary>
    public const int ShortlistSize = 75;

    /// <summary>The default cap on full group simulations (one per candidate, both kinds).</summary>
    public const int DefaultMaxFullSimulations = ShortlistSize;

    /// <summary>
    /// The default cap on strategies of one instrument per candidate (user-adjustable). User decision 2026-10-04: 2, not 1.
    /// The first real calibration (SBDEMO2: 130 strategies, 18 eligible) covered only 2 instruments (gold, NQ), so a cap
    /// of 1 left only pairs and a recall of 1.0 that was vacuous (the shortlist equalled the survivor set).
    /// </summary>
    public const int DefaultMaxPerInstrument = 2;

    /// <summary>30 min: 75 simulations at the Debug-build 18 s each (22.5 min) fit it.</summary>
    public static readonly TimeSpan DefaultMaxWallClock = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The largest eligible pool a search accepts (bounds the cache and the C(P, 2..4) proxy pass): the largest P whose
    /// proxy pass stays within the 60 s gate. P = 24 measured 56 s at the worst median, with a thin margin (one single
    /// sample reached 60.9 s); P = 32 measured 183 s and P = 40 485 s.
    /// </summary>
    public const int MaxPoolSize = 24;

    /// <summary>The largest full-simulation budget a request may ask for (2b validation): about 3x the default.</summary>
    public const int MaxFullSimulationsCeiling = 500;

    /// <summary>The largest wall-clock budget, in seconds, a request may ask for (2b validation): one hour.</summary>
    public const int MaxWallClockSecondsCeiling = 3600;
}
