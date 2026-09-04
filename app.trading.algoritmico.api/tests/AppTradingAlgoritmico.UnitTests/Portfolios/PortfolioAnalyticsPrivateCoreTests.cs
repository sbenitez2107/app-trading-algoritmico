using System.Reflection;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Portfolios;

/// <summary>
/// Unit tests for the two non-public cores this slice adds beside <c>Percentile</c> inside
/// <see cref="PortfolioAnalyticsCalculator"/> (design D4 / D4b, tasks 1.3 and 1.4):
/// <c>SupportedPercentile</c> — the negative-observation support gate — and
/// <c>SeriesDensity Measure(..)</c>, the single derivation of the counts that gate and get
/// reported.
///
/// WHY REFLECTION. Both cores are deliberately non-public: D4 places the gate beside the percentile
/// it guards (private), and D4/D7 keep the density measurement inside Infrastructure because
/// <c>AnalyticsSeries</c> is <c>internal</c> there. Their first public consumers are the
/// <c>BacktestNetSeries[]</c> adapters, which land in the NEXT work unit — so in this one there is
/// no public surface through which the predicate's table can be pinned. This project's precedent
/// for pinning a non-public guarantee is a reflection test (slice 2a's `BacktestNetSeries` shape
/// guard), and the reflection binding doubles as a rename tripwire: rename or re-shape either core
/// and these tests fail loudly rather than silently stop covering it.
///
/// The predicate under test, from `portfolio-monthly-var`:
/// <c>Percentile(sorted, p)</c> reads <c>sorted[floor(p * (N-1))]</c>, and on an ascending sort the
/// negatives occupy indices <c>0 .. negativeCount-1</c> — a positive observation sorts ABOVE the
/// zero block and can never supply the mass that index needs. So the read is supported exactly when
/// <c>negativeCount >= floor(p * (N-1)) + 1</c>. It is a relation against the NEGATIVE count and
/// the percentile being computed — never a hard-coded share, and never the non-zero share.
/// </summary>
public class PortfolioAnalyticsPrivateCoreTests
{
    private static MethodInfo NonPublicStatic(string name)
    {
        var method = typeof(PortfolioAnalyticsCalculator)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull(
            "`{0}` must exist as a non-public static core on PortfolioAnalyticsCalculator", name);
        return method!;
    }

    private static decimal? InvokeSupportedPercentile(IReadOnlyList<decimal> sorted, double p)
        => (decimal?)NonPublicStatic("SupportedPercentile").Invoke(null, [sorted, p]);

    private static decimal InvokePercentile(IReadOnlyList<decimal> sorted, double p)
        => (decimal)NonPublicStatic("Percentile").Invoke(null, [sorted, p])!;

    /// <summary>
    /// An ASCENDING population of <paramref name="total"/> observations holding exactly
    /// <paramref name="negatives"/> negative values and <paramref name="positives"/> positive ones,
    /// the remainder zero — the shape a dense daily net series actually has (D4).
    /// </summary>
    private static List<decimal> Population(int total, int negatives, int positives = 0)
    {
        var series = new List<decimal>(total);
        for (var i = 0; i < negatives; i++) series.Add(-(negatives - i));
        for (var i = 0; i < total - negatives - positives; i++) series.Add(0m);
        for (var i = 0; i < positives; i++) series.Add(i + 1);
        series.Should().HaveCount(total);
        return series;
    }

    // -------------------------------------------------------------------------
    // 1.3 — SupportedPercentile: null iff negativeCount < floor(p * (N-1)) + 1.
    // -------------------------------------------------------------------------

    [Theory]
    // The measured IST fixture shape: N = 3,860 needs >= floor(0.05 * 3859) + 1 = 193 negative days
    // and has 164 — withheld. Its OOST counterpart needs >= 191 and has 172 — also withheld.
    [InlineData(3860, 0.05, 164, false)]
    [InlineData(3804, 0.05, 172, false)]
    // The boundary neither committed fixture can reach: exactly one short, and exactly on it.
    [InlineData(3860, 0.05, 192, false)]
    [InlineData(3860, 0.05, 193, true)]
    [InlineData(3804, 0.05, 190, false)]
    [InlineData(3804, 0.05, 191, true)]
    // VaR99 reads DEEPER into the tail than VaR95: floor(0.01 * 3859) + 1 = 39, so the SAME 164
    // negative days that fail p = 0.05 clear p = 0.01. One population, two verdicts — the gate is
    // evaluated independently per confidence level.
    [InlineData(3860, 0.01, 38, false)]
    [InlineData(3860, 0.01, 39, true)]
    [InlineData(3860, 0.01, 164, true)]
    // Small populations: the relation still derives its threshold, it is never a fixed count.
    [InlineData(5, 0.05, 0, false)]
    [InlineData(5, 0.05, 1, true)]
    [InlineData(1, 0.05, 0, false)]
    [InlineData(1, 0.05, 1, true)]
    public void SupportedPercentile_ReportsOnlyWhenNegativeCountReachesThePercentileIndex(
        int total, double p, int negatives, bool expectedSupported)
    {
        var required = (int)Math.Floor(p * (total - 1)) + 1;

        var actual = InvokeSupportedPercentile(Population(total, negatives), p);

        if (expectedSupported)
            actual.Should().NotBeNull(
                "{0} negative observations reach the {1} that floor({2} * {3}) + 1 requires",
                negatives, required, p, total - 1);
        else
            actual.Should().BeNull(
                "{0} negative observations fall short of the {1} that floor({2} * {3}) + 1 requires",
                negatives, required, p, total - 1);
    }

    [Fact]
    public void SupportedPercentile_EmptySeries_IsWithheld()
    {
        // No observations cannot support any percentile — and must not collapse to a numeric 0,
        // which is the exact failure mode the gate exists to prevent.
        InvokeSupportedPercentile([], 0.05).Should().BeNull();
    }

    [Fact]
    public void SupportedPercentile_WhenSupported_ReturnsTheUngatedPercentileVerbatim()
    {
        // The gate changes the VERDICT, never the NUMBER. `Percentile`'s body is untouched by this
        // slice, so a supported read must be bit-identical to the ungated one.
        var population = Population(3860, 193);

        var gated = InvokeSupportedPercentile(population, 0.05);

        gated.Should().NotBeNull();
        gated!.Value.Should().Be(
            InvokePercentile(population, 0.05), "a supported read is the ungated percentile verbatim");
    }

    [Fact]
    public void SupportedPercentile_HighNonZeroShareDoesNotClearTheGate()
    {
        // The predicate MUST NOT be a non-zero share. This population mirrors the measured IST
        // fixture's shares — 8.24% non-zero (318 of 3,860) but only 164 negative — so a "non-zero
        // share >= 5% ⇒ report" gate would REPORT here while the truthful answer is withheld.
        // 164 positives + 164 negatives is 8.50% non-zero, comfortably clear of any 5% bar.
        var population = Population(3860, negatives: 164, positives: 164);
        var nonZeroShare = 328m / 3860m;

        nonZeroShare.Should().BeGreaterThan(0.05m, "the wrong predicate's threshold is cleared");
        InvokeSupportedPercentile(population, 0.05).Should().BeNull(
            "the relation is against the NEGATIVE count (164 < 193), never the non-zero share");
    }

    [Fact]
    public void SupportedPercentile_PositiveObservationsNeverSupplyTheMissingMass()
    {
        // Same 192 negatives — one short of the 193 the index needs — with and without a large
        // block of positives. Adding positives cannot change the verdict, because they sort ABOVE
        // the zero block and the read index is below it.
        InvokeSupportedPercentile(Population(3860, negatives: 192), 0.05).Should().BeNull();
        InvokeSupportedPercentile(Population(3860, negatives: 192, positives: 3000), 0.05)
            .Should().BeNull("positives sort above the zeros and can never reach index 192");
    }

    // -------------------------------------------------------------------------
    // 1.4 — SeriesDensity Measure(denseDailyNets): ONE derivation of the reported counts.
    // -------------------------------------------------------------------------

    private static (int Dense, int Negative, int NonZero, int NegativeWindows) InvokeMeasure(
        IReadOnlyList<decimal> denseDailyNets)
    {
        var density = NonPublicStatic("Measure").Invoke(null, [denseDailyNets]);
        density.Should().NotBeNull();

        var type = density!.GetType();
        type.Name.Should().Be("SeriesDensity", "Measure returns the density value object beside it");

        int Read(string property)
        {
            var info = type.GetProperty(property);
            info.Should().NotBeNull("SeriesDensity must carry `{0}`", property);
            return (int)info!.GetValue(density)!;
        }

        return (Read("DenseDayCount"), Read("NegativeDayCount"), Read("NonZeroDayCount"),
            Read("NegativeWindowCount"));
    }

    [Fact]
    public void Measure_CountsDenseNegativeAndNonZeroDaysPlusNegativeRollingWindows()
    {
        // 100 dense calendar days: day 0 = -1,000, days 1..99 = +10.
        //   DenseDayCount        = 100 (the series is already dense — one element per calendar day)
        //   NegativeDayCount     =   1
        //   NonZeroDayCount      = 100 (every day traded)
        //   NegativeWindowCount  =   1 — of the 71 rolling 30-day sums, only the window starting at
        //                            day 0 spans the loss: -1000 + 29*10 = -710; the other 70 are
        //                            30 * 10 = +300.
        var series = new List<decimal> { -1_000m };
        for (var i = 1; i < 100; i++) series.Add(10m);

        InvokeMeasure(series).Should().Be((100, 1, 100, 1));
    }

    [Fact]
    public void Measure_ZeroDaysAreNeitherNegativeNorNonZero()
    {
        // 40 zero-net dense days ⇒ 11 rolling window sums, every one exactly 0. A zero is NOT a
        // negative observation: it cannot supply the mass a low percentile index needs, which is
        // the whole reason the gate counts negatives rather than non-zeros.
        var series = Enumerable.Repeat(0m, 40).ToList();

        InvokeMeasure(series).Should().Be((40, 0, 0, 0));
    }

    [Fact]
    public void Measure_SeriesShorterThanTheMonthlyHorizon_HasNoWindows()
    {
        // 10 days is shorter than the 30-calendar-day monthly horizon, so no rolling window exists
        // at all — the negative-window count is 0 by absence of windows, not by their sign.
        // 3 negative days (-5, -5, -1) and 4 non-zero days (those three plus the +3).
        var series = new List<decimal> { -5m, -5m, 0m, 0m, 3m, 0m, -1m, 0m, 0m, 0m };

        InvokeMeasure(series).Should().Be((10, 3, 4, 0));
    }

    [Fact]
    public void Measure_EmptySeries_IsAllZeroCounts()
    {
        InvokeMeasure([]).Should().Be((0, 0, 0, 0));
    }

    [Fact]
    public void Measure_MixedSeries_NegativeWindowCountMatchesTheRollingSumsSigns()
    {
        // 60 dense days: a -100 day every 30th day (days 0 and 30), every other day +1.
        //   NegativeDayCount = 2, NonZeroDayCount = 60.
        //   Rolling 30-day sums: 31 windows. Window i covers days i..i+29.
        //     i = 0  → day 0 and day 29?  day 0 is -100, days 1..29 are +1 (29) → -71  (negative)
        //     i = 1..30 → each covers day 30 (-100) plus 29 of the +1 days → -71     (negative)
        //   So EVERY one of the 31 windows spans exactly one -100 day ⇒ all 31 are negative.
        var series = new List<decimal>();
        for (var i = 0; i < 60; i++) series.Add(i % 30 == 0 ? -100m : 1m);

        InvokeMeasure(series).Should().Be((60, 2, 60, 31));
    }
}
