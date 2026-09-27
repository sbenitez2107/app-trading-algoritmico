namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Whether one breaching close (a <c>BreachPoint</c>) carried any contingency cause of its own
/// (first-breach timing, spec.md "New Timing Fields Use Enums, Never Booleans Or Survival Wording").
/// Derived when mapping to the DTO (design.md Decision 8): <c>Causes.Count == 0 ? Clean : Contingent</c>.
/// Never stored independently of <c>Causes</c>, so the two cannot disagree.
/// </summary>
public enum FtmoBreachPointClass
{
    /// <summary>The close's own causes list is empty — a clean breaching close.</summary>
    Clean = 0,

    /// <summary>The close carries at least one of its own contingency causes.</summary>
    Contingent = 1,
}
