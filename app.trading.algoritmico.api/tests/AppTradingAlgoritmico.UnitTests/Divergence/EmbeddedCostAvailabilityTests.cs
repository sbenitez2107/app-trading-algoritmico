using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 10.1 — pins the exact member order of <see cref="EmbeddedCostAvailability"/> (design D4, D8).</summary>
public class EmbeddedCostAvailabilityTests
{
    [Fact]
    public void HasExactlyFourMembersInThisOrder()
    {
        var values = Enum.GetValues<EmbeddedCostAvailability>();

        values.Should().HaveCount(4);
        ((int)EmbeddedCostAvailability.NoCalibrationRow).Should().Be(0,
            "the zero member must assert nothing usable, not Calibrated (design D8)");
        ((int)EmbeddedCostAvailability.InsufficientSamples).Should().Be(1);
        ((int)EmbeddedCostAvailability.Inconsistent).Should().Be(2);
        ((int)EmbeddedCostAvailability.Calibrated).Should().Be(3);
    }
}
