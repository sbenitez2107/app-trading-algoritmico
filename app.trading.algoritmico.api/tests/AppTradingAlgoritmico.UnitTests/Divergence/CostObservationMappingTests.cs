using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Task 2.3 — the mapping to slice A's <c>OpenObservation</c> preserves OpenTime/OpenPrice/Type/NetPl
/// verbatim and drops ClosePrice/Size/Swap only for this call, never mutating the source.
/// </summary>
public class CostObservationMappingTests
{
    [Fact]
    public void ToOpenObservation_PreservesFourFieldsVerbatim_AndNeverMutatesTheSource()
    {
        var openTime = new DateTime(2026, 4, 21, 10, 15, 0, DateTimeKind.Unspecified);
        var source = new CostObservation(openTime, 15022.06m, 15030m, 0.1m, "buy", 42.5m, 3.1m);

        var mapped = source.ToOpenObservation();

        mapped.OpenTime.Should().Be(openTime);
        mapped.OpenTime.Kind.Should().Be(DateTimeKind.Unspecified);
        mapped.OpenPrice.Should().Be(15022.06m);
        mapped.Type.Should().Be("buy");
        mapped.NetPl.Should().Be(42.5m);

        source.ClosePrice.Should().Be(15030m, "the source must never be mutated by mapping");
        source.Size.Should().Be(0.1m);
        source.Swap.Should().Be(3.1m);
    }
}
