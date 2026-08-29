using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// <see cref="PortfolioService.GetRiskAsync"/> branch-by-<see cref="GuardrailKind"/> behaviour
/// (`funding-guardrails` + `portfolio-monthly-var` specs). Golden regression for the pre-existing
/// LossLimits path plus the new VarTarget path (no breach/headroom, insufficient-history gating).
/// </summary>
public class PortfolioServiceRiskTests
{
    private static TradingAccount MakeAccount(Guid id, string broker) => new()
    {
        Id = id,
        Name = broker + " account",
        Broker = broker,
        AccountType = AccountType.Live,
        Platform = PlatformType.MT4,
        AccountNumber = 1,
        Login = 1,
        Server = "srv",
    };

    private static Strategy MakeStrategy(Guid id, string name, Guid accountId) =>
        new() { Id = id, Name = name, TradingAccountId = accountId };

    private static StrategyTrade Trade(Guid strategyId, long ticket, DateTime open, DateTime close, decimal profit) => new()
    {
        Id = Guid.NewGuid(),
        StrategyId = strategyId,
        Ticket = ticket,
        OpenTime = open,
        CloseTime = close,
        Type = "buy",
        Size = 0.1m,
        Item = "EURUSD",
        OpenPrice = 1.1m,
        ClosePrice = 1.1m,
        StopLoss = 0m,
        TakeProfit = 0m,
        Commission = 0m,
        Taxes = 0m,
        Swap = 0m,
        Profit = profit,
        IsOpen = false,
    };

    /// <summary>One closed trade per calendar day starting at <paramref name="start"/>.</summary>
    private static List<StrategyTrade> DailyTrades(Guid strategyId, DateTime start, int days, decimal netPerDay)
    {
        var trades = new List<StrategyTrade>();
        for (var i = 0; i < days; i++)
        {
            var open = start.AddDays(i);
            trades.Add(Trade(strategyId, i + 1, open, open.AddHours(1), netPerDay));
        }
        return trades;
    }

    private static Portfolio MakePortfolio(Guid id, decimal initialCapital, params Guid[] strategyIds)
    {
        var p = new Portfolio
        {
            Id = id,
            Name = "P",
            Broker = "Multi",
            AccountType = AccountType.Live,
            InitialCapital = initialCapital,
            BaseCurrency = "USD",
            CreatedAt = DateTime.UtcNow,
        };
        foreach (var sid in strategyIds)
            p.Members.Add(new PortfolioStrategy { StrategyId = sid, Weight = 1m });
        return p;
    }

    [Fact]
    public async Task GetRiskAsync_LossLimitsGuardrail_GoldenRegression_HeadroomAndBreachUnchanged()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1);

        await using var db = InMemoryDbContextFactory.Create();
        db.TradingAccounts.Add(MakeAccount(accountId, "FTMO"));
        db.Strategies.Add(MakeStrategy(strategyId, "A", accountId));
        db.StrategyTrades.AddRange(DailyTrades(strategyId, d, 10, -1000m));
        db.Portfolios.Add(MakePortfolio(portfolioId, 100_000m, strategyId));
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "FTMO",
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            ProfitTargetPct = 0.10m,
            DrawdownModel = DrawdownModel.Static,
            Verified = true,
        });
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);
        var risk = await sut.GetRiskAsync(portfolioId);

        var guard = risk.Guardrails.Single();
        guard.Kind.Should().Be(GuardrailKind.LossLimits);
        guard.Configured.Should().BeTrue();
        guard.DailyLossLimitPct.Should().Be(0.05m);
        guard.MaxLossLimitPct.Should().Be(0.10m);
        guard.ProfitTargetPct.Should().Be(0.10m);
        guard.DrawdownModel.Should().Be(DrawdownModel.Static);
        var expectedHeadroom = 0.05m - guard.ServiceVar95Percent;
        guard.DailyHeadroomPct.Should().Be(expectedHeadroom, "unchanged formula: dailyLimit - Var95Percent");
        guard.DailyBreached.Should().Be(guard.ServiceVar95Percent > 0.05m);
        guard.VarTarget.Should().BeNull("VarTarget block is null for a LossLimits guardrail");
    }

    [Fact]
    public async Task GetRiskAsync_VarTargetGuardrail_NeverEmitsBreachOrHeadroom_SufficientHistory()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1);

        await using var db = InMemoryDbContextFactory.Create();
        db.TradingAccounts.Add(MakeAccount(accountId, "Darwinex"));
        db.Strategies.Add(MakeStrategy(strategyId, "A", accountId));
        // 100 calendar days, constant -10/day → sufficient history (>= 90).
        db.StrategyTrades.AddRange(DailyTrades(strategyId, d, 100, -10m));
        db.Portfolios.Add(MakePortfolio(portfolioId, 100_000m, strategyId));
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "Darwinex",
            FundingService = FundingService.DarwinexZero,
            Kind = GuardrailKind.VarTarget,
            TargetVarPct = 0.065m,
            VarFloorPct = 0.0325m,
            Verified = true,
        });
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);
        var risk = await sut.GetRiskAsync(portfolioId);

        var guard = risk.Guardrails.Single();
        guard.Kind.Should().Be(GuardrailKind.VarTarget);
        guard.DailyHeadroomPct.Should().BeNull("no headroom semantics for VarTarget");
        guard.DailyBreached.Should().BeFalse("no breach semantics for VarTarget");
        guard.MaxLossLimitPct.Should().BeNull();
        guard.ProfitTargetPct.Should().BeNull();
        guard.DrawdownModel.Should().BeNull();

        guard.VarTarget.Should().NotBeNull();
        var vt = guard.VarTarget!;
        vt.TargetVarPct.Should().Be(0.065m);
        vt.VarFloorPct.Should().Be(0.0325m);
        vt.HorizonDays.Should().Be(30);
        vt.InsufficientHistory.Should().BeFalse();
        vt.MonthlyVar95.Should().NotBeNull();
        vt.ImpliedMultiplier.Should().Be(0.065m / vt.MonthlyVar95Percent!.Value);
    }

    [Fact]
    public async Task GetRiskAsync_VarTargetGuardrail_BelowMinHistory_InsufficientHistoryNoMultiplier()
    {
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var d = new DateTime(2026, 1, 1);

        await using var db = InMemoryDbContextFactory.Create();
        db.TradingAccounts.Add(MakeAccount(accountId, "Darwinex"));
        db.Strategies.Add(MakeStrategy(strategyId, "A", accountId));
        // Only 30 calendar days — below the 90-day minimum.
        db.StrategyTrades.AddRange(DailyTrades(strategyId, d, 30, -10m));
        db.Portfolios.Add(MakePortfolio(portfolioId, 100_000m, strategyId));
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = "Darwinex",
            FundingService = FundingService.DarwinexZero,
            Kind = GuardrailKind.VarTarget,
            TargetVarPct = 0.065m,
            VarFloorPct = 0.0325m,
            Verified = true,
        });
        await db.SaveChangesAsync();

        var sut = new PortfolioService(db);
        var risk = await sut.GetRiskAsync(portfolioId);

        var guard = risk.Guardrails.Single();
        guard.VarTarget.Should().NotBeNull();
        var vt = guard.VarTarget!;
        vt.InsufficientHistory.Should().BeTrue();
        vt.MonthlyVar95.Should().BeNull();
        vt.MonthlyVar95Percent.Should().BeNull();
        vt.ImpliedMultiplier.Should().BeNull("no multiplier without a monthly VaR estimate");
    }
}
