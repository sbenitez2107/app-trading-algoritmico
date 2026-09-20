using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 1.5 — pins the partialness-disclosure enum's zero member and full member set (design D9).</summary>
public class CostDecompositionStatusTests
{
    [Fact]
    public void CostDecompositionStatus_FirstMemberIsCoverageComponentOnly()
    {
        var names = Enum.GetNames<CostDecompositionStatus>();

        names[0].Should().Be(nameof(CostDecompositionStatus.CoverageComponentOnly));
        ((int)CostDecompositionStatus.CoverageComponentOnly).Should().Be(0);
    }

    [Fact]
    public void CostDecompositionStatus_DeclaresAllFourMembers()
    {
        var names = Enum.GetNames<CostDecompositionStatus>();

        names.Should().Contain(nameof(CostDecompositionStatus.CoverageComponentOnly));
        names.Should().Contain(nameof(CostDecompositionStatus.Decomposed));
        names.Should().Contain(nameof(CostDecompositionStatus.NoDemoTrades));
        names.Should().Contain(nameof(CostDecompositionStatus.NoRunForKind));
    }
}
