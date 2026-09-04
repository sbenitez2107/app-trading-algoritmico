using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>One dated net: the P/L a resized row would have realized, on the day it closed.</summary>
/// <param name="When">The source trade's <c>CloseTime</c>, carried through unchanged.</param>
/// <param name="Net">
/// <c>Profit * (ResizedSize / OriginalSize)</c> — GROSS of every unmodelled cost. There is no
/// commission, swap or tax column on <c>BacktestTrade</c>, so this net cannot be net of them.
/// </param>
public readonly record struct DatedNet(DateTime When, decimal Net);

/// <summary>
/// A backtest run re-expressed as a dated net series a portfolio-analytics consumer can bind to
/// (design.md D1/D2/D3).
/// <para>
/// <b>POSSESSION IS PROOF THE WEIGHT WAS CHECKED.</b> The constructor is private and the ONLY way
/// to obtain an instance is <see cref="Bridge"/>, nested in this same program text, which takes a
/// REQUIRED <c>memberWeight</c> and refuses anything but <c>1</c>. Slice 2a could only state that
/// refusal as a convention on a future consumer; here it is a fact about the type system. It is a
/// sealed CLASS rather than a record struct for <c>OosWindow</c>'s exact reason: a struct has a
/// <c>default</c> instance no matter how private its constructor.
/// </para>
/// <para>
/// <b>WHAT DOES NOT SURVIVE, STATED PLAINLY.</b> Slice 2a's D9 made <c>w * NetOf(t)</c> a COMPILE
/// ERROR, because <see cref="ResizedTrade"/> has no cost fields to bind to. That does not carry
/// over: <c>w * series.Nets[i].Net</c> compiles, because <see cref="Nets"/> is a projection of
/// bare <see cref="decimal"/>. So the REFUSAL is structural and the IMMUNITY to post-hoc
/// multiplication is only convention. Two mitigations, neither a type-system guarantee: the
/// analytics adapters take this sealed type rather than a bare tuple list, so a hand-scaled
/// projection cannot be passed INTO the analytics; and a reflection test asserts this type exposes
/// no scaling member.
/// </para>
/// <para>
/// It deliberately carries NO density. Density is measured once, in Infrastructure, by the code
/// that gates on it, so the count that withheld a figure is the same count reported beside it
/// (D4). The two TRADE-level counts here are the bridge's own and have no other source.
/// </para>
/// </summary>
public sealed class BacktestNetSeries
{
    private BacktestNetSeries(
        Guid strategyId,
        string label,
        string? fundingService,
        BacktestSegment segment,
        decimal targetRiskPerTrade,
        IReadOnlyList<DatedNet> nets,
        int tradeCount,
        int excludedUnscalableCount)
    {
        StrategyId = strategyId;
        Label = label;
        FundingService = fundingService;
        Segment = segment;
        TargetRiskPerTrade = targetRiskPerTrade;
        Nets = nets;
        TradeCount = tradeCount;
        ExcludedUnscalableCount = excludedUnscalableCount;
    }

    public Guid StrategyId { get; }

    /// <summary>The member's display name — what a refusal or a matrix axis names.</summary>
    public string Label { get; }

    /// <summary>The funding service (broker) this member is deployed under, or null.</summary>
    public string? FundingService { get; }

    /// <summary>
    /// Run-level metadata stating WHICH sample every figure was computed over. Never a filter: the
    /// importer rejects a file carrying more than one <c>Sample type</c>, so a run's trades are
    /// wholly one segment and there is nothing to partition (D8).
    /// </summary>
    public BacktestSegment Segment { get; }

    /// <summary>The risk per trade the series was sized at. This IS the sizing decision.</summary>
    public decimal TargetRiskPerTrade { get; }

    /// <summary>Chronological, one entry per SCALABLE resized row.</summary>
    public IReadOnlyList<DatedNet> Nets { get; }

    /// <summary>
    /// Resized rows offered to the bridge — the denominator of the disclosure. Bridge-sourced: a
    /// day-level density measurement cannot recover it, because many trades collapse into one
    /// calendar day.
    /// </summary>
    public int TradeCount { get; }

    /// <summary>
    /// Rows the resizer marked <see cref="ResizeOutcome.Unscalable"/> and this bridge therefore
    /// EXCLUDED — never contributed as a <c>0</c>, because a zero net is a breakeven trade, a
    /// different claim. Satisfies <c>TradeCount - ExcludedUnscalableCount == Nets.Count</c>.
    /// </summary>
    public int ExcludedUnscalableCount { get; }

    /// <summary>
    /// The only factory. Nested here so the private constructor is reachable from one program text
    /// (the <c>OosWindow</c> inverted-nesting trick, which slice 2a could not use because
    /// <c>ResizedTradeSeries</c> and its producer live in different assemblies — these do not).
    /// </summary>
    public static class Bridge
    {
        /// <summary>
        /// Pairs each resized row against the source trade sharing its <c>RowIndex</c> — a
        /// dictionary LOOKUP, never a positional or count comparison — and projects
        /// <c>Profit * (ResizedSize / OriginalSize)</c> onto the source row's close date.
        /// <para>
        /// The two contracts here are deliberately DIFFERENT mechanisms. A pairing failure means
        /// the caller wired two unrelated lists and nothing downstream can do anything useful with
        /// the result, so it THROWS. A non-unit weight is a legitimate data condition the caller
        /// must inspect and report, so it is a STATUS. Implementing them the same way would let one
        /// be handled as the other.
        /// </para>
        /// <para>
        /// Pairing is validated BEFORE the weight, so a wiring bug is never masked by a data
        /// refusal that would have hidden it.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// A resized <c>RowIndex</c> with no source match, or a <c>RowIndex</c> appearing more than
        /// once in <paramref name="source"/> (the concatenated-runs wiring error).
        /// </exception>
        public static BacktestNetSeriesResult Build(
            IReadOnlyList<BacktestTrade> source,
            ResizedTradeSeries resized,
            Guid strategyId,
            string label,
            string? fundingService,
            BacktestSegment segment,
            decimal memberWeight)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(resized);

            var sourceByRowIndex = new Dictionary<int, BacktestTrade>(source.Count);
            foreach (var trade in source)
            {
                if (!sourceByRowIndex.TryAdd(trade.RowIndex, trade))
                {
                    throw new ArgumentException(
                        $"RowIndex {trade.RowIndex} appears more than once in the held source list. "
                        + "Slice 1's unique (BacktestRunId, RowIndex) index makes this unreachable from one "
                        + "run, so the caller has concatenated rows from two different runs.",
                        nameof(source));
                }
            }

            var paired = new List<(BacktestTrade Source, ResizedTrade Resized)>(resized.Trades.Count);
            foreach (var row in resized.Trades)
            {
                if (!sourceByRowIndex.TryGetValue(row.RowIndex, out var match))
                {
                    throw new ArgumentException(
                        $"Resized RowIndex {row.RowIndex} has no source trade in the held list. "
                        + "The two lists do not describe the same run.",
                        nameof(resized));
                }

                paired.Add((match, row));
            }

            // Checked AFTER pairing and BEFORE any net is computed: a refused member must not leave
            // a partially-built series anywhere.
            if (memberWeight != 1m)
            {
                return new BacktestNetSeriesResult(
                    BacktestNetSeriesStatus.NonUnitWeight, Series: null, strategyId, label, memberWeight);
            }

            var nets = new List<DatedNet>(paired.Count);
            var excluded = 0;
            foreach (var (trade, row) in paired)
            {
                if (row.Outcome == ResizeOutcome.Unscalable)
                {
                    // No net AT ALL. Contributing 0 here would assert a breakeven trade.
                    excluded++;
                    continue;
                }

                nets.Add(new DatedNet(trade.CloseTime, trade.Profit * (row.ResizedSize / row.OriginalSize)));
            }

            nets.Sort(static (a, b) => a.When.CompareTo(b.When));

            var series = new BacktestNetSeries(
                strategyId, label, fundingService, segment, resized.TargetRiskPerTrade,
                nets, resized.Trades.Count, excluded);

            return new BacktestNetSeriesResult(
                BacktestNetSeriesStatus.Built, series, strategyId, label, memberWeight);
        }

        /// <summary>
        /// <c>false</c> means <see cref="BacktestNetSeriesStatus.Built"/> was not reached and no
        /// series exists for that member. The nullable <c>out</c> is deliberate: ignoring the
        /// <c>bool</c> and dereferencing the result is a CS8602 warning, which this solution builds
        /// with <c>-warnaserror</c>.
        /// </summary>
        public static bool TryBuild(
            IReadOnlyList<BacktestTrade> source,
            ResizedTradeSeries resized,
            Guid strategyId,
            string label,
            string? fundingService,
            BacktestSegment segment,
            decimal memberWeight,
            out BacktestNetSeries? series)
        {
            var result = Build(source, resized, strategyId, label, fundingService, segment, memberWeight);
            series = result.Series;
            return result.Status == BacktestNetSeriesStatus.Built;
        }
    }
}
