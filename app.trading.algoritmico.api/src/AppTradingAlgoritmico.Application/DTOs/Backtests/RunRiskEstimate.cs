using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One run's own risk-per-trade estimate and the evidence behind it (design.md D1/D4/D11).
/// <para>
/// Shaped like <see cref="SymbolCalibrationDto"/> on purpose: the estimate is ALWAYS returned, both
/// fractions are ALWAYS populated, and only <see cref="RiskPerTrade"/> is withheld when
/// <see cref="Status"/> is not <see cref="RunRiskEstimateStatus.Estimated"/>. The evidence that
/// caused a refusal is exactly what the operator needs to see, so it must survive the refusal.
/// </para>
/// </summary>
/// <param name="Status">Whether the run produced a usable <c>Â</c> at all.</param>
/// <param name="RiskPerTrade">
/// <c>Â</c>, the amount the run appears to have risked per trade — measured from its own SL closes,
/// NEVER seeded from a configured amount. Null unless <see cref="Status"/> is
/// <see cref="RunRiskEstimateStatus.Estimated"/>.
/// </param>
/// <param name="ConsistencyFraction">
/// Share of usable SL closes whose feasible band contains <see cref="RiskPerTrade"/>. THIS is the
/// gate: below <c>TradeRiskNormalizer.MinimumConsistencyFraction</c> the run refuses.
/// </param>
/// <param name="MinLotPinnedFraction">
/// Share of ALL the run's trades whose <c>Size</c> sits at <c>LotGrid.MinLot</c>. Reported, never
/// gating (D11). It answers a different question from consistency — whether the GRID can express the
/// target, not whether the sizing MODEL fits — and the two separate in opposite directions: the
/// coarse export scores 93% consistency (a pass) against 33.8% pinning, the fine one 100% against
/// 0.3%. Folding them into one gate would reject a coarse grid for the wrong reason.
/// </param>
/// <param name="SlSampleCount">Usable SL closes the estimate was drawn from.</param>
public sealed record RunRiskEstimate(
    RunRiskEstimateStatus Status,
    decimal? RiskPerTrade,
    decimal ConsistencyFraction,
    decimal MinLotPinnedFraction,
    int SlSampleCount);
