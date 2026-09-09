using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// One trade's opening moment, price, and direction, projected without any EF entity type.
/// <see cref="Type"/> is the raw order-direction string ("buy"/"sell"/"Buy"/"Sell", compared
/// case-insensitively) and is part of the pairing key alongside the truncated open minute — see
/// <see cref="DemoBacktestComparabilityCalculator"/> remarks and spec Requirement "Exact-Minute
/// Pairing Detects Price-Series Comparability, Not Trade Correspondence".
/// <para>
/// <see cref="NetPl"/> is this one trade's net P/L on ITS OWN side's cost basis — the projection
/// that reads the row decides it, because demo and backtest do not compute net the same way (see
/// <see cref="NetPlBasis"/>). It is <c>null</c> for an observation whose net P/L was not read, in
/// which case the subset containing it reports no net P/L at all rather than a total missing a
/// term.
/// </para>
/// </summary>
internal readonly record struct OpenObservation(
    DateTime OpenTime,
    decimal OpenPrice,
    string Type,
    decimal? NetPl = null);

/// <summary>
/// Pairs a strategy's demo opens against one backtest run's opens by EXACT TRUNCATED MINUTE plus
/// direction, and aggregates the signed entry-price offset (demo minus backtest, raw price units —
/// design.md D7) per calendar month. Stateless and pure: no I/O, no <c>DbContext</c>, no
/// randomness, no seed (design.md "Technical Approach", D1, D7; spec Requirement "The Calculator
/// Is Deterministic").
/// <para>
/// <b>Exact-minute pairing is sound ONLY for measuring this offset.</b> It needs no tolerance rule
/// because the minute either matches or it does not; it MUST NOT be reused to merge, align, or
/// otherwise combine the two series for any other computation (spec Non-Goals; design.md interface
/// note).
/// </para>
/// <para>
/// <c>OpenTime</c> is never converted to another timezone on either side before pairing (D1) — the
/// measured 24-pair fixture depends on reading both series exactly as stored, with
/// <see cref="DateTimeKind"/> untouched. DST divergence is disclosed via
/// <see cref="MonthlyPriceOffsetDto.DstRisk"/>, never corrected (D2): a transition simply moves one
/// side's minute key, which surfaces as a collapse in that month's <c>PairedCount</c> rather than a
/// corrupted offset.
/// </para>
/// <para>
/// A minute where either side holds two or more opens is REFUSED, never resolved: those trades are
/// counted in that side's ambiguous-minute bucket and excluded from pairing entirely — set
/// membership, not ticket order or nearest-price matching, decides (D4; spec Requirement
/// "Same-Minute Duplicates On Either Side Are Refused And Counted, Never Resolved").
/// </para>
/// </summary>
internal static class DemoBacktestComparabilityCalculator
{
    public static PriceOffsetComparabilityDto Measure(
        Guid strategyId,
        BacktestRunKind kind,
        IReadOnlyList<OpenObservation> demoOpens,
        IReadOnlyList<OpenObservation> backtestOpens)
    {
        ArgumentNullException.ThrowIfNull(demoOpens);
        ArgumentNullException.ThrowIfNull(backtestOpens);

        var demoGroups = GroupByMinuteAndDirection(demoOpens);
        var backtestGroups = GroupByMinuteAndDirection(backtestOpens);

        var pairedCount = 0;
        var demoOnlyCount = 0;
        var backtestOnlyCount = 0;
        var ambiguousDemoCount = 0;
        var ambiguousBacktestCount = 0;
        var months = new Dictionary<(int Year, int Month), MonthAccumulator>();

        // One accumulator per DISJOINT subset. They are never added to one another: the subsets are
        // reported separately, and the demo and backtest sides do not even share a cost basis
        // (NetPlBasis). Nothing here feeds the offset computation.
        var pairedDemoNetPl = new NetPlAccumulator();
        var pairedBacktestNetPl = new NetPlAccumulator();
        var demoOnlyNetPl = new NetPlAccumulator();
        var backtestOnlyNetPl = new NetPlAccumulator();
        var ambiguousDemoNetPl = new NetPlAccumulator();
        var ambiguousBacktestNetPl = new NetPlAccumulator();

        foreach (var key in demoGroups.Keys.Union(backtestGroups.Keys))
        {
            var demoList = demoGroups.TryGetValue(key, out var demoAtKey) ? demoAtKey : [];
            var backtestList = backtestGroups.TryGetValue(key, out var backtestAtKey) ? backtestAtKey : [];

            var demoAmbiguous = demoList.Count >= 2;
            var backtestAmbiguous = backtestList.Count >= 2;

            if (demoAmbiguous)
            {
                ambiguousDemoCount += demoList.Count;
                ambiguousDemoNetPl.AddRange(demoList);
            }

            if (backtestAmbiguous)
            {
                ambiguousBacktestCount += backtestList.Count;
                ambiguousBacktestNetPl.AddRange(backtestList);
            }

            if (demoAmbiguous || backtestAmbiguous)
            {
                // Either side's ambiguity excludes the WHOLE minute from pairing. The non-ambiguous
                // side (if it holds exactly one open here) is unpaired, not ambiguous itself.
                if (!demoAmbiguous)
                {
                    demoOnlyCount += demoList.Count;
                    demoOnlyNetPl.AddRange(demoList);
                }

                if (!backtestAmbiguous)
                {
                    backtestOnlyCount += backtestList.Count;
                    backtestOnlyNetPl.AddRange(backtestList);
                }

                continue;
            }

            if (demoList.Count == 1 && backtestList.Count == 1)
            {
                var demoOpen = demoList[0];
                var backtestOpen = backtestList[0];
                var offset = demoOpen.OpenPrice - backtestOpen.OpenPrice;

                pairedCount++;
                pairedDemoNetPl.Add(demoOpen);
                pairedBacktestNetPl.Add(backtestOpen);

                var monthKey = (demoOpen.OpenTime.Year, demoOpen.OpenTime.Month);
                if (!months.TryGetValue(monthKey, out var accumulator))
                {
                    accumulator = new MonthAccumulator();
                    months[monthKey] = accumulator;
                }

                accumulator.Add(offset);
                continue;
            }

            // Exactly one side holds a (non-ambiguous) open at this minute; the other holds none.
            demoOnlyCount += demoList.Count;
            backtestOnlyCount += backtestList.Count;
            demoOnlyNetPl.AddRange(demoList);
            backtestOnlyNetPl.AddRange(backtestList);
        }

        var monthDtos = months
            .OrderBy(entry => entry.Key.Year)
            .ThenBy(entry => entry.Key.Month)
            .Select(entry => entry.Value.ToDto(entry.Key.Year, entry.Key.Month))
            .ToList();

        var status = pairedCount >= 1
            ? ComparabilityReadoutStatus.Measured
            : ComparabilityReadoutStatus.NoPairedOpens;

        return new PriceOffsetComparabilityDto(
            strategyId,
            kind,
            status,
            pairedCount,
            demoOnlyCount,
            backtestOnlyCount,
            ambiguousDemoCount,
            ambiguousBacktestCount,
            monthDtos,
            pairedDemoNetPl.Value,
            pairedBacktestNetPl.Value,
            demoOnlyNetPl.Value,
            backtestOnlyNetPl.Value,
            ambiguousDemoNetPl.Value,
            ambiguousBacktestNetPl.Value);
    }

    /// <summary>
    /// Groups opens by (truncated open minute, direction). Direction is normalized to upper
    /// invariant so the grouping is case-insensitive without a custom equality comparer — the
    /// underlying vocabularies ("buy"/"Buy", "sell"/"Sell") are plain ASCII.
    /// </summary>
    private static Dictionary<(long MinuteTicks, string Direction), List<OpenObservation>> GroupByMinuteAndDirection(
        IReadOnlyList<OpenObservation> opens)
    {
        var groups = new Dictionary<(long, string), List<OpenObservation>>();
        foreach (var open in opens)
        {
            var minuteTicks = open.OpenTime.Ticks - (open.OpenTime.Ticks % TimeSpan.TicksPerMinute);
            var key = (minuteTicks, open.Type.ToUpperInvariant());
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(open);
        }

        return groups;
    }

    /// <summary>
    /// The net P/L of ONE disjoint subset. Addition of <c>decimal</c> is exact and commutative, so
    /// the total does not depend on the order the observations arrive in — the readout stays
    /// byte-identical for identical inputs regardless of input order.
    /// <para>
    /// <see cref="Value"/> is <c>null</c> where the arithmetic is undefined rather than withheld:
    /// an empty subset has no net, and a subset holding an observation with no net P/L would
    /// otherwise publish a total silently missing a term.
    /// </para>
    /// </summary>
    private sealed class NetPlAccumulator
    {
        private decimal _sum;
        private bool _any;
        private bool _incomplete;

        public void Add(OpenObservation observation)
        {
            _any = true;
            if (observation.NetPl is null) _incomplete = true;
            else _sum += observation.NetPl.Value;
        }

        public void AddRange(IReadOnlyList<OpenObservation> observations)
        {
            foreach (var observation in observations) Add(observation);
        }

        public decimal? Value => _any && !_incomplete ? _sum : null;
    }

    private sealed class MonthAccumulator
    {
        private decimal _sum;
        private decimal? _min;
        private decimal? _max;
        private int _positive;
        private int _negative;
        private int _zero;
        private int _count;

        public void Add(decimal offset)
        {
            _sum += offset;
            _min = _min is null ? offset : Math.Min(_min.Value, offset);
            _max = _max is null ? offset : Math.Max(_max.Value, offset);
            _count++;

            if (offset > 0) _positive++;
            else if (offset < 0) _negative++;
            else _zero++;
        }

        public MonthlyPriceOffsetDto ToDto(int year, int month) => new(
            year,
            month,
            _count,
            _count == 0 ? null : _sum / _count,
            _min,
            _max,
            _positive,
            _negative,
            _zero);
    }
}
