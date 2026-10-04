using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1a.2 (design D3) — <see cref="FtmoDailyLossProfile"/> replicates the evaluator's daily-floor
/// bookkeeping (moving floor seeded from capital, reference = balance carried from the previous FTMO day, null
/// nets skipped). The first group pins it on hand-computed series; the second proves, against the UNEDITED
/// <see cref="FtmoBreachEvaluator"/> (only CALLED here), that the evaluator reports a daily breach iff the
/// profile's worst day loss exceeds the allowance, and on the same first day (hard rule 4a).
/// </summary>
public class FtmoDailyLossProfileTests
{
    private const decimal Capital = 10_000m;

    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static DateTime At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ProjectedTrade Trade(int row, DateTime close, decimal? net) =>
        new(row, close.AddHours(-1), close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, net is null ? 0m : 1m);

    private static DateOnly D(int month, int day) => new(2026, month, day);

    /// <summary>The day each close belongs to under the Berlin clock, exactly as the evaluator attributes it.</summary>
    private static DateOnly DayOf(DateTime close) => FtmoDayClock.Attribute(close, Jerusalem, Berlin).BookkeepingDay;

    private static FtmoDailyLossProfile.Profile Profile(IReadOnlyList<ProjectedTrade> series, decimal dailyPct = 0.05m) =>
        FtmoDailyLossProfile.Compute(series, DayOf, Capital, dailyPct);

    // ---- 1a.2.1: hand-computed series ----

    [Fact]
    public void Compute_TheWorstDayLossAndItsDay_AreTakenFromTheLowestIntradayBalance()
    {
        // Day 12: +100 -> 10100. Day 13: ref 10100; -300 -> 9800 (loss 300), +100 -> 9900. Day 14: ref 9900; -150 -> 9750.
        var series = new[]
        {
            Trade(0, At(1, 12, 10), 100m),
            Trade(1, At(1, 13, 10), -300m),
            Trade(2, At(1, 13, 12), 100m),
            Trade(3, At(1, 14, 10), -150m),
        };

        var p = Profile(series);

        p.WorstDayLoss.Should().Be(300m);
        p.WorstDay.Should().Be(D(1, 13));
    }

    [Fact]
    public void Compute_TheReferenceIsTheBalanceCarriedFromThePreviousDay_NotTheInitialCapital()
    {
        // Day 12 ends at 10500. Day 13 loses 400: from 10500 the loss is 400; from initial capital it would be -100.
        var series = new[] { Trade(0, At(1, 12, 10), 500m), Trade(1, At(1, 13, 10), -400m) };

        var p = Profile(series);

        p.WorstDayLoss.Should().Be(400m);
        p.WorstDay.Should().Be(D(1, 13));
    }

    [Fact]
    public void Compute_TheFirstDayStartsFromTheInitialCapital()
    {
        var p = Profile([Trade(0, At(1, 12, 10), -250m)]);

        p.WorstDayLoss.Should().Be(250m);
        p.WorstDay.Should().Be(D(1, 12));
    }

    [Fact]
    public void Compute_NullNetsAreSkipped_AndNeverStartADay()
    {
        var series = new[]
        {
            Trade(0, At(1, 12, 10), -100m),
            Trade(1, At(1, 13, 10), null),
            Trade(2, At(1, 14, 10), -50m),
        };

        var p = Profile(series);

        p.WorstDayLoss.Should().Be(100m);
        p.WorstDay.Should().Be(D(1, 12));
    }

    [Fact]
    public void Compute_ASeriesThatNeverLoses_HasNoWorstDay()
    {
        var p = Profile([Trade(0, At(1, 12, 10), 100m), Trade(1, At(1, 13, 10), 50m)]);

        p.WorstDayLoss.Should().Be(0m);
        p.WorstDay.Should().BeNull();
        p.FirstExceedingDay.Should().BeNull();
    }

    [Fact]
    public void Compute_TheFirstExceedingDay_IsTheFirstDayPastTheAllowance_WhateverTheWorstDay()
    {
        // Allowance = 5% x 10000 = 500. Day 12 loses 600 (first past it), day 13 loses 900 (the worst).
        var series = new[] { Trade(0, At(1, 12, 10), -600m), Trade(1, At(1, 13, 10), -900m) };

        var p = Profile(series);

        p.FirstExceedingDay.Should().Be(D(1, 12));
        p.WorstDay.Should().Be(D(1, 13));
        p.WorstDayLoss.Should().Be(900m);
    }

    [Fact]
    public void Compute_ALossExactlyAtTheAllowance_DoesNotExceedIt()
    {
        var p = Profile([Trade(0, At(1, 12, 10), -500m)]);

        p.WorstDayLoss.Should().Be(500m);
        p.FirstExceedingDay.Should().BeNull("exactly at the floor is not a breach");
    }

    [Fact]
    public void Compute_ACloseBeforeBerlinMidnightButAfterSourceMidnight_BelongsToThePreviousFtmoDay()
    {
        // 00:30 Jerusalem on Jan 13 is 23:30 Berlin on Jan 12 (winter, one hour apart): the loss is day 12's.
        var series = new[] { Trade(0, At(1, 12, 10), -100m), Trade(1, At(1, 13, 0, 30), -200m) };

        var p = Profile(series);

        p.WorstDay.Should().Be(D(1, 12));
        p.WorstDayLoss.Should().Be(300m, "both closes sit in the same FTMO day");
    }

    // ---- 1a.2.2: parity with the evaluator (hard rule 4a) ----

    /// <summary>Deterministic pseudo-random series (a fixed LCG: no RNG, same output every run).</summary>
    private static List<ProjectedTrade> Generated(int seed, DateTime start, int count, decimal scale, bool nullNets)
    {
        var state = (uint)seed;
        uint Next()
        {
            state = (state * 1664525u) + 1013904223u;
            return state >> 8;
        }

        var rows = new List<ProjectedTrade>();
        var close = start;
        for (var i = 0; i < count; i++)
        {
            close = close.AddMinutes(30 + (int)(Next() % 1400));
            decimal? net = nullNets && i % 9 == 4 ? null : ((int)(Next() % 2001) - 900) * scale;
            rows.Add(new ProjectedTrade(i, close.AddMinutes(-20), close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, 1m));
        }

        return rows;
    }

    private static MemberSeries Member(int order, IReadOnlyList<ProjectedTrade> low, decimal highFactor) =>
        new(order, low, [.. low.Select(t => t with { Net = t.Net * highFactor })]);

    private static (IReadOnlyList<ProjectedTrade> Low, IReadOnlyList<ProjectedTrade> High) WithHigh(List<ProjectedTrade> low, decimal factor) =>
        (low, [.. low.Select(t => t with { Net = t.Net * factor })]);

    public static TheoryData<string> Fixtures() => new()
    {
        "quiet",
        "breach-day",
        "merged-two",
        "merged-three-null-nets",
        "berlin-boundary-dst",
    };

    private static (IReadOnlyList<ProjectedTrade> Low, IReadOnlyList<ProjectedTrade> High) Fixture(string name)
    {
        switch (name)
        {
            case "quiet":
                return WithHigh(Generated(1, At(1, 5, 9), 60, 0.2m, nullNets: false), 1.1m);
            case "breach-day":
                {
                    var rows = Generated(2, At(1, 5, 9), 40, 0.4m, nullNets: false);
                    rows[10] = rows[10] with { Net = -900m };
                    return WithHigh(rows, 1.2m);
                }

            case "merged-two":
                {
                    var a = Member(0, Generated(3, At(2, 2, 9), 50, 0.5m, false), 1.1m);
                    var b = Member(1, Generated(4, At(2, 2, 10), 50, 0.5m, false), 0.9m);
                    var merged = Merge([a, b], Intersect([a, b])!.Value);
                    return (merged.Low, merged.High);
                }

            case "merged-three-null-nets":
                {
                    var a = Member(0, Generated(5, At(3, 2, 9), 45, 0.6m, true), 1.15m);
                    var b = Member(1, Generated(6, At(3, 2, 11), 45, 0.6m, true), 1.0m);
                    var c = Member(2, Generated(7, At(3, 3, 8), 45, 0.6m, false), 1.2m);
                    var merged = Merge([a, b, c], Intersect([a, b, c])!.Value);
                    return (merged.Low, merged.High);
                }

            default:
                // Late March: Israel and Germany switch to summer time on different days, and the closes sit around
                // the midnight that separates the Berlin FTMO days.
                return WithHigh(Generated(8, At(3, 24, 20), 70, 0.5m, nullNets: true), 1.1m);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Parity_TheEvaluatorReportsADailyBreachIffTheProfileExceedsTheAllowance_OnTheSameFirstDay(string name)
    {
        var (low, high) = Fixture(name);
        var breached = 0;
        var clean = 0;

        foreach (var series in new[] { low, high })
        {
            foreach (var dailyPct in new[] { 0.01m, 0.02m, 0.05m, 0.10m, 0.30m })
            {
                var allowance = dailyPct * Capital;
                var profile = FtmoDailyLossProfile.Compute(series, DayOf, Capital, dailyPct);
                var finding = FtmoBreachEvaluator.Evaluate(series, Jerusalem, Berlin, Capital, dailyPct, 0.9m).Daily;

                (finding.Verdict != FtmoBreachVerdict.NoBreachObserved).Should().Be(
                    profile.WorstDayLoss > allowance, $"{name} @ {dailyPct}: verdict {finding.Verdict}, worst {profile.WorstDayLoss}");
                if (finding.FirstBreach is { } first)
                {
                    breached++;
                    profile.FirstExceedingDay.Should().Be(first.FtmoDay, $"{name} @ {dailyPct}: the first breach day");
                }
                else
                {
                    clean++;
                    profile.FirstExceedingDay.Should().BeNull();
                }
            }
        }

        // The sweep must not be vacuous: every fixture produces both outcomes across its allowance range.
        (breached + clean).Should().Be(10);
        breached.Should().BeGreaterThan(0, $"{name} must breach at the tightest allowance");
        clean.Should().BeGreaterThan(0, $"{name} must stay clean at the loosest allowance");
    }
}
