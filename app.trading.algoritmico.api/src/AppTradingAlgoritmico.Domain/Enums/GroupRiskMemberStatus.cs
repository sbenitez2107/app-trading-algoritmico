namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why one member of a backtest group risk analysis does, or does not, contribute a series
/// (design.md D8a/D8b/D3).
/// <para>
/// Every value below <see cref="Resolved"/> is a NAMED refusal, never a silent exclusion. A group
/// figure computed over the members that happened to work would answer a different question from
/// the one the operator asked, so a refused member refuses the analysis and says which member and
/// why.
/// </para>
/// </summary>
public enum GroupRiskMemberStatus
{
    /// <summary>Exactly one run carried the requested segment and it produced a series.</summary>
    Resolved = 0,

    /// <summary>
    /// One of the strategy's runs holds trades carrying more than one segment. Only a hand-edited
    /// database reaches this, and it is refused rather than resolved to either value.
    /// </summary>
    RunSegmentsDisagree,

    /// <summary>
    /// No run carries the requested segment once trade-less runs are excluded. This is the explicit
    /// "no evidence for this segment" STATE — no series, not an empty one.
    /// </summary>
    NoEvidenceForSegment,

    /// <summary>
    /// BOTH of the strategy's runs carry the requested segment. Two runs sharing a segment are two
    /// different parameter sets over the same sample, so picking either would make the published
    /// figure depend on an arbitrary choice — and preferring <c>Evaluation</c> would be a
    /// <c>Kind</c>-to-<c>Segment</c> inference, which nothing supports. An optional
    /// <see cref="BacktestRunKind"/> on the request disambiguates; absent it, the ambiguity is
    /// refused rather than guessed.
    /// </summary>
    AmbiguousRunSelection,

    /// <summary>
    /// The selected run's own risk per trade could not be estimated from its SL closes, so there is
    /// nothing to resize onto the target. Slice 2a's refusal, surfaced here.
    /// </summary>
    RiskNotEstimable,

    /// <summary>
    /// The member carries an allocation weight other than <c>1</c>. The series is ALREADY SIZED at
    /// its own target risk per trade, so applying a second factor would double-size it.
    /// </summary>
    NonUnitWeight,
}
