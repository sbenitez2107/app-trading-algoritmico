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

    /// <summary>
    /// Pins the actual hazard: a default-initialized (zero-valued) <see cref="CostDecompositionStatus"/>
    /// must never read as <see cref="CostDecompositionStatus.Decomposed"/> — the most dangerous
    /// possible default, since it would silently assert a COMPLETE decomposition. This is exactly
    /// why <see cref="CostDecompositionStatus.CoverageComponentOnly"/> is retained even though it is
    /// unreachable from production code post-PR-B2.
    /// </summary>
    [Fact]
    public void CostDecompositionStatus_DefaultValue_IsNeverDecomposed()
    {
        var defaultValue = default(CostDecompositionStatus);

        defaultValue.Should().NotBe(CostDecompositionStatus.Decomposed);
        defaultValue.Should().Be(CostDecompositionStatus.CoverageComponentOnly);
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
