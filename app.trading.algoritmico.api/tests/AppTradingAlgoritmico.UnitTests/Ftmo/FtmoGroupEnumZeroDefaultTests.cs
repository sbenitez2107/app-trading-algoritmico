using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-simulation B2.1 (hard rule 7): the zero-value choice for the new group refusal enum, the
/// exact member list and numbering the frontend mirror must match, the banned-wording sweep, and the
/// <see cref="FtmoGroupSimulationLimits"/> cap and disclosures. A new file: <c>FtmoEnumZeroDefaultTests</c>
/// is not edited.
/// </summary>
public class FtmoGroupEnumZeroDefaultTests
{
    private static readonly string[] Banned = ["passed", "safe", "survived", "would have passed"];

    [Fact]
    public void FtmoGroupRefusal_Default_IsInvalidRequest_TheGenericReason()
    {
        default(FtmoGroupRefusal).Should().Be(FtmoGroupRefusal.InvalidRequest);
    }

    [Fact]
    public void FtmoGroupRefusal_InvalidRequest_IsNumericZero()
    {
        ((int)FtmoGroupRefusal.InvalidRequest).Should().Be(0);
    }

    [Fact]
    public void FtmoGroupRefusal_HasExactlyTheEightSpecMembersWithTheirNumericValues()
    {
        var actual = Enum.GetValues<FtmoGroupRefusal>().ToDictionary(v => v.ToString(), v => (int)v);

        actual.Should().Equal(new Dictionary<string, int>
        {
            ["InvalidRequest"] = 0,
            ["SharedInputsRefused"] = 1,
            ["MemberNotFound"] = 2,
            ["MemberMissingKind"] = 3,
            ["MemberRunRefused"] = 4,
            ["MixedSourceTimeZones"] = 5,
            ["NoCommonWindow"] = 6,
            ["MemberHasNoTradesInWindow"] = 7,
        });
    }

    [Fact]
    public void FtmoGroupRefusal_NamesCarryNoBannedSurvivalWording()
    {
        foreach (var name in Enum.GetNames<FtmoGroupRefusal>())
        {
            foreach (var word in Banned)
                name.ToLowerInvariant().Should().NotContain(word);
        }
    }

    // ---- B2.1.2: FtmoGroupSimulationLimits ----

    [Fact]
    public void MaxMembers_IsBetweenOneAndEight()
    {
        FtmoGroupSimulationLimits.MaxMembers.Should().BeInRange(1, 8);
    }

    [Fact]
    public void Disclosures_AreExactlyThree_CoveringConcurrentDominanceSameCloseOrderingAndUnmodelledEligibility()
    {
        var disclosures = FtmoGroupSimulationLimits.Disclosures;

        disclosures.Should().HaveCount(3);
        disclosures[0].ToLowerInvariant().Should().Contain("concurrent").And.Contain("dominate").And.Contain("clean/contingent");
        disclosures[1].ToLowerInvariant().Should().Contain("identical instant").And.Contain("modelling choice");
        disclosures[2].ToLowerInvariant().Should().Contain("eligibility").And.Contain("not modelled");
    }

    [Fact]
    public void Disclosures_CarryNoBannedSurvivalWording()
    {
        foreach (var text in FtmoGroupSimulationLimits.Disclosures)
        {
            foreach (var word in Banned)
                text.ToLowerInvariant().Should().NotContain(word);
        }
    }
}
