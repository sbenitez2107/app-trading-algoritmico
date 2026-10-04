using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>ftmo-group-search 1b-ii follow-up (RELIABILITY-001): only the job's own token reports a cancel.</summary>
public class FtmoGroupSearchCancelFilterTests
{
    [Fact]
    public void Simulate_AnOperationCanceledFromAnotherSource_PropagatesInsteadOfReportingCancelled()
    {
        var cache = Cache(
            Distinct(1, "A", Trade(0, At(1, 5, 9), At(1, 5, 10), 10m), Trade(1, At(1, 9, 9), At(1, 9, 10), -5m)),
            Distinct(2, "B", Trade(0, At(1, 5, 9), At(1, 5, 10), 10m), Trade(1, At(1, 9, 9), At(1, 9, 10), -5m)));
        var shortlist = Plan(cache, _ => Resolution(), new SearchOptions(2, 2), Params(), 50m).Shortlist;

        var act = () => Simulate(
            cache, shortlist, Params(), new SimulationBudget(int.MaxValue),
            _ => throw new OperationCanceledException("not the job token"), CancellationToken.None);

        act.Should().Throw<OperationCanceledException>();
    }
}
