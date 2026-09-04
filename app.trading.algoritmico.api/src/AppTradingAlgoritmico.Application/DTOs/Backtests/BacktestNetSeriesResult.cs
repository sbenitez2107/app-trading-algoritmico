using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// The outcome of a <see cref="BacktestNetSeries.Bridge"/> conversion, with the evidence that
/// survives a refusal (design.md D3 — slice 2a's <c>RunRiskEstimate</c> shape, not
/// <c>OosWindow</c>'s "no object at all": the caller has to be able to NAME what it refused).
/// <para>
/// <see cref="Series"/> is null unless <see cref="Status"/> is
/// <see cref="BacktestNetSeriesStatus.Built"/>. A refused member yields no series — not an empty
/// one, not a flagged one that is nonetheless aggregable.
/// </para>
/// </summary>
/// <param name="Status">Why there is, or is not, a series.</param>
/// <param name="Series">The dated series, or null.</param>
/// <param name="StrategyId">The member the conversion was attempted for.</param>
/// <param name="Label">The member's display name, so a refusal can be reported without a lookup.</param>
/// <param name="OfferedWeight">
/// The weight the caller offered, echoed verbatim. Reported so the refusal states the offending
/// value rather than merely that one was offered.
/// </param>
public sealed record BacktestNetSeriesResult(
    BacktestNetSeriesStatus Status,
    BacktestNetSeries? Series,
    Guid StrategyId,
    string Label,
    decimal OfferedWeight);
