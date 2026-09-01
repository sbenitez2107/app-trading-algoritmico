namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// What a strategy's imported backtest evidence is able to support. ALWAYS derived, never stored
/// and never user-settable: there is no column a user could flip, so overfitting cannot re-enter
/// through a silent "include me anyway" toggle (design.md D14). A stored copy would also be
/// unmaintainable — deleting a strategy or a run happens in the database, where no application
/// code runs to recompute it.
/// </summary>
public enum BacktestReadiness
{
    /// <summary>No run at all. White.</summary>
    None = 0,

    /// <summary>
    /// A run exists, but the strategy is not evaluable: no Evaluation run, or no walk-forward
    /// export, or no trade at/after the boundary. Sizing is available; an out-of-sample claim is
    /// not. Amber.
    /// </summary>
    SizingOnly = 1,

    /// <summary>Evaluation run + walk-forward export + at least one trade at/after the boundary. Green.</summary>
    Evaluable = 2,
}
