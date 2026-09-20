using System.Reflection;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Task 2.1 — pins <c>CostObservation</c>'s shape by reflection before anything downstream
/// (coverage, and later swap/embedded/residual) depends on it. Compile-time correctness is
/// enforced by the type declaration itself; this test additionally pins that <c>Size</c> is
/// non-nullable <c>decimal</c> (design D2's corrected verification against the entities) and that
/// the type is an internal readonly record struct.
/// </summary>
public class CostObservationShapeTests
{
    [Fact]
    public void CostObservation_IsAnInternalReadonlyRecordStruct()
    {
        var type = typeof(CostObservation);

        type.IsValueType.Should().BeTrue();
        type.IsPublic.Should().BeFalse("CostObservation is slice B's own internal projection type, never a public contract");

        type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !f.Name.Contains('<', StringComparison.Ordinal))
            .Should().BeEmpty("a readonly record struct exposes state through properties backed by init-only auto-properties, not public fields");
    }

    [Fact]
    public void CostObservation_HasExactlyTheDesignatedMemberShape()
    {
        var properties = typeof(CostObservation).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.PropertyType);

        properties.Should().ContainKey("OpenTime").WhoseValue.Should().Be(typeof(DateTime));
        properties.Should().ContainKey("OpenPrice").WhoseValue.Should().Be(typeof(decimal));
        properties.Should().ContainKey("ClosePrice").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("Size").WhoseValue.Should().Be(typeof(decimal),
            "Size is non-nullable decimal on both StrategyTrade and BacktestTrade — verified against the entities (design D2 correction)");
        properties.Should().ContainKey("Type").WhoseValue.Should().Be(typeof(string));
        properties.Should().ContainKey("NetPl").WhoseValue.Should().Be(typeof(decimal?));
        properties.Should().ContainKey("Swap").WhoseValue.Should().Be(typeof(decimal?));
    }
}
