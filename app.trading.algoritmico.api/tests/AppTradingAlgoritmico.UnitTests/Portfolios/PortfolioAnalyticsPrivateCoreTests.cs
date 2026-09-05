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
/// <c>Percentile(sorted, p)</c> INTERPOLATES between <c>sorted[floor(p * (N-1))]</c> and
/// <c>sorted[ceil(p * (N-1))]</c>, and on an ascending sort the negatives occupy indices
/// <c>0 .. negativeCount-1</c> — a zero-net day and a winning day both sort ABOVE them and neither
/// can supply the mass a loss estimate needs. So the PUBLISHED FIGURE is supported exactly when
/// BOTH endpoints are losses: <c>negativeCount >= ceil(p * (N-1)) + 1</c>, which is
/// <c>floor(p * (N-1)) + 2</c> whenever the rank is not a whole number. It is a relation against
/// the NEGATIVE count and the percentile being computed — never a hard-coded share, and never the
/// non-zero share.
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
    // 1.3 — SupportedPercentile: null iff negativeCount < ceil(p * (N-1)) + 1.
    // -------------------------------------------------------------------------

    [Theory]
    // The measured IST fixture shape: N = 3,860 needs >= ceil(0.05 * 3859) + 1 = 194 negative days
    // and has 164 — withheld. Its OOST counterpart needs >= 192 and has 172 — also withheld.
    [InlineData(3860, 0.05, 164, false)]
    [InlineData(3804, 0.05, 172, false)]
    // The boundary neither committed fixture can reach: exactly one short, and exactly on it.
    // 193 and 191 are the counts the SUPERSEDED `floor + 1` relation called supported; both now
    // withhold, because at exactly those counts the second interpolation endpoint is the first
    // NON-NEGATIVE observation and the published figure would be partly drawn from the zero block.
    [InlineData(3860, 0.05, 193, false)]
    [InlineData(3860, 0.05, 194, true)]
    [InlineData(3804, 0.05, 191, false)]
    [InlineData(3804, 0.05, 192, true)]
    // VaR99 reads DEEPER into the tail than VaR95: ceil(0.01 * 3859) + 1 = 40, so the SAME 164
    // negative days that fail p = 0.05 clear p = 0.01. One population, two verdicts — the gate is
    // evaluated independently per confidence level.
    [InlineData(3860, 0.01, 39, false)]
    [InlineData(3860, 0.01, 40, true)]
    [InlineData(3860, 0.01, 164, true)]
    // Small populations: the relation still derives its threshold, it is never a fixed count.
    [InlineData(5, 0.05, 1, false)]
    [InlineData(5, 0.05, 2, true)]
    // N = 1 is the WHOLE-RANK case: 0.05 * 0 = 0, so lo == hi, `Percentile` returns sorted[0]
    // verbatim and there is no second endpoint to defend. One loss is the entire published figure,
    // so one loss is enough — the relation is as strict as the number requires and no stricter.
    [InlineData(1, 0.05, 0, false)]
    [InlineData(1, 0.05, 1, true)]
    public void SupportedPercentile_ReportsOnlyWhenNegativeCountReachesEveryIndexTheFigureIsComposedOf(
        int total, double p, int negatives, bool expectedSupported)
    {
        var required = (int)Math.Ceiling(p * (total - 1)) + 1;

        var actual = InvokeSupportedPercentile(Population(total, negatives), p);

        if (expectedSupported)
            actual.Should().NotBeNull(
                "{0} negative observations reach the {1} that ceil({2} * {3}) + 1 requires",
                negatives, required, p, total - 1);
        else
            actual.Should().BeNull(
                "{0} negative observations fall short of the {1} that ceil({2} * {3}) + 1 requires",
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
        var population = Population(3860, 194);

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
            "the relation is against the NEGATIVE count (164 < 194), never the non-zero share");
    }

    [Fact]
    public void SupportedPercentile_PositiveObservationsNeverSupplyTheMissingMass()
    {
        // Same 193 negatives — one short of the 194 the two read indices need — with and without a
        // large block of positives. Adding positives cannot change the verdict, because they sort
        // ABOVE the zero block and both read indices are below it.
        InvokeSupportedPercentile(Population(3860, negatives: 193), 0.05).Should().BeNull();
        InvokeSupportedPercentile(Population(3860, negatives: 193, positives: 3000), 0.05)
            .Should().BeNull("positives sort above the zeros and can never reach index 192 or 193");
    }

    // -------------------------------------------------------------------------
    // RELIABILITY-001 — the gate must defend the value that is PUBLISHED, not one index of it.
    //
    // `Percentile` does not read a single index. It reads TWO — `sorted[lo]` and `sorted[lo + 1]`
    // — and returns a LINEAR INTERPOLATION between them. A gate stated over `lo` alone therefore
    // authorises a figure that is partly determined by `sorted[lo + 1]`, which at the exact
    // boundary is by construction the FIRST NON-NEGATIVE observation. Asserting `NotBeNull()` at a
    // boundary cannot see that: these tests assert the published VALUE.
    // -------------------------------------------------------------------------

    /// <summary>The two indices <c>Percentile</c> actually interpolates between, and the weight.</summary>
    private static (int Lo, int Hi, decimal Frac) ReadIndices(int count, double p)
    {
        var rank = p * (count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return (lo, hi, (decimal)(rank - lo));
    }

    [Theory]
    [InlineData(3860, 0.05, 193)]
    [InlineData(3860, 0.05, 194)]
    [InlineData(3860, 0.05, 3860)]
    [InlineData(3804, 0.05, 191)]
    [InlineData(3804, 0.05, 192)]
    [InlineData(3860, 0.01, 39)]
    [InlineData(3860, 0.01, 40)]
    [InlineData(91, 0.05, 5)]
    [InlineData(91, 0.05, 6)]
    public void SupportedPercentile_APublishedFigureIsComposedOnlyOfLosses(
        int total, double p, int negatives)
    {
        // Whatever the gate authorises, EVERY observation that determines the number must itself be
        // a loss. A figure the gate calls "supported" while an interpolation endpoint is a zero-fill
        // or a win is exactly the contamination this gate exists to eliminate.
        var population = Population(total, negatives);
        var (lo, hi, _) = ReadIndices(total, p);

        var published = InvokeSupportedPercentile(population, p);

        if (published is null) return;

        population[lo].Should().BeNegative(
            "index {0} determines the published figure {1}", lo, published);
        population[hi].Should().BeNegative(
            "index {0} is the second interpolation endpoint of the published figure {1}", hi, published);
    }

    [Fact]
    public void SupportedPercentile_ExactlyOnTheOldThreshold_IsNotPublishedDilutedByTheZeroBlock()
    {
        // 3,860 observations, 193 negatives — the count the ORIGINAL relation (`floor(p(N-1)) + 1`)
        // called supported. `Percentile` reads sorted[192] = -1 and sorted[193] = 0 with weight
        // 0.95, so the number it publishes is -0.05: 95% of it comes from the ZERO BLOCK. This is
        // the "0.00 that still passes" the gate was written to prevent, one index to the right.
        var population = Population(3860, negatives: 193);

        // `Percentile` derives its weight from a double, so the figure carries the rounding of that
        // conversion — 0.95 is not exactly representable. That is not this finding, but it is why
        // every value assertion here is approximate rather than exact.
        InvokePercentile(population, 0.05).Should().BeApproximately(
            -0.05m, 0.000001m, "the ungated interpolation is 95% zero-fill");
        population[193].Should().Be(0m, "the second endpoint is the first observation of the zero block");

        InvokeSupportedPercentile(population, 0.05).Should().BeNull(
            "a figure 95% determined by a zero-net day is not a loss estimate");
    }

    [Fact]
    public void SupportedPercentile_ExactlyOnTheOldThreshold_IsNotPublishedWithAnInvertedSign()
    {
        // The same boundary with a WIN as the second endpoint instead of a zero. 91 observations,
        // 5 negatives (-5..-1) then 86 wins of +900. Percentile reads sorted[4] = -1 and
        // sorted[5] = +900 at weight 0.5 and returns +449.50 — a POSITIVE low percentile, which the
        // caller negates into a NEGATIVE "loss magnitude" and the UI renders, because the figure is
        // not null.
        var population = new List<decimal>();
        for (var i = 0; i < 5; i++) population.Add(-(5 - i));
        for (var i = 0; i < 86; i++) population.Add(900m);

        InvokePercentile(population, 0.05).Should().BeApproximately(
            449.50m, 0.000001m, "the win dominates the interpolation");

        InvokeSupportedPercentile(population, 0.05).Should().BeNull(
            "a percentile that a WIN pulls above zero is not a loss estimate at any confidence level");
    }

    [Fact]
    public void SupportedPercentile_OneAboveTheOldThreshold_PublishesAFigureEveryPartOfWhichIsALoss()
    {
        // The reporting side of the same boundary, pinned as a VALUE. 194 negatives put BOTH
        // endpoints inside the negative block: sorted[192] = -2, sorted[193] = -1, weight 0.95.
        var population = Population(3860, negatives: 194);

        InvokeSupportedPercentile(population, 0.05).Should().BeApproximately(
            -1.05m, 0.000001m, "both interpolation endpoints are losses, so the published figure is one too");
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
