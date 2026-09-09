using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Divergence;

/// <summary>
/// The demo-vs-backtest entry-price offset for one strategy and one caller-named
/// <see cref="BacktestRunKind"/> slot, measured over exact-minute-paired opens only
/// (`demo-backtest-comparability` spec). Gates the future <c>demo-backtest-cost-decomposition</c>
/// slice — decomposing cost between two price series that do not first pass this check is
/// arithmetic dressed as signal.
/// <para>
/// <b>This readout is TRANSITIONAL, not a permanent verdict.</b> It reflects the CURRENT
/// data-symbol binding (measured: <c>USATECHIDXUSD_M1_UTC02</c> bound to <c>NDX_DARWINEX</c>) and
/// is expected to change — including to stop reproducing at all — once the underlying price series
/// is rebound to a comparably-sourced instrument. It MUST NOT be read as a stable or permanent
/// property of the strategy.
/// </para>
/// <para>
/// <see cref="Basis"/> is a COMPUTED, non-nullable property, mirroring
/// <c>ServiceGuardrailDto.BreachBasis</c>: no call site can construct this readout without
/// disclosing that its figures are scoped to the paired subset (design.md D6).
/// </para>
/// <para>
/// <b>Each disjoint subset's net P/L is reported separately</b>, never folded into the offset and
/// never summed into one figure (spec Requirement "The Trade-Set Difference Is Reported As A Set
/// Relationship, Never As A Score"). The offset describes the PAIRED subset only, so without these
/// figures a reader cannot tell what share of the value that subset covers: an offset measured over
/// pairs worth 50 while the unpaired demo trades are worth 500 describes a tenth of the P/L.
/// A figure is <c>null</c> only where the arithmetic is undefined — a net over an empty subset, or
/// one whose observations carry no net P/L at all — never as a withheld value.
/// </para>
/// <para>
/// <b>The two sides' figures are NOT comparable and MUST NOT be subtracted, ranked, or read as a
/// winner</b> — see <see cref="DemoNetPlBasis"/> and <see cref="BacktestNetPlBasis"/>, which state
/// the two different cost bases, and note that each side's value is in ITS OWN deposit currency, as
/// stored, with no conversion performed anywhere in this slice.
/// </para>
/// </summary>
public sealed record PriceOffsetComparabilityDto(
    Guid StrategyId,
    BacktestRunKind Kind,
    ComparabilityReadoutStatus Status,
    int PairedCount,
    int DemoOnlyCount,
    int BacktestOnlyCount,
    int AmbiguousMinuteDemoCount,
    int AmbiguousMinuteBacktestCount,
    IReadOnlyList<MonthlyPriceOffsetDto> Months,
    decimal? PairedDemoNetPl = null,
    decimal? PairedBacktestNetPl = null,
    decimal? DemoOnlyNetPl = null,
    decimal? BacktestOnlyNetPl = null,
    decimal? AmbiguousMinuteDemoNetPl = null,
    decimal? AmbiguousMinuteBacktestNetPl = null)
{
    /// <summary>
    /// Non-nullable, non-droppable disclosure: every OFFSET figure on this readout — mean, min,
    /// max, sign consistency — is measured over the exact-minute-paired subset only, never over
    /// the full demo or backtest trade set. It says nothing about how a net P/L is computed; that
    /// is <see cref="DemoNetPlBasis"/> / <see cref="BacktestNetPlBasis"/>, deliberately separate so
    /// neither disclosure blurs the other.
    /// </summary>
    public ComparabilityBasis Basis => ComparabilityBasis.PairedOpensOnly;

    /// <summary>
    /// Non-nullable, non-droppable disclosure of how every demo-side net P/L on this readout
    /// (<see cref="PairedDemoNetPl"/>, <see cref="DemoOnlyNetPl"/>,
    /// <see cref="AmbiguousMinuteDemoNetPl"/>) is computed.
    /// </summary>
    public NetPlBasis DemoNetPlBasis => NetPlBasis.DemoProfitPlusCommissionSwapTaxes;

    /// <summary>
    /// Non-nullable, non-droppable disclosure of how every backtest-side net P/L on this readout
    /// (<see cref="PairedBacktestNetPl"/>, <see cref="BacktestOnlyNetPl"/>,
    /// <see cref="AmbiguousMinuteBacktestNetPl"/>) is computed. It differs from
    /// <see cref="DemoNetPlBasis"/>, and that difference is the point: the two sides' figures are
    /// reported side by side because each subset needs its own value, NOT so that one can be
    /// subtracted from or ranked against the other.
    /// </summary>
    public NetPlBasis BacktestNetPlBasis => NetPlBasis.BacktestProfitInclusiveOfCommissionAndSpreadExcludingSwap;
}

/// <summary>
/// One calendar month's exact-minute-paired offset. Only reported for months with
/// <see cref="PairedCount"/> &gt;= 1 — this slice publishes no invented minimum below which a
/// month's figures are withheld (spec Requirement "Figures Publish At Any Paired Count Above
/// Zero"). <see cref="MeanOffsetPriceUnits"/>, <see cref="MinOffsetPriceUnits"/> and
/// <see cref="MaxOffsetPriceUnits"/> are RAW PRICE UNITS (demo entry price minus backtest entry
/// price) — never "points", because no instrument-specification table is read anywhere in this
/// slice and no tick-size conversion is performed; naming the fields "points" would assert a
/// conversion the code does not do (design.md D7).
/// </summary>
public sealed record MonthlyPriceOffsetDto(
    int Year,
    int Month,
    int PairedCount,
    decimal? MeanOffsetPriceUnits,
    decimal? MinOffsetPriceUnits,
    decimal? MaxOffsetPriceUnits,
    int PositiveCount,
    int NegativeCount,
    int ZeroCount)
{
    /// <summary>
    /// Discloses that this calendar month MAY carry a US/broker DST transition — a deliberately
    /// over-inclusive calendar rule (<c>Month is 3 or 10 or 11</c>), never a tzdata lookup. A
    /// transition does not corrupt <see cref="MeanOffsetPriceUnits"/>; it shifts one side's minute
    /// key, so affected trades simply stop pairing — the symptom is a collapse in
    /// <see cref="PairedCount"/> for that month (design.md D2).
    /// </summary>
    public DstTransitionRisk DstRisk =>
        Month is 3 or 10 or 11 ? DstTransitionRisk.TransitionMonth : DstTransitionRisk.None;

    /// <summary>
    /// The share of this month's paired offsets sharing the sign of the majority. Reported, and
    /// never framed as a directional performance claim — a paired-trade sample of this size cannot
    /// establish that one series outperforms the other (spec Requirement "Sign Consistency Is
    /// Reported, And No Directional Performance Claim Is Made"). <c>null</c> only when
    /// <see cref="PairedCount"/> is <c>0</c>, which this type never emits (see class remarks).
    /// </summary>
    public decimal? SignConsistency => PairedCount == 0
        ? null
        : (decimal)Math.Max(PositiveCount, NegativeCount) / PairedCount;
}
