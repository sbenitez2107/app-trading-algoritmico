using AppTradingAlgoritmico.Application.Interfaces;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>Task 5.1 — the interface's kind parameter is required, never optional or defaulted (design "BacktestRunKind required").</summary>
public class ICostDecompositionReadServiceTests
{
    [Fact]
    public void GetAsync_KindParameter_IsNonOptional()
    {
        var method = typeof(ICostDecompositionReadService).GetMethod(nameof(ICostDecompositionReadService.GetAsync));
        var kindParameter = method!.GetParameters().Single(p => p.Name == "kind");

        kindParameter.IsOptional.Should().BeFalse();
        kindParameter.HasDefaultValue.Should().BeFalse();
    }
}
