using System.Reflection;
using AppTradingAlgoritmico.Application.Interfaces;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Structural isolation gate (SBI-4): asserts, via reflection over the interface itself, that
/// <see cref="IBacktestDbContext"/> exposes NO path to <c>StrategyTrades</c> or a tracked
/// <c>Strategies</c> DbSet. This makes the corruption path (importer accidentally writing to
/// live trade storage) a COMPILE-TIME impossibility for anything coded only against the
/// interface, not merely a behavioral convention — see design.md D2.
/// </summary>
public class BacktestDbContextIsolationTests
{
    private static readonly MemberInfo[] Members =
        typeof(IBacktestDbContext).GetMembers(BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void Interface_HasNoStrategyTradesMember()
    {
        Members.Should().NotContain(m => m.Name.Contains("StrategyTrades"));
    }

    [Fact]
    public void Interface_HasNoStrategiesDbSetMember()
    {
        Members.Should().NotContain(m => m.Name == "Strategies");
    }

    [Fact]
    public void ExposedEntities_DeclareNoNavigationToStrategy()
    {
        // A DbSet is not the only compile-time path. BacktestRun.StrategyId is a bare foreign key
        // on purpose: a `Strategy` navigation property would let anything holding a run reach — and
        // mutate — the strategy through the importer's own surface, which is exactly what the
        // narrow interface exists to prevent.
        Type[] exposed =
        [
            typeof(Domain.Entities.BacktestRun),
            typeof(Domain.Entities.BacktestTrade),
            typeof(Domain.Entities.StrategyWalkForwardExport),
            typeof(Domain.Entities.WalkForwardWindow),
            typeof(Domain.Entities.SymbolCalibration),
        ];

        var offenders = exposed
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pi => pi.PropertyType == typeof(Domain.Entities.Strategy)
                    || (pi.PropertyType.IsGenericType
                        && pi.PropertyType.GetGenericArguments().Contains(typeof(Domain.Entities.Strategy))))
                .Select(pi => $"{t.Name}.{pi.Name}"))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Interface_ExposesExactlyTheDocumentedSurface()
    {
        var propertyAndMethodNames = typeof(IBacktestDbContext)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Method)
            .Select(m => m.Name)
            .Where(n => !n.StartsWith("get_", StringComparison.Ordinal)) // property getters show up as get_X methods too
            .Distinct()
            .ToList();

        propertyAndMethodNames.Should().BeEquivalentTo(
        [
            "BacktestRuns",
            "BacktestTrades",
            "SymbolCalibrations",
            "StrategyWalkForwardExports",
            "WalkForwardWindows",
            "SaveChangesAsync",
            "Database",
        ]);
    }
}
