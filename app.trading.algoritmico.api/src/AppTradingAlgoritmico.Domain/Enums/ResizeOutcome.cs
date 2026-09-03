namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// What the lot grid actually did to one resized trade (design.md D8). Clamping is legitimate and
/// unavoidable, so it is never a refusal — it is labelled and counted, because
/// <see cref="RaisedToMinimum"/> means the position is OVER-risked against the target.
/// </summary>
public enum ResizeOutcome
{
    /// <summary><c>MinLot &lt;= q' &lt;= MaxLots</c>. Achieved risk is at or below target, within one step.</summary>
    OnTarget = 0,

    /// <summary><c>q' &lt; MinLot</c> — the grid cannot go small enough, so achieved risk EXCEEDS the target.</summary>
    RaisedToMinimum,

    /// <summary><c>q' &gt; MaxLots</c> — the grid cannot go large enough, so achieved risk falls under the target.</summary>
    CappedAtMaximum,

    /// <summary>
    /// The row could not be scaled at all: its original <c>Size</c> is zero or negative, so there
    /// is nothing to scale FROM. Distinct from <see cref="RaisedToMinimum"/>, which asserts the row
    /// is OVER-risked — a claim that cannot be made about a row whose achieved risk is unknown.
    /// </summary>
    Unscalable,
}
