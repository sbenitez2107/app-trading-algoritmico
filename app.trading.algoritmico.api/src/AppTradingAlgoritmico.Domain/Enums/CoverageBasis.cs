namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Non-nullable, non-droppable disclosure that every coverage-gap label on a cost-decomposition
/// readout is a PRESUMPTION drawn from the absence of backtest trades, never a confirmed finding.
/// The single member covers both the one-sided cases (a month with demo trades and no backtest
/// trades, or vice versa) and the zero-trades-both-sides case: a month with no trades on either
/// side cannot be distinguished from "the strategy did not signal that month" — the only
/// independent corroboration, SQX Data Manager's gap percentage, has no ingestion path anywhere in
/// the codebase (GUI-only, verified) and is out of scope. See spec "Data Coverage Discloses Itself
/// As A Presumption From Absence, Never As A Proof"; design D2, D11.
/// </summary>
public enum CoverageBasis
{
    PresumedFromBacktestTradeAbsence = 0,
}
