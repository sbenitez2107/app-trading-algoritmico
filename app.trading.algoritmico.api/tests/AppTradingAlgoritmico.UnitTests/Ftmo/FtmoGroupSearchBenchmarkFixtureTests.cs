using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d.2.1 — shape tests for the closed-form benchmark pool, written BEFORE the benchmark runs on it:
/// P = 24 strategies of the shipped 1,000-trade 'never' series (each shifted, see <see cref="FtmoGroupBenchmarkFixture"/>),
/// every one eligible, none pruned, so the funnel enumerates exactly C(24,2) + C(24,3) + C(24,4) = 12,926 candidates.
/// </summary>
public class FtmoGroupSearchBenchmarkFixtureTests
{
    private static readonly SearchOptions Options = new(2, 4);

    [Fact]
    public void Pool_HasTwentyFourStrategies_EachWithADeployAndADistinctEvaluationSeriesOfOneThousandTrades()
    {
        var cache = FtmoGroupSearchBenchmarkFixture.BuildCache();

        cache.Members.Should().HaveCount(24);
        foreach (var m in cache.Members)
        {
            var deploy = cache.Input(m.StrategyId, BacktestRunKind.Deploy).Projection!.ProjectedLow!;
            var eval = cache.Input(m.StrategyId, BacktestRunKind.Evaluation).Projection!.ProjectedLow!;
            deploy.Should().HaveCount(1_000);
            eval.Should().HaveCount(1_000);
            eval.Select(t => t.OpenSource).Should().NotEqual(deploy.Select(t => t.OpenSource), "an identical pair would be excluded from the funnel");
        }
    }

    [Fact]
    public void Pool_IsEntirelyEligible_AndTheFunnelEnumeratesTwelveThousandNineHundredTwentySixUnprunedCandidates()
    {
        var cache = FtmoGroupSearchBenchmarkFixture.BuildCache();

        var eligibility = EvaluateEligibility(cache, _ => Resolution(), includeIdentical: false);
        var pruned = Prune(cache, eligibility.Eligible, _ => Resolution(), Options, 10_000m, 50m);

        eligibility.Exclusions.Should().BeEmpty();
        eligibility.Eligible.Should().HaveCount(24);
        pruned.Funnel.Should().Be(new Funnel(12_926, 0, 0, 0, 12_926));
        pruned.Survivors.Count(c => c.Length == 2).Should().Be(276);
        pruned.Survivors.Count(c => c.Length == 3).Should().Be(2_024);
        pruned.Survivors.Count(c => c.Length == 4).Should().Be(10_626);
    }

    [Fact]
    public void Pool_AFourMemberCandidate_HasAUsableProxy()
    {
        var cache = FtmoGroupSearchBenchmarkFixture.BuildCache();

        var outcome = ComputeProxy(cache, [.. cache.Members.Take(4).Select(m => m.StrategyId)], Params());

        outcome.Removal.Should().BeNull("a benchmark whose candidates are all removed by the proxy would measure nothing");
    }

    [Fact]
    public void Pool_IsClosedForm_TwoBuildsAreIdentical()
    {
        var a = FtmoGroupSearchBenchmarkFixture.BuildCache(poolSize: 6);
        var b = FtmoGroupSearchBenchmarkFixture.BuildCache(poolSize: 6);

        a.Members.Select(m => m.StrategyId).Should().Equal(b.Members.Select(m => m.StrategyId));
        foreach (var m in a.Members)
        {
            foreach (var kind in FtmoGroupMemberResolution.Kinds)
            {
                a.Input(m.StrategyId, kind).Projection!.ProjectedLow.Should().Equal(b.Input(m.StrategyId, kind).Projection!.ProjectedLow);
            }
        }
    }
}
