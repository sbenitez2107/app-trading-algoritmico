using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b.2 (design D3) — eligibility. Every excluded strategy is listed with its reason (nothing
/// is dropped silently) and eligible + exclusions always equals the pool size P.
/// </summary>
public class FtmoGroupSearchEligibilityTests
{
    private static Strat Good(int n, string symbol = "EURUSD") =>
        Distinct(n, symbol, Trade(0, At(1, 12, 9), At(1, 12, 10), 10m), Trade(1, At(1, 14, 9), At(1, 14, 11), -5m));

    private static FtmoGroupSearchEngine.EligibilityResult Run(bool includeIdentical, Func<string?, FtmoSimulationInputs.SymbolResolution>? resolve, params Strat[] pool) =>
        FtmoGroupSearchEngine.EvaluateEligibility(Cache(pool), resolve ?? (_ => Resolution()), includeIdentical);

    private static FtmoGroupSearchEngine.EligibilityResult Run(params Strat[] pool) => Run(false, null, pool);

    private static void ShouldReconcile(FtmoGroupSearchEngine.EligibilityResult r, int pool) =>
        (r.Eligible.Count + r.Exclusions.Count).Should().Be(pool).And.Be(r.PoolSize);

    [Fact]
    public void Evaluate_AFullyUsableStrategy_IsEligible_AndThePoolIsListedInAscendingIdOrder()
    {
        var r = Run(Good(3), Good(1), Good(2));

        r.Eligible.Should().Equal(Id(1), Id(2), Id(3));
        r.Exclusions.Should().BeEmpty();
        ShouldReconcile(r, 3);
    }

    [Fact]
    public void Evaluate_AStrategyMissingAKind_IsExcludedWithMissingKind()
    {
        var onlyDeploy = Good(2) with { Eval = null };

        var r = Run(Good(1), onlyDeploy, Good(3));

        r.Exclusions.Should().ContainSingle().Which.Should().Match<FtmoGroupSearchEngine.Exclusion>(
            e => e.StrategyId == Id(2) && e.Reason == FtmoGroupSearchExclusionReason.MissingKind);
        r.Eligible.Should().Equal(Id(1), Id(3));
        ShouldReconcile(r, 3);
    }

    [Theory]
    [InlineData(FtmoSimulationRefusal.InstrumentSpecMissing)]
    [InlineData(FtmoSimulationRefusal.PointValueNotCalibrated)]
    [InlineData(FtmoSimulationRefusal.FxRateNotDeclared)]
    public void Evaluate_ASymbolLevelRefusal_ExcludesTheStrategyAndCarriesThatRefusal(FtmoSimulationRefusal refusal)
    {
        var r = Run(Good(1), Good(2) with { SymbolRefusal = refusal });

        var excluded = r.Exclusions.Should().ContainSingle().Subject;
        excluded.Reason.Should().Be(FtmoGroupSearchExclusionReason.SymbolRefused);
        excluded.Refusal.Should().Be(refusal);
        r.Eligible.Should().Equal(Id(1));
    }

    [Fact]
    public void Evaluate_NonUsdWithoutABand_IsExcluded_WhileUsdStrategiesStillFormTheEligiblePool()
    {
        var eur = Good(3, "EURGBP") with { SymbolRefusal = FtmoSimulationRefusal.FxRateNotDeclared };

        var r = Run(Good(1), Good(2), eur);

        r.Eligible.Should().Equal(Id(1), Id(2));
        r.Exclusions.Single().Refusal.Should().Be(FtmoSimulationRefusal.FxRateNotDeclared);
    }

    [Fact]
    public void Evaluate_AnUnresolvedZone_IsExcluded()
    {
        FtmoSimulationInputs.SymbolResolution Resolve(string? s) =>
            s == "BAD" ? Resolution(zoneRefusal: FtmoSimulationRefusal.TimeZoneDataUnavailable) : Resolution();

        var r = Run(false, Resolve, Good(1), Good(2, "BAD"));

        r.Exclusions.Should().ContainSingle().Which.Reason.Should().Be(FtmoGroupSearchExclusionReason.ZoneUnresolved);
        r.Eligible.Should().Equal(Id(1));
    }

    [Fact]
    public void Evaluate_ARefusedProjection_IsExcludedWithItsRefusal()
    {
        var r = Run(Good(1), Good(2) with { ProjectionRefusal = FtmoSimulationRefusal.RiskNotEstimable });

        var excluded = r.Exclusions.Should().ContainSingle().Subject;
        excluded.Reason.Should().Be(FtmoGroupSearchExclusionReason.ProjectionRefused);
        excluded.Refusal.Should().Be(FtmoSimulationRefusal.RiskNotEstimable);
    }

    [Fact]
    public void Evaluate_ARowlessProjection_IsExcluded()
    {
        var r = Run(Good(1), new Strat(2, "EURUSD", [], []));

        r.Exclusions.Should().ContainSingle().Which.Reason.Should().Be(FtmoGroupSearchExclusionReason.ProjectionRowless);
    }

    [Fact]
    public void Evaluate_IdenticalDeployAndEval_IsExcludedByDefault()
    {
        var rows = new[] { Trade(0, At(1, 12, 9), At(1, 12, 10), 10m) };
        var identical = new Strat(2, "EURUSD", rows, rows);

        var r = Run(Good(1), identical);

        r.Exclusions.Should().ContainSingle().Which.Reason.Should().Be(FtmoGroupSearchExclusionReason.IdenticalDeployEval);
        r.Eligible.Should().Equal(Id(1));
    }

    [Fact]
    public void Evaluate_IdenticalDeployAndEval_OptInIncludesAndFlagsIt()
    {
        var rows = new[] { Trade(0, At(1, 12, 9), At(1, 12, 10), 10m) };
        var identical = new Strat(2, "EURUSD", rows, rows);

        var r = Run(true, null, Good(1), identical);

        r.Eligible.Should().Equal(Id(1), Id(2));
        r.IdenticalDeployEval.Should().BeEquivalentTo([Id(2)]);
        r.Exclusions.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_SeriesThatDifferOnlyInANet_AreNotIdentical()
    {
        var deploy = new[] { Trade(0, At(1, 12, 9), At(1, 12, 10), 10m) };
        var eval = new[] { Trade(0, At(1, 12, 9), At(1, 12, 10), 10.5m) };

        var r = Run(Good(1), new Strat(2, "EURUSD", deploy, eval));

        r.Eligible.Should().Contain(Id(2));
        r.IdenticalDeployEval.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_EligiblePlusExclusions_AlwaysEqualsThePool_AndEveryReasonIsListed()
    {
        var pool = new[]
        {
            Good(1),
            Good(2) with { Eval = null },
            Good(3) with { SymbolRefusal = FtmoSimulationRefusal.InstrumentSpecMissing },
            Good(4) with { ProjectionRefusal = FtmoSimulationRefusal.RiskNotEstimable },
            new Strat(5, "EURUSD", [], []),
        };

        var r = Run(pool);

        ShouldReconcile(r, 5);
        r.Exclusions.Select(e => e.Reason).Should().BeEquivalentTo(
        [
            FtmoGroupSearchExclusionReason.MissingKind, FtmoGroupSearchExclusionReason.SymbolRefused,
            FtmoGroupSearchExclusionReason.ProjectionRefused, FtmoGroupSearchExclusionReason.ProjectionRowless,
        ]);
    }

    [Fact]
    public void ExclusionReason_ZeroValue_IsUnknown_AndNeverAReal()
    {
        default(FtmoGroupSearchExclusionReason).Should().Be(FtmoGroupSearchExclusionReason.Unknown);
        ((int)FtmoGroupSearchExclusionReason.Unknown).Should().Be(0);
        Enum.GetValues<FtmoGroupSearchExclusionReason>().Where(r => r != FtmoGroupSearchExclusionReason.Unknown)
            .Should().OnlyContain(r => (int)r > 0);
    }
}
