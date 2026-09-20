namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Slice B's own projection of one trade — demo or backtest — carrying the fields slice A's
/// <see cref="OpenObservation"/> cannot: <see cref="ClosePrice"/>, <see cref="Size"/> and
/// <see cref="Swap"/>, none of which slice A's <c>DemoOpensQuery</c>/<c>BacktestOpensQuery</c>
/// project (design D2 — <c>OpenObservation</c> is deliberately not extended).
/// <para>
/// <see cref="Size"/> is non-nullable <c>decimal</c> — verified against
/// <c>StrategyTrade.Size</c> and <c>BacktestTrade.Size</c>, both non-nullable on the entity.
/// </para>
/// <para>
/// <see cref="Swap"/> is nullable because only <c>StrategyTrade</c> has a <c>Swap</c> column;
/// every backtest-side instance carries <c>null</c>. <see cref="ClosePrice"/> is nullable to match
/// <c>StrategyTrade.ClosePrice</c> (null while a trade is still open); the backtest side's
/// non-nullable <c>ClosePrice</c> is projected into a non-null value.
/// </para>
/// </summary>
internal readonly record struct CostObservation(
    DateTime OpenTime,
    decimal OpenPrice,
    decimal? ClosePrice,
    decimal Size,
    string Type,
    decimal? NetPl,
    decimal? Swap);

/// <summary>
/// Maps <see cref="CostObservation"/> to slice A's <see cref="OpenObservation"/> for calls into
/// slice A's unchanged <see cref="DemoBacktestComparabilityCalculator.Measure"/> (design D3).
/// Preserves <c>OpenTime</c>, <c>OpenPrice</c>, <c>Type</c> and <c>NetPl</c> verbatim, dropping
/// <c>ClosePrice</c>/<c>Size</c>/<c>Swap</c> only for this call — nothing is mutated on the source.
/// </summary>
internal static class CostObservationMapping
{
    public static OpenObservation ToOpenObservation(this CostObservation observation)
        => new(observation.OpenTime, observation.OpenPrice, observation.Type, observation.NetPl);

    public static IReadOnlyList<OpenObservation> ToOpenObservations(this IReadOnlyList<CostObservation> observations)
        => observations.Select(o => o.ToOpenObservation()).ToList();
}
