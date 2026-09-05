using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One member of a backtest group risk analysis and what became of it (design.md D8a).
/// <para>
/// The row is present whether or not the member contributed a series, and it is the ONLY place a
/// refusal can be attributed. A group figure computed over "the members that worked" would silently
/// answer a different question from the one asked, so a refused member refuses the whole analysis —
/// and this row is what says which member and why.
/// </para>
/// </summary>
/// <param name="StrategyId">The requested member.</param>
/// <param name="Label">Its display name, so a refusal reads without a second lookup.</param>
/// <param name="Status">Whether it contributed a series, and if not, why not.</param>
/// <param name="Segment">The selected run's segment, or null when no run was selected.</param>
/// <param name="RunKind">Which slot the selected run occupied. Reported, never used to infer the segment.</param>
/// <param name="RunId">The selected run, or null.</param>
/// <param name="Detail">
/// The refusal in words, naming the run, the two ambiguous kinds, or the offending weight. Null
/// when <paramref name="Status"/> is <see cref="GroupRiskMemberStatus.Resolved"/>.
/// </param>
public sealed record GroupRiskMemberDto(
    Guid StrategyId,
    string Label,
    GroupRiskMemberStatus Status,
    BacktestSegment? Segment,
    BacktestRunKind? RunKind,
    Guid? RunId,
    string? Detail);

/// <summary>
/// The result of one backtest group risk analysis (design.md D4/D5/D6/D8).
/// <para>
/// <see cref="Risk"/> and <see cref="Correlation"/> are null unless <see cref="Status"/> is
/// <see cref="GroupRiskAnalysisStatus.Completed"/>. A refusal returns NO figures — not zeroed ones,
/// not partial ones — while still returning <see cref="Members"/>, because the evidence that caused
/// a refusal is exactly what the operator needs in order to fix it.
/// </para>
/// <para>
/// Inside a completed analysis every withheld VaR is <c>null</c> with a
/// <see cref="VarWithholdReason"/> beside it, never <c>0</c>. That is the whole point of the new
/// DTO family: the shipped <c>PortfolioRiskDto</c> cannot express it.
/// </para>
/// </summary>
/// <param name="Status">The one outcome. Every refusal carries its own value.</param>
/// <param name="Segment">The single sample label every figure was computed over, or null when none was reached.</param>
/// <param name="Members">One row per requested member, in request order.</param>
/// <param name="Risk">The group VaR readout, or null.</param>
/// <param name="Correlation">The group correlation matrix, or null.</param>
/// <param name="Refusal">The group-level refusal in words, or null when nothing was refused.</param>
public sealed record GroupRiskAnalysisDto(
    GroupRiskAnalysisStatus Status,
    BacktestSegment? Segment,
    IReadOnlyList<GroupRiskMemberDto> Members,
    BacktestPortfolioRiskDto? Risk,
    BacktestCorrelationDto? Correlation,
    string? Refusal);
