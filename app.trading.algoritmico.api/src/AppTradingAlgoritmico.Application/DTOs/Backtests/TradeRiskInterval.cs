namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// The dollar risk one trade could have carried, as a BAND (design.md D6).
/// <para>
/// <b>There is deliberately no scalar accessor.</b> No <c>Value</c>, no <c>Midpoint</c>, no
/// <c>Mean</c>, no implicit conversion to <see cref="decimal"/> — no member at all that yields a
/// bare number. The rejected alternative was a point estimate carrying a <c>Basis</c> tag beside
/// it, and it was rejected for one reason: a bare number next to an enum gets read as the number.
/// A consumer that wants a single figure has to write the collapse itself, at which point it is
/// visibly the consumer's judgement rather than this type's claim.
/// </para>
/// <para>
/// A null endpoint means genuinely open, not zero, and never a default: <see cref="Low"/> null is
/// unbounded BELOW, <see cref="High"/> null is unbounded ABOVE, and both null is
/// <c>RiskBasis.Unavailable</c>.
/// </para>
/// <para>
/// CONSUMER CONTRACT. MAY: render both endpoints; derive R bounds (remembering that they SWAP when
/// <c>Profit &lt; 0</c>); count trades by basis; test two bands for overlap.
/// MAY NOT: collapse to a point; average intervals; SUM endpoints across trades (every trade shares
/// the same <c>Â</c>, so the errors are dependent and interval arithmetic overstates the spread);
/// rank on an imputed or unbounded R without carrying its basis; divide by a null endpoint.
/// </para>
/// </summary>
/// <param name="Low">Lower endpoint, or null when risk is unbounded below.</param>
/// <param name="High">Upper endpoint, or null when risk is unbounded above.</param>
public sealed record TradeRiskInterval(decimal? Low, decimal? High)
{
    /// <summary>Both endpoints open — nothing is known about this trade's risk.</summary>
    public static TradeRiskInterval Unknown { get; } = new(null, null);

    /// <summary>A measurement: a band whose endpoints coincide.</summary>
    public static TradeRiskInterval Point(decimal value) => new(value, value);
}
