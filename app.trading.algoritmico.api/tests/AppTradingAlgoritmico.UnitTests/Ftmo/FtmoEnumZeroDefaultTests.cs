using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// Phase 4 — pins the zero-value choice for every new FTMO enum. This repo has been bitten three
/// times by an optimistic enum zero (<c>PlatformType.MT4</c>, <c>FundingService.Other</c>,
/// <c>CalibrationStatus.Calibrated</c>); a default-initialized value here must never read as a
/// confident breach or a confident clean pass.
/// </summary>
public class FtmoEnumZeroDefaultTests
{
    [Fact]
    public void FtmoBreachVerdict_Default_IsBreachContingent_NeverBreachedOrNoBreachObserved()
    {
        default(FtmoBreachVerdict).Should().Be(FtmoBreachVerdict.BreachContingent);
    }

    [Fact]
    public void FtmoSimulationStatus_Default_IsRefused_NeverEvaluated()
    {
        default(FtmoSimulationStatus).Should().Be(FtmoSimulationStatus.Refused);
    }

    [Fact]
    public void FtmoSimulationRefusal_Default_IsInvalidRequest_TheGenericReason()
    {
        default(FtmoSimulationRefusal).Should().Be(FtmoSimulationRefusal.InvalidRequest);
    }
}
