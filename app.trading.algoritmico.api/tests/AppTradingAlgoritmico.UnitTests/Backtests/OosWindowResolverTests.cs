using System.Reflection;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// WF-5, WF-6: the out-of-sample boundary is owned by the walk-forward export, obtainable only
/// through <see cref="OosWindow.Resolver"/>, and simply absent for a Deploy run.
/// </summary>
public class OosWindowResolverTests
{
    private static readonly DateTime Boundary = new(2025, 5, 26);

    private static BacktestRun Run(BacktestRunKind kind) => new()
    {
        Id = Guid.NewGuid(),
        SourceFileName = "ListOfTrades_XAUUSD_H1_IST.csv",
        ContentHash = "hash",
        StrategyId = Guid.NewGuid(),
        Kind = kind,
        Symbol = "XAUUSD_M1_UTC02",
        CreatedAt = DateTime.UtcNow,
    };

    private static StrategyWalkForwardExport Export() => new()
    {
        Id = Guid.NewGuid(),
        StrategyId = Guid.NewGuid(),
        OosFromDate = Boundary,
        DeployParameters = "TEMAPeriod1=32,",
        EvaluationParameters = "TEMAPeriod1=35,",
        ContentHash = "wf-hash",
        SourceFileName = "WFParamsExport_XAUUSD_H1.csv",
        CreatedAt = DateTime.UtcNow,
    };

    private static BacktestTrade TradeClosing(DateTime closeTime) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = Guid.NewGuid(),
        RowIndex = 0,
        Ticket = 1,
        Symbol = "XAUUSD_M1_UTC02",
        Type = "Buy",
        OpenTime = closeTime.AddDays(-1),
        OpenPrice = 1000m,
        Size = 0.1m,
        CloseTime = closeTime,
        ClosePrice = 1010m,
        Profit = 10m,
        Balance = 1010m,
        SampleTypeRaw = "IST",
        Segment = BacktestSegment.InSampleTest,
        CloseType = "PT",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void TryGetOosWindow_DeployRunWithAnExportPresent_YieldsNoWindowAtAll()
    {
        // Not an empty range, not a zero-trade filtered set: NO window. A Deploy run's parameters
        // were fitted through the end of the data, so every one of its trades is in-sample, and a
        // permissive default here is exactly the false claim the type exists to prevent.
        var obtained = OosWindow.Resolver.TryGetOosWindow(Run(BacktestRunKind.Deploy), Export(), out var window);

        obtained.Should().BeFalse();
        window.Should().BeNull();
    }

    [Fact]
    public void TryGetOosWindow_EvaluationRunWithNoExportYet_YieldsNoWindow()
    {
        var obtained = OosWindow.Resolver.TryGetOosWindow(Run(BacktestRunKind.Evaluation), export: null, out var window);

        obtained.Should().BeFalse();
        window.Should().BeNull("the boundary is owned by the export and is unknown until one is imported");
    }

    [Fact]
    public void TryGetOosWindow_EvaluationRunWithAnExport_YieldsTheExportsBoundary()
    {
        var obtained = OosWindow.Resolver.TryGetOosWindow(Run(BacktestRunKind.Evaluation), Export(), out var window);

        obtained.Should().BeTrue();
        window!.FromInclusive.Should().Be(Boundary);
    }

    [Fact]
    public void OosWindow_Includes_IsInclusiveOfTheBoundaryItself()
    {
        OosWindow.Resolver.TryGetOosWindow(Run(BacktestRunKind.Evaluation), Export(), out var window);

        window!.Includes(TradeClosing(Boundary.AddTicks(-1))).Should().BeFalse();
        window.Includes(TradeClosing(Boundary)).Should().BeTrue("the boundary date is the first out-of-sample day");
        window.Includes(TradeClosing(Boundary.AddDays(1))).Should().BeTrue();
    }

    [Fact]
    public void OosWindow_Filter_ReturnsOnlyTradesAtOrAfterTheBoundary()
    {
        OosWindow.Resolver.TryGetOosWindow(Run(BacktestRunKind.Evaluation), Export(), out var window);
        var trades = new[]
        {
            TradeClosing(new DateTime(2024, 1, 1)),
            TradeClosing(new DateTime(2025, 5, 25, 23, 59, 59)),
            TradeClosing(new DateTime(2025, 5, 26)),
            TradeClosing(new DateTime(2026, 1, 1)),
        };

        var oos = window!.Filter(trades).ToList();

        oos.Should().HaveCount(2);
        oos.Select(t => t.CloseTime).Should().Equal(new DateTime(2025, 5, 26), new DateTime(2026, 1, 1));
    }

    // ---- The guarantee itself ----

    [Fact]
    public void OosWindow_HasNoPubliclyReachableConstructor()
    {
        // The claim in the class docs is that a boundary can be OBTAINED but never built. This
        // fences it: if anyone later adds a public or internal constructor "for convenience", the
        // guarantee silently degrades to a naming convention and this test says so.
        var constructors = typeof(OosWindow).GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        constructors.Should().OnlyContain(c => c.IsPrivate);
        typeof(OosWindow).IsSealed.Should().BeTrue("a subclass could otherwise widen construction");
        typeof(OosWindow).IsValueType.Should().BeFalse(
            "a struct would always have a default instance whose boundary is DateTime.MinValue — a window that admits every trade");
    }

    // ---- WF-5: the boundary is never copied onto a run or a trade ----

    [Fact]
    public void OosFromDate_ExistsOnTheExportAndNowhereElse()
    {
        static IEnumerable<string> MemberNames(Type t) =>
            t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Select(m => m.Name);

        MemberNames(typeof(StrategyWalkForwardExport)).Should().Contain(nameof(StrategyWalkForwardExport.OosFromDate));

        MemberNames(typeof(BacktestRun)).Should().NotContain(
            n => n.Contains("OosFrom", StringComparison.OrdinalIgnoreCase),
            "a value owned by the export and copied onto the run cannot observe the export changing");
        MemberNames(typeof(BacktestTrade)).Should().NotContain(
            n => n.Contains("OosFrom", StringComparison.OrdinalIgnoreCase));
    }
}
