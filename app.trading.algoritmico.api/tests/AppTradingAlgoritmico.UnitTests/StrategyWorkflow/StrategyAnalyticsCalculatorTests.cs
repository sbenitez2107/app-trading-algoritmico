using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Tests for the pure analytics calculator. No DB / no DI — pure inputs in, DTO out.
/// </summary>
public class StrategyAnalyticsCalculatorTests
{
    private static StrategyTrade Trade(
        DateTime open,
        DateTime? close,
        decimal profit,
        decimal commission = 0m,
        decimal swap = 0m,
        decimal taxes = 0m,
        bool isOpen = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            StrategyId = Guid.Empty,
            Ticket = Random.Shared.NextInt64(1_000_000, long.MaxValue),
            OpenTime = open,
            CloseTime = close,
            Type = "buy",
            Size = 0.1m,
            Item = "ndx",
            OpenPrice = 100m,
            ClosePrice = isOpen ? null : 101m,
            StopLoss = 0m,
            TakeProfit = 0m,
            Commission = commission,
            Taxes = taxes,
            Swap = swap,
            Profit = profit,
            IsOpen = isOpen,
        };

    [Fact]
    public void Compute_NoTrades_ReturnsAllZeros()
    {
        var dto = StrategyAnalyticsCalculator.Compute(100_000m, []);

        dto.InitialBalance.Should().Be(100_000m);
        dto.TradeCount.Should().Be(0);
        dto.TotalReturn.Should().Be(0m);
        dto.MaxDrawdownAmount.Should().Be(0m);
        dto.SharpeRatio.Should().Be(0m);
        dto.Sqn.Should().Be(0m);
    }

    [Fact]
    public void Compute_SimpleSeries_TotalReturnsAndCountsCorrect()
    {
        // 4 closed trades — 3 wins (50, 30, 100), 1 loss (-40). Costs are zero.
        var d = new DateTime(2026, 1, 1);
        var trades = new[]
        {
            Trade(d, d.AddHours(1), profit: 50m),
            Trade(d.AddDays(1), d.AddDays(1).AddHours(1), profit: 30m),
            Trade(d.AddDays(2), d.AddDays(2).AddHours(1), profit: -40m),
            Trade(d.AddDays(3), d.AddDays(3).AddHours(1), profit: 100m),
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.TradeCount.Should().Be(4);
        dto.ClosedCount.Should().Be(4);
        dto.WinCount.Should().Be(3);
        dto.LossCount.Should().Be(1);
        dto.NetProfit.Should().Be(140m);
        dto.GrossProfit.Should().Be(180m);
        dto.GrossLoss.Should().Be(-40m);
        dto.AverageTrade.Should().Be(35m);
        dto.AverageWin.Should().Be(60m);
        dto.AverageLoss.Should().Be(-40m);
        dto.LargestWin.Should().Be(100m);
        dto.LargestLoss.Should().Be(-40m);
        dto.WinRate.Should().BeApproximately(0.75m, 0.0001m);
        dto.ProfitFactor.Should().BeApproximately(4.5m, 0.0001m, "180 / 40");
        dto.PayoutRatio.Should().BeApproximately(1.5m, 0.0001m, "60 / 40");
        dto.TotalReturn.Should().BeApproximately(0.0014m, 0.0001m, "140 / 100k");
    }

    [Fact]
    public void Compute_DrawdownAfterPeak_TracksMaxDdCorrectly()
    {
        // Sequence: +500 (peak 100500) → -300 (eq 100200, dd=300) → -200 (eq 100000, dd=500 ← max) → +100
        var d = new DateTime(2026, 1, 1);
        var trades = new[]
        {
            Trade(d, d.AddHours(1), profit: 500m),
            Trade(d.AddDays(1), d.AddDays(1).AddHours(1), profit: -300m),
            Trade(d.AddDays(2), d.AddDays(2).AddHours(1), profit: -200m),
            Trade(d.AddDays(3), d.AddDays(3).AddHours(1), profit: 100m),
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.MaxDrawdownAmount.Should().Be(500m, "peak was 100,500; valley reached 100,000");
        dto.MaxDrawdownPercent.Should()
            .BeApproximately(500m / 100_500m, 0.0001m, "ratio against the peak, not the initial balance");
    }

    [Fact]
    public void Compute_ConsecutiveStreaks_AreDetectedAcrossTradeOrder()
    {
        // Win, Win, Loss, Loss, Loss, Win — max wins=2, max losses=3
        var d = new DateTime(2026, 1, 1);
        var trades = new[]
        {
            Trade(d, d.AddHours(1), profit: 10m),
            Trade(d.AddDays(1), d.AddDays(1).AddHours(1), profit: 10m),
            Trade(d.AddDays(2), d.AddDays(2).AddHours(1), profit: -5m),
            Trade(d.AddDays(3), d.AddDays(3).AddHours(1), profit: -5m),
            Trade(d.AddDays(4), d.AddDays(4).AddHours(1), profit: -5m),
            Trade(d.AddDays(5), d.AddDays(5).AddHours(1), profit: 10m),
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.MaxConsecutiveWins.Should().Be(2);
        dto.MaxConsecutiveLosses.Should().Be(3);
        dto.AverageConsecutiveWins.Should().BeApproximately(1.5m, 0.0001m, "runs of 2 and 1");
        dto.AverageConsecutiveLosses.Should().Be(3m, "single run of 3");
    }

    [Fact]
    public void Compute_OpenAndClosedTrades_OpenExcludedFromClosedAggregates()
    {
        var d = new DateTime(2026, 1, 1);
        var trades = new[]
        {
            Trade(d, d.AddHours(1), profit: 100m),
            Trade(d.AddDays(1), close: null, profit: 50m, isOpen: true), // unrealised
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.TradeCount.Should().Be(2);
        dto.ClosedCount.Should().Be(1);
        dto.OpenCount.Should().Be(1);
        // GrossProfit / WinCount / etc only count CLOSED trades.
        dto.WinCount.Should().Be(1);
        dto.GrossProfit.Should().Be(100m);
        // But TotalProfit aggregates ALL trades — open ones contribute their unrealized P/L.
        dto.TotalProfit.Should().Be(150m);
    }

    [Fact]
    public void ComputeMonthlyReturns_CompoundsBetweenMonths()
    {
        // Jan: +5,000 on $100k → 5%, equity ends at 105k
        // Feb: +5,250 on $105k → 5%, equity ends at 110,250
        var trades = new[]
        {
            Trade(new DateTime(2026, 1, 15), new DateTime(2026, 1, 15, 12, 0, 0), profit: 5_000m),
            Trade(new DateTime(2026, 2, 10), new DateTime(2026, 2, 10, 12, 0, 0), profit: 5_250m),
        };

        var months = StrategyAnalyticsCalculator.ComputeMonthlyReturns(100_000m, trades);

        months.Should().HaveCount(2);

        var jan = months[0];
        jan.Year.Should().Be(2026);
        jan.Month.Should().Be(1);
        jan.EquityStart.Should().Be(100_000m);
        jan.EquityEnd.Should().Be(105_000m);
        jan.ReturnPercent.Should().BeApproximately(0.05m, 0.0001m);

        var feb = months[1];
        feb.EquityStart.Should().Be(105_000m, "rolls forward from January's end");
        feb.EquityEnd.Should().Be(110_250m);
        feb.ReturnPercent.Should().BeApproximately(0.05m, 0.0001m, "5,250 / 105,000 = exactly 5% — compounding works");
    }

    [Fact]
    public void ComputeMonthlyReturns_GroupsByCloseTimeNotOpenTime()
    {
        // Open in Jan, close in Feb → bucket = Feb.
        var trades = new[]
        {
            Trade(new DateTime(2026, 1, 28), new DateTime(2026, 2, 3, 10, 0, 0), profit: 200m),
        };

        var months = StrategyAnalyticsCalculator.ComputeMonthlyReturns(100_000m, trades);

        months.Should().ContainSingle();
        months[0].Month.Should().Be(2);
    }

    [Fact]
    public void Compute_SharpeRatio_IsNonZeroForVolatileSeries()
    {
        // Several days with mixed returns — Sharpe should be a finite number, sign reflects mean direction.
        var d = new DateTime(2026, 1, 1);
        var trades = new[]
        {
            Trade(d.AddDays(0), d.AddDays(0).AddHours(1), profit: 100m),
            Trade(d.AddDays(1), d.AddDays(1).AddHours(1), profit: 200m),
            Trade(d.AddDays(2), d.AddDays(2).AddHours(1), profit: -150m),
            Trade(d.AddDays(3), d.AddDays(3).AddHours(1), profit: 300m),
            Trade(d.AddDays(4), d.AddDays(4).AddHours(1), profit: -100m),
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.SharpeRatio.Should().NotBe(0m, "5 days of mixed returns produce a finite Sharpe");
    }

    [Fact]
    public void Compute_Exposure_MergesOverlappingTrades()
    {
        // Two fully-overlapping trades over a 10-hour window — exposure must be 100%.
        var d = new DateTime(2026, 1, 1, 10, 0, 0);
        var trades = new[]
        {
            Trade(d, d.AddHours(10), profit: 0m),
            Trade(d.AddHours(2), d.AddHours(8), profit: 0m), // contained inside trade 1
        };

        var dto = StrategyAnalyticsCalculator.Compute(100_000m, trades);

        dto.Exposure.Should().BeApproximately(1m, 0.001m, "overlapping intervals merge — both windows count once");
    }
}
