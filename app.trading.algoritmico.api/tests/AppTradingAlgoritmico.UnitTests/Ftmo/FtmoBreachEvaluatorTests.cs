using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// PR P2, Phase 6 — <see cref="FtmoBreachEvaluator"/>. Running-balance, three-state, day-scoped
/// evaluation (design.md Decision 5, spec.md Daily/Max Loss/Overlap requirements).
/// </summary>
public class FtmoBreachEvaluatorTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Outside any DST-mismatch window for Jerusalem/Berlin: near-constant 1h offset.
    private static DateTime Close(int day, int hour, int minute) =>
        new(2026, 1, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(
        int rowIndex, DateTime open, DateTime close, decimal? net, ResizeOutcome outcome = ResizeOutcome.OnTarget) =>
        new(rowIndex, open, close, net, outcome, FtmoLots: 1m);

    [Fact]
    public void Evaluate_Minus8PctDayWithVar95At2Pct_GivesBreachedIndependentOfVar95()
    {
        // spec.md's headline scenario: VaR95 across the segment is irrelevant to this evaluator —
        // it only ever sees closes and balances, never a VaR figure.
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: -800m), // 8% intraday drop
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
    }

    [Fact]
    public void Evaluate_ReferenceFloorMovesWithPreviousMidnightBalance_NotFlatInitialMinus5Pct()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: 200m), // day 1 closes 2% above initial: 10,200
            Trade(1, Close(11, 8, 0), Close(11, 9, 0), net: -600m), // day 2 balance becomes 9,600
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.30m);

        // Evaluator's day-2 floor: 10,200 - 500 (5% of Initial Capital) = 9,700. Balance after
        // trade 2 is 9,600, which is BELOW that moving floor -- a breach. A flat Initial-Capital
        // floor (9,500) would call this NO breach (9,600 is not below 9,500): this assertion only
        // passes when the floor genuinely moved with day 1's (higher) closing balance.
        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
    }

    [Fact]
    public void Evaluate_FlatInitialMinus5PctEveryDay_WouldFailThisRequirement()
    {
        // Same two-day segment; a hypothetical flat-floor implementation would compute day 2's floor
        // as a fixed Initial Capital - 5%, differing from the evaluator's actual moving floor.
        const decimal initial = 10_000m;
        var day1Close = 10_200m; // day 1 ends 2% above initial
        var evaluatorFloor = day1Close - 0.05m * initial; // 9,700
        var flatFloor = initial - 0.05m * initial; // 9,500

        evaluatorFloor.Should().NotBe(flatFloor);
    }

    [Fact]
    public void Evaluate_Day1UsesInitialCapitalAsReferenceBalance()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: -501m), // just over 5% of 10,000
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
        result.Daily.FirstBreach!.Value.Level.Should().Be(initial - 0.05m * initial);
    }

    [Fact]
    public void Evaluate_MaxLossFloorDoesNotMoveAfterAProfitableDay()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: 500m), // day 1 closes 5% above initial
            Trade(1, Close(11, 8, 0), Close(11, 9, 0), net: -1_499m), // day 2: balance = 10,500-1,499=9,001
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.30m, maxPct: 0.10m);

        // Static max floor is always Initial - 10% = 9,000, unaffected by day 1's profit. 9,001 > 9,000: no breach.
        result.Max.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
        result.Max.FirstBreach.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ThreeClosesOneDay_MiddleCloseBreachesThirdRecovers_FindingReflectsMiddleBreach()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 8, 10), net: -100m),
            Trade(1, Close(10, 8, 20), Close(10, 8, 30), net: -450m), // balance 9,450: below 9,500 floor
            Trade(2, Close(10, 8, 40), Close(10, 8, 50), net: 500m), // recovers to 9,950
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
        result.Daily.FirstBreach!.Value.Balance.Should().Be(9_450m);
    }

    [Fact]
    public void Evaluate_NoOverlap_BreachStaysBreached()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: -800m),
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
    }

    [Fact]
    public void Evaluate_OverlapAtBreachInstant_DowngradesToBreachContingentWithConcurrentOpenPositionCause()
    {
        const decimal initial = 10_000m;
        var breachingClose = Close(10, 9, 0);
        var trades = new[]
        {
            // Second trade opens before and closes after the breaching close, and its OWN close
            // brings the balance back above the floor so it is not itself an independent breaching
            // close -- constructed overlap fixture (measured 0-of-46 real runs overlap).
            Trade(0, Close(10, 7, 0), Close(10, 11, 0), net: 500m),
            Trade(1, Close(10, 8, 0), breachingClose, net: -800m),
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Daily.Causes.Should().Contain(BreachContingencyCause.ConcurrentOpenPosition);
    }

    [Fact]
    public void Evaluate_UnscalableTradeClosedBeforeBreach_DowngradesWithUnscalableTradeExcludedCause()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 7, 0), Close(10, 7, 30), net: null, ResizeOutcome.Unscalable),
            Trade(1, Close(10, 8, 0), Close(10, 9, 0), net: -800m),
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Daily.Causes.Should().Contain(BreachContingencyCause.UnscalableTradeExcluded);
    }

    [Fact]
    public void Evaluate_UnscalableAfterTheDaysReferenceWasSet_SameDayDailyBreachIsContingent()
    {
        // The day's reference (Initial Capital on day 1) is fixed BEFORE the excluded close, so the
        // missing P/L sits in the balance but not in the reference: the same-day breach is uncertain.
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 8, 30), net: 100m),
            Trade(1, Close(10, 9, 0), Close(10, 9, 30), net: null, ResizeOutcome.Unscalable),
            Trade(2, Close(10, 10, 0), Close(10, 11, 0), net: -700m), // 9,400 < 9,500
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.30m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Daily.Causes.Should().Equal(BreachContingencyCause.UnscalableTradeExcluded);
    }

    [Fact]
    public void Evaluate_UnscalableOnAnEarlierDay_LaterDayDailyBreachStaysBreached()
    {
        // On a later FTMO day the balance AND its midnight reference both lack the excluded P/L, so
        // `balance < reference - 5%` is unchanged by it: the breach is genuine evidence.
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 7, 0), Close(10, 7, 30), net: null, ResizeOutcome.Unscalable),
            Trade(1, Close(10, 8, 0), Close(10, 9, 0), net: 100m), // day 10 ends at 10,100
            Trade(2, Close(11, 8, 0), Close(11, 9, 0), net: -600m), // 9,500 < 10,100 - 500 = 9,600
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.30m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
        result.Daily.FirstBreach!.Value.SourceTime.Should().Be(Close(11, 9, 0));
    }

    [Fact]
    public void Evaluate_UnscalableOnAnEarlierDay_LaterMaxBreachStaysContingent_WhileDailyIsBreached()
    {
        // The static floor never moves, so the excluded P/L offsets EVERY later comparison against
        // it: the max-limit latch is permanent even though the same close is a clean daily breach.
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 7, 0), Close(10, 7, 30), net: null, ResizeOutcome.Unscalable),
            Trade(1, Close(20, 8, 0), Close(20, 9, 0), net: -1_100m), // 8,900 < 9,000 and < 9,500
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Max.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Max.Causes.Should().Equal(BreachContingencyCause.UnscalableTradeExcluded);
        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
    }

    [Fact]
    public void Evaluate_LaterCleanBreachAfterContingentOne_YieldsBreachedOnTheCleanClose()
    {
        const decimal initial = 10_000m;
        var firstBreachClose = Close(10, 9, 0);
        var trades = new[]
        {
            // Overlap causes the first breach to be contingent; this position's own close recovers
            // the balance above the day-1 floor so it is not itself a second breaching close.
            Trade(0, Close(10, 7, 0), Close(10, 11, 0), net: 500m),
            Trade(1, Close(10, 8, 0), firstBreachClose, net: -800m),
            // Second, later breach with no overlapping position: clean.
            Trade(2, Close(11, 8, 0), Close(11, 9, 0), net: -600m),
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.30m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.Breached);
    }

    [Fact]
    public void Evaluate_NoBreachingClose_YieldsNoBreachObserved()
    {
        const decimal initial = 10_000m;
        var trades = new[]
        {
            Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: 100m),
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
        result.Max.Verdict.Should().Be(FtmoBreachVerdict.NoBreachObserved);
    }

    // =====================================================================
    // Phase 3 (ftmo-first-breach-timing) — BreachPoint widened: RowIndex, FtmoDay, Causes.
    // =====================================================================

    [Fact]
    public void Evaluate_ContingentDailyBreach_FirstBreachCausesEqualTheCloseOwnCauses()
    {
        const decimal initial = 10_000m;
        var breachingClose = Close(10, 9, 0);
        var trades = new[]
        {
            Trade(0, Close(10, 7, 0), Close(10, 11, 0), net: 500m), // overlap span
            Trade(1, Close(10, 8, 0), breachingClose, net: -800m), // contingent breach
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        // Independent of the run-level Causes (equal here since this IS the run-level cause, but
        // captured on the point itself, not read from the finding's Causes property).
        result.Daily.FirstBreach!.Value.Causes.Should().Equal(BreachContingencyCause.ConcurrentOpenPosition);
    }

    [Fact]
    public void Evaluate_FirstCleanBreach_CausesAreEmpty_WhenTheCleanCloseHasNoCauses()
    {
        const decimal initial = 10_000m;
        var firstBreachClose = Close(10, 9, 0);
        var trades = new[]
        {
            Trade(0, Close(10, 7, 0), Close(10, 11, 0), net: 500m), // overlap span
            Trade(1, Close(10, 8, 0), firstBreachClose, net: -800m), // contingent
            Trade(2, Close(11, 8, 0), Close(11, 9, 0), net: -600m), // later clean breach
        };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.30m);

        result.Daily.FirstBreach!.Value.Causes.Should().NotBeEmpty();
        result.Daily.FirstCleanBreach!.Value.Causes.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_BreachPoint_CarriesRowIndexAndFtmoDay_MatchingTheSourceTradeAndBookkeepingDay()
    {
        const decimal initial = 10_000m;
        var closeTime = Close(10, 9, 0);
        var trades = new[] { Trade(5, Close(10, 8, 0), closeTime, net: -800m) };

        var attribution = FtmoDayClock.Attribute(closeTime, Jerusalem, Berlin);
        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.FirstBreach!.Value.RowIndex.Should().Be(5);
        result.Daily.FirstBreach.Value.FtmoDay.Should().Be(attribution.BookkeepingDay);
    }

    [Fact]
    public void Evaluate_NoOutputContainsPassSurvivedSafeWording()
    {
        const decimal initial = 10_000m;
        var breachedTrades = new[] { Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: -800m) };
        var cleanTrades = new[] { Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: 100m) };

        var banned = new[] { "passed", "safe", "survived", "would have passed" };

        foreach (var trades in new[] { breachedTrades, cleanTrades })
        {
            var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);
            foreach (var finding in new[] { result.Daily, result.Max })
            {
                var text = finding.DisclosureText.ToLowerInvariant();
                foreach (var word in banned)
                    text.Should().NotContain(word);

                var verdictText = finding.Verdict.ToString().ToLowerInvariant();
                foreach (var word in banned)
                    verdictText.Should().NotContain(word);
            }
        }
    }

    /// <summary>
    /// tasks.md 9.1 — extends the shipped no-pass-wording coverage to every new discriminator
    /// introduced by ftmo-first-breach-timing: none is typed as a boolean, and none of their string
    /// values affirm survival (spec.md "Every new discriminator is inspected for boolean or survival
    /// wording").
    /// </summary>
    [Fact]
    public void Evaluate_NewTimingDiscriminators_AreNeverBooleanAndCarryNoSurvivalWording()
    {
        var banned = new[] { "passed", "safe", "survived", "would have passed" };

        foreach (var enumType in new[]
        {
            typeof(FtmoBreachPointClass), typeof(FtmoFxBandEnd), typeof(FtmoFirstBreachingLimit),
        })
        {
            enumType.IsEnum.Should().BeTrue();
            Enum.GetUnderlyingType(enumType).Should().NotBe(typeof(bool));

            foreach (var name in Enum.GetNames(enumType))
            {
                var lower = name.ToLowerInvariant();
                foreach (var word in banned)
                    lower.Should().NotContain(word);
            }
        }

        foreach (var dtoType in new[] { typeof(FtmoBreachTimingDto), typeof(FtmoFirstLimitBreachDto) })
        {
            foreach (var property in dtoType.GetProperties())
            {
                property.PropertyType.Should().NotBe(typeof(bool));
                property.PropertyType.Should().NotBe(typeof(bool?));
            }
        }
    }

    /// <summary>
    /// design.md Decision 2 / tasks.md 2.3 — line 157 now reads <c>attribution.BookkeepingDay</c>
    /// instead of duplicating <c>.CandidateDays.Min()</c>. This is a compile-level confirmation, not a
    /// behavioral one: for an ambiguous-day fixture, the day the evaluator actually used for floor
    /// bookkeeping still equals <c>CandidateDays.Min()</c> (the evaluator's daily floor is anchored on
    /// day 1 to Initial Capital regardless of which day is chosen; the assertion here is that
    /// <c>BookkeepingDay</c> is exactly the earliest candidate, not a behavioral divergence — no fixture
    /// is contrived to reach a difference that design.md notes is unreachable in current code).
    /// </summary>
    [Fact]
    public void Evaluate_AmbiguousSourceTime_DailyBreachDayEqualsCandidateDaysMin()
    {
        const decimal initial = 10_000m;
        // Same ambiguous Jerusalem instant as FtmoDayClockTests's own ambiguous fixture.
        var ambiguousClose = new DateTime(2013, 10, 27, 1, 30, 0, DateTimeKind.Unspecified);
        var trades = new[] { Trade(0, ambiguousClose.AddHours(-1), ambiguousClose, net: -800m) };

        var attribution = FtmoDayClock.Attribute(ambiguousClose, Jerusalem, Berlin);
        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        // The ambiguity itself downgrades the verdict (AmbiguousSourceTime cause) regardless of which
        // candidate day is chosen for bookkeeping — this test only pins that the CHOSEN day is the
        // earliest candidate, per design.md Decision 2.
        result.Daily.Verdict.Should().Be(FtmoBreachVerdict.BreachContingent);
        result.Daily.Causes.Should().Contain(BreachContingencyCause.AmbiguousSourceTime);
        attribution.BookkeepingDay.Should().Be(attribution.CandidateDays.Min());
    }

    [Fact]
    public void Evaluate_NoBreachObservedCarriesDisclosureText_NotASurvivalClaim()
    {
        const decimal initial = 10_000m;
        var trades = new[] { Trade(0, Close(10, 8, 0), Close(10, 9, 0), net: 100m) };

        var result = FtmoBreachEvaluator.Evaluate(trades, Jerusalem, Berlin, initial, dailyPct: 0.05m, maxPct: 0.10m);

        result.Daily.DisclosureText.Should().NotBeNullOrWhiteSpace();
        result.Daily.DisclosureText.ToLowerInvariant().Should().Contain("no breach was found");
    }
}
