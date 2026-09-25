namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Why a detected breach was downgraded to <see cref="FtmoBreachVerdict.BreachContingent"/>
/// (design.md Decision 5). Always carried in a list on the finding — never read alone as a full
/// verdict — so there is no single CLR-default cause that could be misread as "no cause".
/// </summary>
public enum BreachContingencyCause
{
    /// <summary>Another trade's <c>OpenTime</c> precedes and <c>CloseTime</c> follows the breaching close.</summary>
    ConcurrentOpenPosition = 0,

    /// <summary>The close's source timestamp is ambiguous under its source zone (daily limit only).</summary>
    AmbiguousSourceTime,

    /// <summary>The close's source timestamp is invalid under its source zone (daily limit only).</summary>
    InvalidSourceTime,

    /// <summary>The close falls in a source/Berlin DST-mismatch window whose exact day differs from the naive day (daily limit only).</summary>
    DstMismatchWindow,

    /// <summary>
    /// An <see cref="ResizeOutcome.Unscalable"/> row closed before the breach. Max limit: any time
    /// before it. Daily limit: only on the breach's own FTMO day — on a later day the balance and
    /// its midnight reference carry the same missing P/L, so the comparison is unaffected.
    /// </summary>
    UnscalableTradeExcluded,

    /// <summary>The verdict differs between the declared FX band's low and high ends.</summary>
    FxRoundingSensitive,
}
