using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMerger;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D3 — the multi-start race surrogate: a cheap stand-in for the shipped multi-start race, used ONLY
/// to order the shortlist. <c>internal static</c>, pure: no I/O, no clock, no randomness.
/// <para>
/// For each start the real engine would use (the first scalable open of every Berlin month, ascending), and for each FX
/// end, one pass replays the 2-Step chain over the merged series by close: phase 1 (10% target), phase 2 (5% target) on
/// the post-handover rows (<c>Open &gt;= T</c>, capital reset), then the funded phase (no target) to the end of data. It
/// tracks (a) a breach: the daily floor (the balance carried from the previous Berlin day, strictly below
/// <c>reference - dailyPct x capital</c>) or the static max floor (<c>capital x (1 - maxPct)</c>), reached before the
/// phase's target is DECIDED (a breach wins a same-close tie, as in the race); (b) the largest fraction of the daily and
/// max-loss allowances used along the chain; (c) the calendar days from the start to the phase-2 target.
/// </para>
/// <para>
/// SPEED. Walking every row from every start is O(starts x rows) and was 5x too slow for the proxy gate. Past the first
/// day boundary of a phase the balance is a shifted global running sum and the daily-loss series is start-independent,
/// so the pass answers "first breach", "first decided close" and "worst usage" with prefix sums, a next-breach array and
/// segment-tree descents (O(log rows)). Only the head of each phase (rows opened before the phase, and the phase's first
/// day) is walked row by row. <see cref="ComputeKindReference"/> walks every row and is the oracle the tests compare to.
/// </para>
/// <para>
/// APPROXIMATIONS versus the engine (all disclosed, none touches the shipped files): the trading-day count of a phase is
/// the distinct own-open days of the phase's rows opened before the group close (the engine also counts rows merely
/// closed by it, which differs only for a zero-duration row); the open-position test is the shipped sweep's
/// opens-before-minus-closes-among-them count; a breach is a breach on EITHER FX end (the engine reports the worse end),
/// and the days to both targets are the later of the two ends and missing unless both ends reach it; the funded phase
/// contributes a breach and a usage only (the ranking's funded share and its contingency flags are not modelled);
/// amounts are fixed-point integers at 1e-6, exact for money, which keeps the hot loop off <c>decimal</c> arithmetic.
/// </para>
/// </summary>
internal static class FtmoRaceSurrogate
{
    /// <summary>One kind's figures. The share is over the kind's starts; the usage is the worst over every start and both FX ends.</summary>
    /// <param name="MedianDays">Nearest-rank median of the calendar days to both targets; null when no start reaches both.</param>
    internal readonly record struct KindResult(int StartCount, int Breaches, decimal WorstUsed, int? MedianDays)
    {
        internal decimal BreachShare => StartCount == 0 ? 0m : (decimal)Breaches / StartCount;
    }

    /// <summary>
    /// The three shortlist keys. A zero or a negative headroom is a real value; only <see cref="MedianDays"/> can be missing.
    /// <paramref name="HasStarts"/> is false when no kind had a single start: the keys are then vacuous (breach 0, headroom 1)
    /// and the shortlist ranks the candidate after every candidate that has starts.
    /// </summary>
    internal readonly record struct Outcome(decimal BreachShare, decimal Headroom, int? MedianDays, bool HasStarts = true);

    private readonly record struct PassResult(bool Breached, decimal Used, int? DaysToBothTargets);

    private readonly record struct Rules(long Capital, long DailyAllowance, long MaxAllowance, long Phase1Gain, long Phase2Gain);

    /// <summary>
    /// The scalable rows of one merged series, in the two orders the pass needs. Shared by both FX ends. Arrays are
    /// thread-owned scratch (see <see cref="Scratch"/>): they have at least <see cref="N"/> slots and are overwritten
    /// by every call, because a search runs this on every candidate and the allocation, not the arithmetic, was the cost.
    /// </summary>
    private sealed class Prepared
    {
        internal int N;

        // By (close, row index).
        internal long[] CloseTicks = [];
        internal long[] OpenOfClose = [];
        internal int[] CloseDay = [];
        internal bool[] LastOfGroup = [];
        internal bool[] DayStart = [];
        internal bool[] NoOpen = [];
        internal long[] NetLow = [];
        internal long[] NetHigh = [];

        // By (open, row index).
        internal long[] OpenTicks = [];
        internal long[] CloseOfOpen = [];
        internal int[] OpenDay = [];

        /// <summary>Largest close among the first k+1 rows by open: tells whether a row opened before an instant is still open at it.</summary>
        internal long[] PrefixMaxClose = [];

        internal (long Ticks, int Day)[] Starts = [];
        internal int StartCount;

        internal void Ensure(int n)
        {
            if (CloseTicks.Length >= n)
                return;

            var capacity = Math.Max(n, CloseTicks.Length * 2);
            CloseTicks = new long[capacity];
            OpenOfClose = new long[capacity];
            CloseDay = new int[capacity];
            LastOfGroup = new bool[capacity];
            DayStart = new bool[capacity];
            NoOpen = new bool[capacity];
            NetLow = new long[capacity];
            NetHigh = new long[capacity];
            OpenTicks = new long[capacity];
            CloseOfOpen = new long[capacity];
            OpenDay = new int[capacity];
            PrefixMaxClose = new long[capacity];
            Starts = new (long, int)[capacity];
        }
    }

    /// <summary>The per-FX-end structures: running sums, the start-independent daily-loss series and its trees.</summary>
    private sealed class End
    {
        internal long[] Net = [];

        /// <summary><c>Cp[k]</c> is the sum of the first k nets by close.</summary>
        internal long[] Cp = [];

        internal int[] NextDaily = [];
        internal long[] MinCp = [];
        internal long[] OkMaxCp = [];
        internal long[] LossMax = [];
        internal int Size;

        internal void Ensure(int n, int size)
        {
            if (Cp.Length < n + 1)
            {
                Cp = new long[Math.Max(n + 1, Cp.Length * 2)];
                NextDaily = new int[Cp.Length];
            }

            if (MinCp.Length < 2 * size)
            {
                MinCp = new long[2 * size];
                OkMaxCp = new long[2 * size];
                LossMax = new long[2 * size];
            }
        }
    }

    /// <summary>One per thread: the buffers of the call in flight. A call never re-enters itself.</summary>
    private sealed class Scratch
    {
        internal readonly Prepared Rows = new();
        internal readonly End Low = new();
        internal readonly End High = new();
        internal readonly HashSet<int> Months = [];
        internal readonly List<int> Days = [];
        internal int[] Scalable = [];
        internal int[] ByClose = [];
        internal int[] ByOpen = [];
        internal long[] Keys = [];
    }

    [ThreadStatic]
    private static Scratch? scratch;

    /// <summary>The worse kind: the highest breach share, the lowest headroom, and the highest median (missing if any kind has none).</summary>
    internal static Outcome Aggregate(IReadOnlyList<KindResult> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        if (kinds.Count == 0)
            return new Outcome(0m, 1m, null, HasStarts: false);

        var breach = kinds.Max(k => k.BreachShare);
        var headroom = 1m - kinds.Max(k => k.WorstUsed);
        var median = kinds.Any(k => k.MedianDays is null) ? null : kinds.Max(k => k.MedianDays);
        return new Outcome(breach, headroom, median, kinds.Any(k => k.StartCount > 0));
    }

    /// <param name="dayOf">The bookkeeping (Berlin) day of an instant; the search passes its shared cache.</param>
    internal static KindResult ComputeKind(MergedSeries merged, Func<DateTime, DateOnly> dayOf, GroupParams p)
        => Compute(merged, dayOf, p, fast: true);

    /// <summary>The same figures by walking every row of every pass. Slow; the oracle for the tests.</summary>
    internal static KindResult ComputeKindReference(MergedSeries merged, Func<DateTime, DateOnly> dayOf, GroupParams p)
        => Compute(merged, dayOf, p, fast: false);

    private static KindResult Compute(MergedSeries merged, Func<DateTime, DateOnly> dayOf, GroupParams p, bool fast)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(dayOf);
        ArgumentNullException.ThrowIfNull(p);

        var rules = new Rules(
            Scaled(p.InitialCapital), Scaled(p.DailyPct * p.InitialCapital), Scaled(p.MaxPct * p.InitialCapital),
            Scaled(FtmoChallengeRules.Phase1TargetPct * p.InitialCapital), Scaled(FtmoChallengeRules.Phase2TargetPct * p.InitialCapital));

        var work = scratch ??= new Scratch();
        var series = Prepare(merged, dayOf, work);
        var low = BuildEnd(series, series.NetLow, rules, work.Low);
        var high = BuildEnd(series, series.NetHigh, rules, work.High);

        var breaches = 0;
        var worstUsed = 0m;
        var days = work.Days;
        days.Clear();

        for (var k = 0; k < series.StartCount; k++)
        {
            var (ticks, startDay) = series.Starts[k];
            var a = Pass(series, low, ticks, startDay, rules, fast);
            var b = Pass(series, high, ticks, startDay, rules, fast);

            if (a.Breached || b.Breached)
                breaches++;

            worstUsed = Math.Max(worstUsed, Math.Max(a.Used, b.Used));

            if (a.DaysToBothTargets is { } x && b.DaysToBothTargets is { } y)
                days.Add(Math.Max(x, y));
        }

        days.Sort();
        return new KindResult(series.StartCount, breaches, worstUsed, FtmoOrderStatistics.Compute(days).Median);
    }

    // ---- preparation ----

    private static Prepared Prepare(MergedSeries merged, Func<DateTime, DateOnly> dayOf, Scratch work)
    {
        var low = merged.Low;
        var high = merged.High;
        var rows = low.Count;
        if (work.Scalable.Length < rows)
        {
            var capacity = Math.Max(rows, work.Scalable.Length * 2);
            work.Scalable = new int[capacity];
            work.ByClose = new int[capacity];
            work.ByOpen = new int[capacity];
            work.Keys = new long[capacity];
        }

        var n = 0;
        for (var r = 0; r < rows; r++)
        {
            if (low[r].Net is not null && high[r].Net is not null)
                work.Scalable[n++] = r;
        }

        var s = work.Rows;
        s.Ensure(n);
        s.N = n;

        // By (close, row index): a primitive-key sort, then the ties by row index.
        for (var k = 0; k < n; k++)
        {
            work.ByClose[k] = work.Scalable[k];
            work.Keys[k] = low[work.Scalable[k]].CloseSource.Ticks;
        }

        Array.Sort(work.Keys, work.ByClose, 0, n);
        BreakTiesByRowIndex(work.Keys, work.ByClose, n, low);

        // By (open, row index): the merged series already is, so only a hand-built one pays for a sort.
        for (var k = 0; k < n; k++)
            work.ByOpen[k] = work.Scalable[k];

        if (!IsOpenOrdered(low, work.ByOpen, n))
        {
            for (var k = 0; k < n; k++)
                work.Keys[k] = low[work.ByOpen[k]].OpenSource.Ticks;

            Array.Sort(work.Keys, work.ByOpen, 0, n);
            BreakTiesByRowIndex(work.Keys, work.ByOpen, n, low);
        }

        for (var i = 0; i < n; i++)
        {
            var row = low[work.ByClose[i]];
            s.CloseTicks[i] = row.CloseSource.Ticks;
            s.OpenOfClose[i] = row.OpenSource.Ticks;
            s.CloseDay[i] = dayOf(row.CloseSource).DayNumber;
            s.NetLow[i] = Scaled(row.Net!.Value);
            s.NetHigh[i] = Scaled(high[work.ByClose[i]].Net!.Value);
        }

        // The same rule as the shipped start enumerator: the first scalable open of each Berlin month.
        work.Months.Clear();
        s.StartCount = 0;
        var maxClose = long.MinValue;
        for (var i = 0; i < n; i++)
        {
            var row = low[work.ByOpen[i]];
            var day = dayOf(row.OpenSource);
            s.OpenTicks[i] = row.OpenSource.Ticks;
            s.CloseOfOpen[i] = row.CloseSource.Ticks;
            s.OpenDay[i] = day.DayNumber;
            maxClose = Math.Max(maxClose, row.CloseSource.Ticks);
            s.PrefixMaxClose[i] = maxClose;
            if (work.Months.Add((day.Year * 12) + day.Month))
                s.Starts[s.StartCount++] = (row.OpenSource.Ticks, day.DayNumber);
        }

        // Group and day boundaries, and "is any position open at this group close" (the shipped sweep's count).
        var opensBefore = 0;
        var zeroDurationInGroup = 0;
        for (var i = 0; i < n; i++)
        {
            s.DayStart[i] = i == 0 || s.CloseDay[i] != s.CloseDay[i - 1];
            if (s.OpenOfClose[i] == s.CloseTicks[i])
                zeroDurationInGroup++;

            s.LastOfGroup[i] = i == n - 1 || s.CloseTicks[i + 1] != s.CloseTicks[i];
            s.NoOpen[i] = false;
            if (!s.LastOfGroup[i])
                continue;

            while (opensBefore < n && s.OpenTicks[opensBefore] < s.CloseTicks[i])
                opensBefore++;

            // Rows closed by this instant that were opened before it: every row up to here but the zero-duration ones of this group.
            s.NoOpen[i] = opensBefore - ((i + 1) - zeroDurationInGroup) == 0;
            zeroDurationInGroup = 0;
        }

        return s;
    }

    /// <summary>After a primitive sort by key, orders each run of equal keys by the rows' own index (insertion sort: runs are tiny).</summary>
    private static void BreakTiesByRowIndex(long[] keys, int[] order, int n, IReadOnlyList<ProjectedTrade> rows)
    {
        for (var start = 0; start < n;)
        {
            var end = start + 1;
            while (end < n && keys[end] == keys[start])
                end++;

            for (var i = start + 1; i < end; i++)
            {
                var item = order[i];
                var j = i - 1;
                while (j >= start && rows[order[j]].RowIndex > rows[item].RowIndex)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = item;
            }

            start = end;
        }
    }

    private static bool IsOpenOrdered(IReadOnlyList<ProjectedTrade> rows, int[] order, int n)
    {
        for (var i = 1; i < n; i++)
        {
            var a = rows[order[i - 1]];
            var b = rows[order[i]];
            if (a.OpenSource > b.OpenSource || (a.OpenSource == b.OpenSource && a.RowIndex > b.RowIndex))
                return false;
        }

        return true;
    }

    private static End BuildEnd(Prepared s, long[] net, Rules r, End e)
    {
        var n = s.N;
        var size = 1;
        while (size < n)
            size <<= 1;

        e.Ensure(n, size);
        e.Net = net;
        e.Size = size;

        Array.Fill(e.MinCp, long.MaxValue, 0, 2 * size);
        Array.Fill(e.OkMaxCp, long.MinValue, 0, 2 * size);
        Array.Fill(e.LossMax, long.MinValue, 0, 2 * size);

        e.Cp[0] = 0;
        for (var j = 0; j < n; j++)
            e.Cp[j + 1] = e.Cp[j] + net[j];

        var dayFirst = 0;
        for (var j = 0; j < n; j++)
        {
            if (s.DayStart[j])
                dayFirst = j;

            e.MinCp[size + j] = e.Cp[j + 1];
            e.OkMaxCp[size + j] = s.LastOfGroup[j] && s.NoOpen[j] ? e.Cp[j + 1] : long.MinValue;
            e.LossMax[size + j] = e.Cp[dayFirst] - e.Cp[j + 1];
        }

        for (var i = size - 1; i >= 1; i--)
        {
            e.MinCp[i] = Math.Min(e.MinCp[2 * i], e.MinCp[(2 * i) + 1]);
            e.OkMaxCp[i] = Math.Max(e.OkMaxCp[2 * i], e.OkMaxCp[(2 * i) + 1]);
            e.LossMax[i] = Math.Max(e.LossMax[2 * i], e.LossMax[(2 * i) + 1]);
        }

        e.NextDaily[n] = n;
        for (var j = n - 1; j >= 0; j--)
            e.NextDaily[j] = e.LossMax[size + j] > r.DailyAllowance ? j : e.NextDaily[j + 1];

        return e;
    }

    // ---- the pass ----

    /// <summary>One start on one FX end: phase 1, then phase 2, then the funded phase, stopping at the first breach.</summary>
    private static PassResult Pass(Prepared s, End e, long startTicks, int startDay, Rules r, bool fast)
    {
        var n = s.N;
        var net = e.Net;
        var phase = 0;
        var lower = startTicks;
        var i0 = LowerBound(s.CloseTicks, n, lower);
        var worstLoss = 0L;
        var worstDraw = 0L;
        int? daysToBoth = null;
        var breached = false;

        while (!breached)
        {
            var hasTarget = phase < 2;
            var target = r.Capital + (phase == 0 ? r.Phase1Gain : r.Phase2Gain);
            var o0 = LowerBound(s.OpenTicks, n, lower);

            // A later phase takes rows that close AFTER the handover: a zero-duration row at the handover instant is not one.
            while (phase > 0 && o0 < n && s.OpenTicks[o0] == lower && s.CloseOfOpen[o0] == lower)
                o0++;

            // The walked head: rows opened before the phase that are still open at its start, then up to the first day boundary.
            var headEnd = n;
            if (fast)
            {
                var first = i0;
                if (o0 > 0 && s.PrefixMaxClose[o0 - 1] >= lower)
                    first = Math.Max(first, UpperBound(s.CloseTicks, n, s.PrefixMaxClose[o0 - 1]));

                headEnd = first;
                while (headEnd < n && !s.DayStart[headEnd])
                    headEnd++;
            }

            var balance = r.Capital;
            var reference = r.Capital;
            var currentDay = int.MinValue;
            var o = o0;
            var opened = 0;
            var closed = 0;
            var tradingDays = 0;
            var lastOpenDay = int.MinValue;
            var zeroDuration = 0;
            var decidedAt = -1;

            for (var i = i0; i < headEnd; i++)
            {
                var open = s.OpenOfClose[i];
                if (open >= lower)
                {
                    var day = s.CloseDay[i];
                    if (day != currentDay)
                    {
                        reference = balance;
                        currentDay = day;
                    }

                    balance += net[i];
                    var loss = reference - balance;
                    if (loss > worstLoss)
                        worstLoss = loss;

                    var draw = r.Capital - balance;
                    if (draw > worstDraw)
                        worstDraw = draw;

                    if (loss > r.DailyAllowance || draw > r.MaxAllowance)
                    {
                        breached = true;
                        break;
                    }

                    closed++;
                    if (open == s.CloseTicks[i])
                        zeroDuration++;
                }

                if (!s.LastOfGroup[i])
                    continue;

                var groupClose = s.CloseTicks[i];
                while (o < n && s.OpenTicks[o] < groupClose)
                {
                    if (s.OpenDay[o] != lastOpenDay)
                    {
                        tradingDays++;
                        lastOpenDay = s.OpenDay[o];
                    }

                    opened++;
                    o++;
                }

                if (hasTarget && balance >= target && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase && opened - (closed - zeroDuration) <= 0)
                {
                    decidedAt = i;
                    break;
                }

                zeroDuration = 0;
            }

            if (breached)
                break;

            if (decidedAt < 0 && headEnd < n)
            {
                // From the day boundary on, the balance is the running sum shifted by a constant: answer by descent.
                var shift = balance - e.Cp[headEnd];
                var maxBreach = Math.Min(FirstBelow(e.MinCp, e.Size, headEnd, r.Capital - r.MaxAllowance - shift), n);
                var breach = Math.Min(e.NextDaily[headEnd], maxBreach);

                var decided = n;
                if (hasTarget)
                {
                    var from = Math.Max(headEnd, MinDaysIndex(s, o0));
                    if (from < n)
                        decided = Math.Min(FirstAtLeast(e.OkMaxCp, e.Size, from, target - shift), n);
                }

                int last;
                if (breach < n && breach <= decided)
                {
                    breached = true;
                    last = breach;
                }
                else if (decided < n)
                {
                    decidedAt = decided;
                    last = decided;
                }
                else
                {
                    last = n - 1;
                }

                worstLoss = Math.Max(worstLoss, RangeMax(e.LossMax, e.Size, headEnd, last));
                worstDraw = Math.Max(worstDraw, r.Capital - shift - RangeMin(e.MinCp, e.Size, headEnd, last));
            }

            if (breached || decidedAt < 0)
                break;

            // The phase is decided at this close: the next one restarts at capital over the rows opened from here on.
            phase++;
            if (phase == 2)
                daysToBoth = s.CloseDay[decidedAt] - startDay;

            lower = s.CloseTicks[decidedAt];
            i0 = decidedAt + 1;
        }

        var used = Math.Max((decimal)worstLoss / r.DailyAllowance, (decimal)worstDraw / r.MaxAllowance);
        return new PassResult(breached, used, breached ? null : daysToBoth);
    }

    /// <summary>The first close index at which the rows opened from <paramref name="o0"/> span the minimum trading days; the row count when never.</summary>
    private static int MinDaysIndex(Prepared s, int o0)
    {
        var days = 0;
        var lastDay = int.MinValue;
        for (var o = o0; o < s.N; o++)
        {
            if (s.OpenDay[o] == lastDay)
                continue;

            lastDay = s.OpenDay[o];
            if (++days == FtmoChallengeRules.MinTradingDaysPerPhase)
                return LowerBound(s.CloseTicks, s.N, s.OpenTicks[o] + 1);
        }

        return s.N;
    }

    // ---- fixed point and search helpers ----

    private const decimal Scale = 1_000_000m;

    private static long Scaled(decimal value) => (long)decimal.Round(value * Scale, MidpointRounding.AwayFromZero);

    /// <summary>The first index below <paramref name="count"/> whose value is at least <paramref name="value"/>; the count when none is.</summary>
    private static int LowerBound(long[] sorted, int count, long value)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    /// <summary>The first index below <paramref name="count"/> whose value is greater than <paramref name="value"/>; the count when none is.</summary>
    private static int UpperBound(long[] sorted, int count, long value)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid] <= value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // ---- segment trees (iterative, power-of-two size, leaves at [size, 2 size)) ----

    /// <summary>The first leaf index at or after <paramref name="from"/> whose value is at least <paramref name="x"/>; <paramref name="size"/> when none.</summary>
    private static int FirstAtLeast(long[] tree, int size, int from, long x)
    {
        if (from >= size)
            return size;

        var i = from + size;
        while (true)
        {
            if (tree[i] >= x)
            {
                while (i < size)
                {
                    i <<= 1;
                    if (tree[i] < x)
                        i++;
                }

                return i - size;
            }

            while ((i & 1) == 1)
                i >>= 1;

            if (i == 0)
                return size;

            i++;
        }
    }

    /// <summary>The first leaf index at or after <paramref name="from"/> whose value is below <paramref name="x"/>; <paramref name="size"/> when none.</summary>
    private static int FirstBelow(long[] tree, int size, int from, long x)
    {
        if (from >= size)
            return size;

        var i = from + size;
        while (true)
        {
            if (tree[i] < x)
            {
                while (i < size)
                {
                    i <<= 1;
                    if (tree[i] >= x)
                        i++;
                }

                return i - size;
            }

            while ((i & 1) == 1)
                i >>= 1;

            if (i == 0)
                return size;

            i++;
        }
    }

    private static long RangeMax(long[] tree, int size, int left, int right)
    {
        var result = long.MinValue;
        for (int l = left + size, r = right + size + 1; l < r; l >>= 1, r >>= 1)
        {
            if ((l & 1) == 1)
                result = Math.Max(result, tree[l++]);

            if ((r & 1) == 1)
                result = Math.Max(result, tree[--r]);
        }

        return result;
    }

    private static long RangeMin(long[] tree, int size, int left, int right)
    {
        var result = long.MaxValue;
        for (int l = left + size, r = right + size + 1; l < r; l >>= 1, r >>= 1)
        {
            if ((l & 1) == 1)
                result = Math.Min(result, tree[l++]);

            if ((r & 1) == 1)
                result = Math.Min(result, tree[--r]);
        }

        return result;
    }
}
