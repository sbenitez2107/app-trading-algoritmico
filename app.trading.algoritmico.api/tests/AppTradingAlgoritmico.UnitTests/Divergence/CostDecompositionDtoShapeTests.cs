using System.Reflection;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Phase 4 — pins B1's DTO surface as the PR seam contract (design D9): swap, embedded-cost and
/// residual members must not exist at all, and the root DTO must carry slice A's comparability
/// figures verbatim alongside the coverage component.
/// <para>
/// PR B2 adds <c>Swap</c>/<c>EmbeddedCost</c>/<c>Residual</c> to <see cref="CostDecompositionDto"/>
/// itself, purely additively (design D9) — so the seam guard below now scopes to
/// <see cref="CoverageComponentDto"/>/<see cref="CoverageMonthDto"/> only, which must NEVER carry a
/// money field regardless of B2 (coverage is input-disjoint from swap/embedded/residual — design D4).
/// </para>
/// </summary>
public class CostDecompositionDtoShapeTests
{
    [Fact]
    public void CoverageDtos_NeverDeclareSwapEmbeddedOrResidualMembers()
    {
        var forbiddenNameFragments = new[] { "Swap", "Embedded", "Residual" };

        foreach (var type in new[] { typeof(CoverageComponentDto), typeof(CoverageMonthDto) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var fragment in forbiddenNameFragments)
                {
                    property.Name.Should().NotContain(fragment,
                        $"{type.Name}.{property.Name} — B1's DTO must physically omit swap/embedded-cost/residual members (design D9), never null them");
                }
            }
        }
    }
}
