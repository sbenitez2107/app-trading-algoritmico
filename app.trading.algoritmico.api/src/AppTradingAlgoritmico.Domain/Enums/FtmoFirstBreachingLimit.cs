namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Which limit's first breach occurred earliest, per run (spec.md "Per-Run First-Limit-To-Break").
/// <see cref="BothSameClose"/> is reported when the daily limit's and the max limit's first breaching
/// closes are the same underlying row — sameness is detected by row identity, never by comparing
/// timestamps alone (design.md Decision 7 / spec.md "Identical timestamps on different rows are not
/// treated as a tie").
/// </summary>
public enum FtmoFirstBreachingLimit
{
    /// <summary>The daily-loss limit's first breach occurred earliest.</summary>
    Daily = 0,

    /// <summary>The max-loss limit's first breach occurred earliest.</summary>
    Max = 1,

    /// <summary>The daily and max limits' first breaching closes are the same underlying row.</summary>
    BothSameClose = 2,
}
