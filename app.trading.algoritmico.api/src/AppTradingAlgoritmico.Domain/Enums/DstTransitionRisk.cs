namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Discloses that a calendar month MAY contain a US/broker daylight-saving transition —
/// deliberately a CALENDAR RULE (<c>Month is 3 or 10 or 11</c>), not a tzdata lookup. No app
/// dependency currently ships a timezone database version, and the SQX <c>DST: Yes</c> flag does
/// not identify which day a transition happened on, so no transition DATE is ever computed
/// (design.md D2).
/// <para>
/// A transition does not corrupt the entry-price offset this readout measures: it shifts one
/// side's minute key, so the affected trades simply stop pairing. The symptom is a collapse in
/// that month's paired-trade count, which the readout already reports beside every figure — this
/// flag only names the months where that collapse might be a DST artifact rather than a genuine
/// comparability gap. {3, 10, 11} deliberately over-includes (the November value also catches a
/// same-week non-transition read for some regions); over-inclusion of a disclosure is safe in a
/// way that under-inclusion is not.
/// </para>
/// </summary>
public enum DstTransitionRisk
{
    None = 0,
    TransitionMonth = 1,
}
