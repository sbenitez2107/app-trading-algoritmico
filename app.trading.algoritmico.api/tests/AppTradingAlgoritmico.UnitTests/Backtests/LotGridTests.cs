using AppTradingAlgoritmico.Domain.Backtests;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Task 1.4 — the grid is a validated value object, not four loose constants (design.md D8).
/// The 1-decimal grid must be CONSTRUCTIBLE so the coarse-grid cases are testable, without the
/// system shipping support for it.
/// </summary>
public class LotGridTests
{
    [Fact]
    public void ImoxRetester_Preset_IsTheTwoDecimalGrid()
    {
        var grid = LotGrid.ImoxRetester;

        grid.SizeDecimals.Should().Be(2);
        grid.Step.Should().Be(0.01m);
        grid.MinLot.Should().Be(0.01m, "MinLot IS the step — 'Size if no MM' 0.1 is the money-management fallback, not a floor");
        grid.MaxLots.Should().Be(10m);
    }

    [Fact]
    public void Ctor_OneDecimalGrid_IsConstructible()
    {
        var grid = new LotGrid(sizeDecimals: 1, step: 0.10m, minLot: 0.10m, maxLots: 10m);

        grid.Step.Should().Be(0.10m);
        grid.MinLot.Should().Be(0.10m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Ctor_NonPositiveStep_Throws(double step)
    {
        Action act = () => new LotGrid(2, (decimal)step, 0.01m, 10m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("step");
    }

    [Fact]
    public void Ctor_MinLotBelowStep_Throws()
    {
        Action act = () => new LotGrid(2, step: 0.01m, minLot: 0.005m, maxLots: 10m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("minLot");
    }

    [Fact]
    public void Ctor_MaxLotsBelowMinLot_Throws()
    {
        Action act = () => new LotGrid(2, step: 0.01m, minLot: 0.05m, maxLots: 0.04m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxLots");
    }

    [Theory]
    [InlineData(1, 0.01)]   // step needs 2 decimals, grid claims 1
    [InlineData(3, 0.01)]   // step needs 2 decimals, grid claims 3
    [InlineData(2, 0.10)]   // step needs 1 decimal, grid claims 2
    public void Ctor_SizeDecimalsMismatchingStep_Throws(int sizeDecimals, double step)
    {
        Action act = () => new LotGrid(sizeDecimals, (decimal)step, (decimal)step, 10m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("sizeDecimals");
    }

    [Fact]
    public void Ctor_StepNeedingItsOwnDecimalCount_IsAccepted()
    {
        // 0.05 genuinely needs 2 decimals, so (2, 0.05) is a coherent grid.
        Action act = () => new LotGrid(2, step: 0.05m, minLot: 0.05m, maxLots: 10m);

        act.Should().NotThrow();
    }
}
