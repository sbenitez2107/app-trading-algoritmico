using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 1.1 — pins the enum shape before any calculator reads it.</summary>
public class CoverageBasisTests
{
    [Fact]
    public void CoverageBasis_HasExactlyOneMember_PresumedFromBacktestTradeAbsence()
    {
        var values = Enum.GetValues<CoverageBasis>();

        values.Should().ContainSingle();
        values.Should().Contain(CoverageBasis.PresumedFromBacktestTradeAbsence);
        ((int)CoverageBasis.PresumedFromBacktestTradeAbsence).Should().Be(0);
    }
}
