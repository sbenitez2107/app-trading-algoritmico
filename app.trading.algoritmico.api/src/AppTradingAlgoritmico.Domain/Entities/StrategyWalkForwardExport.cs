using AppTradingAlgoritmico.Domain.Common;

namespace AppTradingAlgoritmico.Domain.Entities;

/// <summary>
/// One SQX Optimizer "Walk-Forward Results" export, at most one per <see cref="Strategy"/>.
/// <para>
/// This entity OWNS <see cref="OosFromDate"/>. The boundary is never copied onto a
/// <see cref="BacktestRun"/> or a <see cref="BacktestTrade"/>: a value owned by A and copied onto
/// B cannot observe A changing, so re-importing a newer export would leave every run pinned to the
/// stale boundary. Because it is owned here and read through <c>OosWindow.Resolver</c>, a run
/// imported BEFORE its export needs no re-import when the export finally arrives — the boundary
/// simply becomes obtainable. See design.md D10.
/// </para>
/// </summary>
public class StrategyWalkForwardExport : BaseEntity
{
    /// <summary>The owning strategy. Unique — one export per strategy — and cascade-deleted.</summary>
    public Guid StrategyId { get; set; }

    /// <summary>
    /// OOS start of the SECOND-TO-LAST window: the last window whose out-of-sample period has
    /// actually elapsed. Positional because the user's process is positional — the deployed
    /// parameters come from the last row, the evaluation parameters from the one before it.
    /// </summary>
    public DateTime OosFromDate { get; set; }

    /// <summary>Verbatim <c>Parameters</c> text of the LAST row — the parameters currently deployed.</summary>
    public required string DeployParameters { get; set; }

    /// <summary>Verbatim <c>Parameters</c> text of the SECOND-TO-LAST row — what an Evaluation run should have been produced from.</summary>
    public required string EvaluationParameters { get; set; }

    /// <summary>
    /// SHA-256 over the raw file bytes, lowercase hex. Audit only: a WF export is replaced
    /// wholesale on re-import, so the hash decides nothing.
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>Sanitized (<see cref="Path.GetFileName(string)"/>) original filename. Display/audit only.</summary>
    public required string SourceFileName { get; set; }

    public ICollection<WalkForwardWindow> Windows { get; init; } = [];
}
