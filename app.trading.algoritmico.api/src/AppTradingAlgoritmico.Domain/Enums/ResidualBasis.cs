namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Non-nullable, non-droppable disclosure of the execution residual's claim boundary (design D6,
/// D10; spec "The Execution Residual Is Computed Over The Paired Subset Only"). The single member
/// states, on the type, that the residual is the exact-minute-paired subset's demo-versus-backtest
/// difference AFTER swap and embedded cost are removed. It is NOT slippage, NOT a strategy-quality
/// score, and does NOT account for the M1-versus-tick intrabar path assumption (an independent
/// modelling gap recorded in slice A's Non-Goals). Demo-only and backtest-only P/L are never folded
/// into it under any circumstance (design D10 — rejected: residual over the full disjoint partition).
/// </summary>
public enum ResidualBasis
{
    PairedSubsetAfterSwapAndEmbeddedCost = 0,
}
