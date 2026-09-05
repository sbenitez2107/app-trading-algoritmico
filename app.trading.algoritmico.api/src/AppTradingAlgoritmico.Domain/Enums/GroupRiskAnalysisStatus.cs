namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// The outcome of one backtest group risk analysis (design.md D8/D8a/D8b).
/// <para>
/// Every refusal carries its OWN value. Collapsing them would make "you did not say which sample"
/// read the same as "this member's two runs both carry it", and the operator's next action differs
/// in every case. The two <c>Unknown</c>-related refusals in particular are DIFFERENT RULES that
/// happen to reach the same place, and they stay distinct here for that reason.
/// </para>
/// </summary>
public enum GroupRiskAnalysisStatus
{
    /// <summary>Every member resolved and the figures were produced.</summary>
    Completed = 0,

    /// <summary>
    /// The request carried no segment. The field is <c>BacktestSegment?</c> precisely so this is
    /// expressible: on a non-nullable enum an omitted JSON property binds to <c>0</c>, which IS
    /// <see cref="BacktestSegment.Unknown"/>, and "required input" would be unsatisfiable as typed.
    /// </summary>
    SegmentNotSpecified,

    /// <summary>
    /// The request asked for <see cref="BacktestSegment.Unknown"/>. That label exists so an
    /// unrecognised sample type degrades safely instead of pointing at a meaningful segment;
    /// publishing a figure labelled "computed over the Unknown sample" asserts something the data
    /// does not support, the same failure class as publishing a <c>0.00</c> VaR. A DIFFERENT rule
    /// from <see cref="SegmentNotSpecified"/>, reaching the same place by different reasoning.
    /// </summary>
    UnknownSegmentNotSelectable,

    /// <summary>The request named no strategies. There is no group to analyse.</summary>
    NoStrategiesRequested,

    /// <summary>At least one requested strategy does not exist.</summary>
    StrategyNotFound,

    /// <summary>The requested lot grid is not a valid grid, so nothing can be resized onto it.</summary>
    InvalidLotGrid,

    /// <summary>A member's run holds trades carrying more than one segment. See <see cref="GroupRiskMemberStatus.RunSegmentsDisagree"/>.</summary>
    RunSegmentsDisagree,

    /// <summary>A member has no run carrying the requested segment. See <see cref="GroupRiskMemberStatus.NoEvidenceForSegment"/>.</summary>
    NoEvidenceForSegment,

    /// <summary>A member's two runs both carry the requested segment. See <see cref="GroupRiskMemberStatus.AmbiguousRunSelection"/>.</summary>
    AmbiguousRunSelection,

    /// <summary>A member's selected run yielded no risk-per-trade estimate.</summary>
    RiskNotEstimable,

    /// <summary>A member carries an allocation weight other than <c>1</c>.</summary>
    NonUnitWeight,

    /// <summary>
    /// The members' selected runs do not all carry the same segment. A correlation or VaR figure
    /// implies ONE sample label, so a mixed group is refused, naming the disagreeing members and
    /// their segments — never computed with a "mixed" label and never resolved by majority.
    /// </summary>
    HeterogeneousGroup,
}
