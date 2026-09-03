using AppTradingAlgoritmico.Domain.Backtests;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// A whole run re-expressed at one risk per trade, on one grid (design.md D8/D9).
/// <para>
/// <b>It is ALREADY SIZED.</b> It carries its own <see cref="TargetRiskPerTrade"/> precisely so a
/// consumer never has to infer the sizing from somewhere else — and so that multiplying it by a
/// <c>PortfolioStrategy.Weight</c> is visibly double-sizing rather than a plausible-looking scale.
/// </para>
/// <para>
/// THREE FACTS, in descending strength. (1) STRUCTURAL: this is a sealed record, not an
/// <c>IReadOnlyList&lt;StrategyTrade&gt;</c>, and <c>PortfolioMemberInput.Trades</c> is hard-typed —
/// passing a series is a compile error. There is no conversion, no implicit operator, and no
/// <c>ToStrategyTrades()</c>; this slice adds none and a test asserts their absence by reflection.
/// (2) STRUCTURAL: <see cref="ResizedTrade"/> carries no cost fields and no entity base, so the
/// analytics net-of-costs path cannot bind either. (3) CONVENTION, and named as one: a future
/// consumer that DOES accept this series must refuse a <c>Weight != 1</c> rather than multiply it.
/// That third one is an obligation on a later slice, not a fact about the type system, and it is
/// stated as such.
/// </para>
/// <para>
/// The series is never REFUSED for clamping. Clamping is legitimate and often unavoidable; what
/// would be illegitimate is hiding it. Hence the four counts and
/// <see cref="MaxAchievedRisk"/> — and <see cref="UnknownAchievedRiskCount"/>, without which
/// <see cref="MaxAchievedRisk"/> would read as a ceiling it cannot promise. No clamp-fraction
/// threshold is defended, because nothing measured supports one.
/// </para>
/// </summary>
/// <param name="TargetRiskPerTrade">The risk per trade the operator asked for. Asked for — not necessarily achieved.</param>
/// <param name="Grid">The grid the sizes were floored onto.</param>
/// <param name="Trades">One row per trade of the source profile, in the same order.</param>
/// <param name="OnTargetCount">Trades the grid could express: achieved risk at or below target, within one step.</param>
/// <param name="RaisedToMinimumCount">Trades pinned UP to the minimum lot. These are OVER-risked against the target.</param>
/// <param name="CappedAtMaximumCount">Trades capped DOWN at the ceiling. These are under-risked.</param>
/// <param name="MaxAchievedRisk">
/// Largest KNOWN achieved risk across the series, or null when no trade has a bounded upper
/// endpoint. Read it together with <see cref="UnknownAchievedRiskCount"/>: it is the worst case
/// among the trades whose worst case is known, not the worst case of the series.
/// </param>
/// <param name="UnknownAchievedRiskCount">
/// Trades whose achieved risk has NO upper endpoint — an <c>Unbounded</c> minimum-lot pin, or a
/// trade with no usable size. Every one of these is a hole in <see cref="MaxAchievedRisk"/>.
/// </param>
/// <param name="UnscalableCount">
/// Trades that could not be scaled at all because their original <c>Size</c> is zero or negative.
/// Deliberately NOT folded into <see cref="RaisedToMinimumCount"/>: that count asserts the row is
/// over-risked, which cannot be claimed about a row whose achieved risk is unknown. Together with
/// the three clamp counts this partitions every row exactly once.
/// </param>
public sealed record ResizedTradeSeries(
    decimal TargetRiskPerTrade,
    LotGrid Grid,
    IReadOnlyList<ResizedTrade> Trades,
    int OnTargetCount,
    int RaisedToMinimumCount,
    int CappedAtMaximumCount,
    decimal? MaxAchievedRisk,
    int UnknownAchievedRiskCount,
    int UnscalableCount);
