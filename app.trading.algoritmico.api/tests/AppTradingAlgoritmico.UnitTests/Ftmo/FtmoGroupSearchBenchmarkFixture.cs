using AppTradingAlgoritmico.Infrastructure.Services;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d.2.1 — test-only support for the search benchmark: a pool of P strategies built from the shipped
/// group fixture (<see cref="FtmoGroupBenchmarkFixture"/>: the 1,000-trade 'never' series, member <c>m</c> shifted by
/// <c>m x spacing / P</c> hours). Every strategy has its own symbol (so the per-instrument cap prunes nothing), the same
/// source zone and a common window (so no pair conflicts), and an Evaluation series one hour later than its Deploy
/// series (so none is an identical Deploy/Evaluation member). Closed-form: no <see cref="Random"/>.
/// </summary>
internal static class FtmoGroupSearchBenchmarkFixture
{
    internal const int PoolSize = 24;

    internal static FtmoProjectionCache BuildCache(int poolSize = PoolSize)
    {
        var members = FtmoGroupBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Never, poolSize);
        return Cache([.. members.Select(m => new Strat(
            m.MemberOrder + 1,
            $"SYM{m.MemberOrder + 1}",
            [.. m.Low],
            [.. m.Low.Select(t => t with { OpenSource = t.OpenSource.AddHours(1), CloseSource = t.CloseSource.AddHours(1) })]))]);
    }
}
