using FluentAssertions;
using AppTradingAlgoritmico.Infrastructure.Services;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.4.1 (design.md D7) — shape tests for the deterministic group benchmark fixture,
/// written BEFORE the benchmark runs against it: k members of the shipped 1,000-trade series, each shifted by
/// <c>m x spacing / k</c> hours, every member numbered <c>RowIndex 0..999</c> so the merger's duplicate-index
/// path is exercised, with real cross-member overlap and no <see cref="Random"/>.
/// </summary>
public class FtmoGroupBenchmarkFixtureTests
{
    private const FtmoMultiStartBenchmarkFixture.Profile Never = FtmoMultiStartBenchmarkFixture.Profile.Never;

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void Build_ProducesKMembersOfOneThousandTradesEach(int k)
    {
        var members = FtmoGroupBenchmarkFixture.Build(Never, k);

        members.Should().HaveCount(k);
        members.Select(m => m.MemberOrder).Should().Equal(Enumerable.Range(0, k));
        members.Should().OnlyContain(m => m.Low.Count == 1_000 && m.High.Count == 1_000);
    }

    [Fact]
    public void Build_EveryMemberNumbersItsRowsZeroToNineHundredNinetyNine_SoMembersCollideOnRowIndex()
    {
        var members = FtmoGroupBenchmarkFixture.Build(Never, 4);

        foreach (var member in members)
            member.Low.Select(t => t.RowIndex).Should().Equal(Enumerable.Range(0, 1_000));
    }

    [Fact]
    public void Build_MemberMIsTheBaseSeriesShiftedByMTimesSpacingOverKHours()
    {
        const int k = 4;
        var baseline = FtmoMultiStartBenchmarkFixture.Build(Never);
        var members = FtmoGroupBenchmarkFixture.Build(Never, k);

        for (var m = 0; m < k; m++)
        {
            var shift = TimeSpan.FromHours(m * FtmoGroupBenchmarkFixture.SpacingHours / k);
            members[m].Low.Select(t => t.OpenSource - shift).Should().Equal(baseline.Select(t => t.OpenSource));
            members[m].Low.Select(t => t.CloseSource - shift).Should().Equal(baseline.Select(t => t.CloseSource));
        }
    }

    [Fact]
    public void Build_LowAndHighEndsAreIdentical_ADegenerateUsdBand()
    {
        var members = FtmoGroupBenchmarkFixture.Build(Never, 3);

        foreach (var member in members)
            member.High.Should().Equal(member.Low);
    }

    [Fact]
    public void Build_HasCrossMemberOverlap_AMemberTradeIsOpenWhileAnotherMembersTradeIsOpen()
    {
        var members = FtmoGroupBenchmarkFixture.Build(Never, 8);
        var first = members[0].Low;
        var second = members[1].Low;

        var overlapping = first.Count(a => second.Any(b => b.OpenSource < a.CloseSource && a.OpenSource < b.CloseSource));

        overlapping.Should().BeGreaterThan(100, "the benchmark must exercise cross-member concurrency, not disjoint series");
    }

    [Fact]
    public void Build_IsDeterministic_NoRandomness()
    {
        var first = FtmoGroupBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Fast, 5);
        var second = FtmoGroupBenchmarkFixture.Build(FtmoMultiStartBenchmarkFixture.Profile.Fast, 5);

        for (var m = 0; m < 5; m++)
            second[m].Low.Should().Equal(first[m].Low);
    }

    [Fact]
    public void Build_AMemberCountBelowOne_Throws()
    {
        var act = () => FtmoGroupBenchmarkFixture.Build(Never, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
