using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One trade re-expressed on the lot grid at an operator-chosen risk per trade (design.md D7/D8).
/// <para>
/// It deliberately carries NO <c>Commission</c>, <c>Swap</c> or <c>Taxes</c> and does NOT inherit
/// <c>BaseEntity</c>. That is not an oversight — it is the second structural fact behind D9: without
/// those members <c>AnalyticsSeries.NetOf(StrategyTrade)</c> cannot bind to it, so
/// <c>weight * NetOf(t)</c> does not compile against an already-sized trade.
/// </para>
/// </summary>
/// <param name="RowIndex">0-based ordinal within the source file, carried through unchanged.</param>
/// <param name="Ticket">Informational only.</param>
/// <param name="OriginalSize">The size the backtest actually traded.</param>
/// <param name="ResizedSize">The size the target implies AFTER flooring onto the grid and clamping.</param>
/// <param name="AchievedRisk">
/// What the resized size actually risks: the trade's OWN band from the normalizer, scaled by
/// <c>ResizedSize / OriginalSize</c>. It is never recomputed from <c>Â</c>, which would discard the
/// exact measured value on an SL close, and it is never assumed to equal the target.
/// </param>
/// <param name="Outcome">Whether the grid could express the target for this trade at all.</param>
/// <param name="Basis">The provenance carried over from the normalizer — an achieved band inherits it.</param>
public sealed record ResizedTrade(
    int RowIndex,
    long Ticket,
    decimal OriginalSize,
    decimal ResizedSize,
    TradeRiskInterval AchievedRisk,
    ResizeOutcome Outcome,
    RiskBasis Basis);
