namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Which FX-band end produced a merged first-breach timing point (design.md Decision 6, spec.md
/// "FX-Band Timing Reports The Earliest Point And Names The Producing End"). <see cref="BothEnds"/>
/// covers both a genuine same-row tie between <c>fxLow</c> and <c>fxHigh</c> and a same-currency
/// symbol's single evaluation run (identity band, both ends coincide).
/// </summary>
public enum FtmoFxBandEnd
{
    /// <summary>The declared FX band's low end produced the earliest point.</summary>
    FxLow = 0,

    /// <summary>The declared FX band's high end produced the earliest point.</summary>
    FxHigh = 1,

    /// <summary>Both ends' earliest breaching closes are the same underlying row (or a same-currency symbol's identity band).</summary>
    BothEnds = 2,
}
