namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Provenance of one trade's dollar risk (design.md D5). The label is the whole point: a
/// non-<see cref="Measured"/> risk is an interval and MUST NOT be rendered as a bare number.
/// <para>
/// Assignment precedence is <see cref="Measured"/> &gt; <see cref="Unavailable"/> &gt;
/// <see cref="Unbounded"/> &gt; <see cref="Imputed"/> — declaration order below is the reading
/// order of the design table, not the precedence.
/// </para>
/// </summary>
public enum RiskBasis
{
    /// <summary><c>CloseType == "SL"</c> with a present <c>RealizedRisk</c>. Interval is the point <c>[r, r]</c>.</summary>
    Measured = 0,

    /// <summary>
    /// Non-SL exit strictly inside the grid. The trade was sized from its initial stop, so the run's
    /// <c>Â</c> is recovered rather than guessed — but only as the band <c>(Â·q/(q+step), Â]</c>.
    /// </summary>
    Imputed,

    /// <summary>
    /// Non-SL exit sitting on a grid edge, where one side of the band is open. At <c>MinLot</c> a
    /// legitimate floor and a clamp UP are indistinguishable, so risk is unbounded ABOVE; at
    /// <c>MaxLots</c> it is unbounded BELOW.
    /// </summary>
    Unbounded,

    /// <summary><c>Size &lt;= 0</c>, or an SL row whose <c>RealizedRisk</c> is null or zero. Both endpoints null.</summary>
    Unavailable,
}
