using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-multi-start PR1, task 1.3 (design.md Decision 3) — the funded phase: a fresh account at
/// Initial Capital on trades with <c>Open &gt;= T2 AND Close &gt; T2</c> (the same PR0-fixed handover
/// rule as <see cref="FtmoChallengeRace.RunChain"/>), same loss limits, no target, no day minimum,
/// evaluated by the UNEDITED <see cref="FtmoBreachEvaluator"/>, first breach via
/// <see cref="FtmoChallengeRace.FirstBreach"/>.
/// </summary>
public class FtmoFundedPhaseTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private const decimal Capital = 10_000m;
    private const decimal DailyPct = 0.05m;
    private const decimal MaxPct = 0.10m;

    // Outside any DST-mismatch window: near-constant 1h offset. Each `day` is a distinct FTMO
    // trading day when a trade opens on it (same convention as FtmoChallengeRaceTests).
    private static DateTime At(int day, int hour = 8, int minute = 0) =>
        new DateTime(2026, 1, 1, hour, minute, 0, DateTimeKind.Unspecified).AddDays(day - 1);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net,
        ResizeOutcome outcome = ResizeOutcome.OnTarget) =>
        new(rowIndex, open, close, net, outcome, FtmoLots: 1m);

    private static FtmoChallengeRace.PhaseResult Phase1(DateTime startSourceOpen) =>
        new(FtmoPhaseOutcome.TargetReachedFirst, startSourceOpen, null, null, null, null, null, null, null);

    private static FtmoChallengeRace.PhaseResult Phase2(FtmoPhaseOutcome outcome, DateTime? outcomeSourceClose) =>
        new(outcome, null, null, null, outcomeSourceClose, null, null, null, null);

    [Fact]
    public void AFundedPhaseThatBreaches_ReportsBreachedFirstFromBothOrigins()
    {
        var phase1Start = At(1);
        var t2 = At(95, 9); // phase 1 began 95 days before the funded start (day 96).
        var phase1 = Phase1(phase1Start);
        var phase2 = Phase2(FtmoPhaseOutcome.TargetReachedFirst, t2);

        var trades = new[]
        {
            // Funded anchor: first trade opened after T2, day 96, flat (no breach yet).
            Trade(0, At(96, 8), At(96, 9), net: 0m),
            // Breaches the max floor (10,000 * (1 - 0.10) = 9,000) 40 days after the funded start.
            Trade(1, At(136, 8), At(136, 9), net: -2_000m),
        };

        var result = FtmoFundedPhase.Run(trades, phase1, phase2, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        result.Outcome.Should().Be(FtmoFundedOutcome.BreachedFirst);
        result.CalendarDaysFromFundedStart.Should().Be(40);
        result.CalendarDaysFromChainStart.Should().Be(135);
    }

    [Fact]
    public void AFundedPhaseWithNoBreachByEndOfData_ReportsNoBreachAtEndOfData()
    {
        var phase1Start = At(1);
        var t2 = At(10, 9);
        var phase1 = Phase1(phase1Start);
        var phase2 = Phase2(FtmoPhaseOutcome.TargetReachedFirst, t2);

        var trades = new[]
        {
            Trade(0, At(11, 8), At(11, 9), net: 100m),
            Trade(1, At(12, 8), At(12, 9), net: 100m),
        };

        var result = FtmoFundedPhase.Run(trades, phase1, phase2, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        result.Outcome.Should().Be(FtmoFundedOutcome.NoBreachByEndOfData);
    }

    [Fact]
    public void AFundedPhaseWithNoTradesLeft_ReportsNoBreachByEndOfDataWithZeroRunway()
    {
        var phase1Start = At(1);
        var t2 = At(10, 9);
        var phase1 = Phase1(phase1Start);
        var phase2 = Phase2(FtmoPhaseOutcome.TargetReachedFirst, t2);

        // No trade opens after T2.
        var trades = new[]
        {
            Trade(0, At(9, 8), At(9, 9), net: 100m),
            Trade(1, At(10, 8), At(10, 9), net: 100m),
        };

        var result = FtmoFundedPhase.Run(trades, phase1, phase2, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        result.Outcome.Should().Be(FtmoFundedOutcome.NoBreachByEndOfData);
        result.CalendarDaysFromFundedStart.Should().Be(0);
        result.CalendarDaysFromChainStart.Should().Be(0);
    }

    [Fact]
    public void APhaseTwoThatDoesNotReachItsTarget_LeavesTheFundedPhaseNotStarted()
    {
        var phase1 = Phase1(At(1));
        var phase2 = Phase2(FtmoPhaseOutcome.NeitherByEndOfData, null);

        var trades = new[]
        {
            Trade(0, At(11, 8), At(11, 9), net: 100m),
        };

        var result = FtmoFundedPhase.Run(trades, phase1, phase2, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        result.Outcome.Should().Be(FtmoFundedOutcome.NotStarted);
    }

    /// <summary>
    /// Falsification-bearing (hard rule 6, extraction/reset equivalence): phase 2's ending balance is
    /// far below Initial Capital. If the funded phase carried that ending balance over instead of
    /// resetting to Initial Capital, a −2,000 close would breach the max floor (balance would already
    /// be below 9,000 before this close); resetting to Initial Capital (10,000), the same close leaves
    /// the balance at 8,000, which DOES still breach 9,000 — so this fixture alone would not
    /// distinguish the two. The day-1 floor pin instead uses the DAILY limit: a reset account's day-1
    /// floor is Initial Capital minus <c>dailyPct</c> (10,000 − 5% = 9,500); a small −400 close stays
    /// above that floor only when the day starts from Initial Capital, not from a much lower carried
    /// balance (which would already sit below its own carried floor).
    /// </summary>
    [Fact]
    public void TheFundedDayOneFloorResetsFromInitialCapital()
    {
        var phase1 = Phase1(At(1));
        var t2 = At(10, 9);
        var phase2 = Phase2(FtmoPhaseOutcome.TargetReachedFirst, t2);

        var trades = new[]
        {
            // Day 11 (funded day 1): a small loss that stays above a floor based on Initial Capital
            // (9,500) but would breach a floor carried over from a much lower phase-2 ending balance.
            Trade(0, At(11, 8), At(11, 9), net: -400m),
        };

        var result = FtmoFundedPhase.Run(trades, phase1, phase2, Jerusalem, Berlin, Capital, DailyPct, MaxPct);

        result.Outcome.Should().Be(FtmoFundedOutcome.NoBreachByEndOfData);
    }
}
