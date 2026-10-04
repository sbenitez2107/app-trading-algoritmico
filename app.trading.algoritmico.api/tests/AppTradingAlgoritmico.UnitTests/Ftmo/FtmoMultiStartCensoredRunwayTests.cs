using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// Regression for the censored-runway crash in <see cref="FtmoMultiStartReadService"/>: a censored
/// start whose phase has NO trades left (the previous phase reached its target on the last replayed
/// close) must report a runway of 0 — the same rule <see cref="FtmoFundedPhase"/> already applies to an
/// empty funded phase (spec.md "A funded phase with no trades left reports NoBreachByEndOfData with zero
/// runway") — and that 0 is an observed start in the censored-runway order statistics, never a null that
/// throws in <c>Summarize</c>.
/// </summary>
public class FtmoMultiStartCensoredRunwayTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Weekdays in January 2026 (one FTMO month -> exactly one start), one trade per day.
    private static readonly DateTime[] TradeDays =
    [
        new(2026, 1, 5, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 6, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 7, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 8, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 12, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 13, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 14, 10, 0, 0, DateTimeKind.Unspecified),
        new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified),
    ];

    private static List<FtmoTradeProjector.ProjectedTrade> Trades(params decimal[] nets) =>
        [.. nets.Select((net, i) => new FtmoTradeProjector.ProjectedTrade(
            i, TradeDays[i], TradeDays[i].AddHours(1), net, ResizeOutcome.OnTarget, FtmoLots: 0.10m))];

    private static FtmoMultiStartRunDto Compute(IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades) =>
        FtmoMultiStartReadService.ComputeRun(
            Guid.NewGuid(), BacktestRunKind.Deploy, BacktestSegment.InSample, trades, trades,
            Jerusalem, Berlin, initialCapital: 10_000m, dailyPct: 0.05m, maxPct: 0.10m, profitTargetPct: null,
            fxBand: (1m, 1m), unscalableCount: 0,
            new FtmoChallengeRulesDto(0.10m, 0.05m, 4, TimeLimitDays: null), CancellationToken.None);

    // Case (a): phase 1 (+10%, 4 trading days) is reached on the 4th and last trade -> phase 2 has
    // nothing left to replay.
    [Fact]
    public void Phase1TargetOnTheLastTrade_Phase2UndecidedWithNoTradesLeft_ReportsZeroRunway()
    {
        var run = Compute(Trades(300m, 300m, 300m, 300m));

        var row = run.Starts.Should().ContainSingle().Subject;
        row.Outcome.Should().Be(FtmoChainOutcome.Phase2UndecidedAtEndOfData);
        row.IsCensored.Should().BeTrue();
        row.RunwayCalendarDays.Should().Be(0);
        row.Phase2.CalendarDaysElapsed.Should().BeNull("the phase DTO mirrors the race, which never started phase 2");

        run.Summary!.CensoredRunway.Should().Be(new FtmoOrderStatisticsDto(1, 0, 0, 0, 0, 0));
    }

    // Case (b): phase 2 (+5%, 4 trading days) is reached on the 8th and last trade -> the funded phase
    // has nothing left to replay (FtmoFundedPhase already reports runway 0 here).
    [Fact]
    public void Phase2TargetOnTheLastTrade_FundedNoBreachWithNoTradesLeft_ReportsZeroRunway()
    {
        var run = Compute(Trades(300m, 300m, 300m, 300m, 150m, 150m, 150m, 150m));

        var row = run.Starts.Should().ContainSingle().Subject;
        row.Outcome.Should().Be(FtmoChainOutcome.FundedNoBreachAtEndOfData);
        row.IsCensored.Should().BeTrue();
        row.RunwayCalendarDays.Should().Be(0);

        run.Summary!.CensoredRunway.Should().Be(new FtmoOrderStatisticsDto(1, 0, 0, 0, 0, 0));
    }

    // A phase 2 that DID start and ran out of data keeps its real elapsed days (not coerced to 0).
    [Fact]
    public void Phase2UndecidedWithTradesLeft_ReportsItsElapsedDaysAsRunway()
    {
        var run = Compute(Trades(300m, 300m, 300m, 300m, 50m, 50m));

        var row = run.Starts.Should().ContainSingle().Subject;
        row.Outcome.Should().Be(FtmoChainOutcome.Phase2UndecidedAtEndOfData);
        row.Phase2.CalendarDaysElapsed.Should().NotBeNull();
        row.RunwayCalendarDays.Should().Be(row.Phase2.CalendarDaysElapsed);
        row.RunwayCalendarDays.Should().BeGreaterThan(0);
    }
}
