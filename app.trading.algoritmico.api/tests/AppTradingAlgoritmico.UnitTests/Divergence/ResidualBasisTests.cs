using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 10.3 — pins <see cref="ResidualBasis"/> as a single-member enum stating the claim boundary (design D6, D10).</summary>
public class ResidualBasisTests
{
    [Fact]
    public void HasExactlyOneMember()
    {
        var values = Enum.GetValues<ResidualBasis>();

        values.Should().HaveCount(1);
        ((int)ResidualBasis.PairedSubsetAfterSwapAndEmbeddedCost).Should().Be(0);
    }
}
