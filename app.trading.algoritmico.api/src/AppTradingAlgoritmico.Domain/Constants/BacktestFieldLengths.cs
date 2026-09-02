namespace AppTradingAlgoritmico.Domain.Constants;

/// <summary>
/// THE source of truth for the length-bounded text columns of the backtest-import tables.
/// <para>
/// The EF configurations and BOTH SQX CSV parsers MUST read these constants. They are two ends
/// of one rule: the parser refuses an over-length value while it is still data, so it never
/// reaches the column that would refuse it as a "String or binary data would be truncated" error
/// at <c>SaveChanges</c> — an error that is NOT transient, so no retry strategy recovers from it.
/// </para>
/// <para>
/// A hand-copied literal in a second place is exactly how those two ends drift apart, which is
/// why <c>BacktestSchemaTests.TextColumnLengths_ComeFromTheSharedConstants</c> fences the mapping
/// against a re-introduced magic number.
/// </para>
/// </summary>
public static class BacktestFieldLengths
{
    /// <summary>SQX symbol, e.g. <c>XAUUSD_M1_UTC02</c> — not the broker item.</summary>
    public const int Symbol = 60;

    /// <summary>Trade direction as exported, e.g. <c>Buy</c>.</summary>
    public const int TradeType = 20;

    /// <summary>Walk-forward label verbatim, e.g. <c>IS</c>, <c>OOS1</c>, <c>IST</c>.</summary>
    public const int SampleTypeRaw = 30;

    /// <summary>Close reason verbatim, e.g. <c>SL</c>, <c>End Of Friday (Time)</c>.</summary>
    public const int CloseType = 30;

    /// <summary>Free-text comment column — the widest, and the one most likely to overflow.</summary>
    public const int Comment = 500;

    /// <summary>
    /// <c>SourceFileName</c> on both import tables — the Windows MAX_PATH bound on a name the user
    /// supplies. Filename parsing is gone (attribution is a foreign key now), so this guards the
    /// stored name and nothing else.
    /// </summary>
    public const int FileNameOrKey = 260;

    /// <summary>SHA-256 as lowercase hex — fixed width, never user-supplied.</summary>
    public const int ContentHash = 64;

    /// <summary>
    /// The walk-forward export's <c>Parameters</c> text, stored verbatim on every window and twice
    /// more on the export itself (<c>DeployParameters</c>/<c>EvaluationParameters</c>). The
    /// committed fixture's rows are ~70 characters, but the field is an opaque, strategy-specific
    /// <c>key=value</c> list whose length grows with the number of optimized inputs — so the width
    /// is generous rather than fitted to one file.
    /// </summary>
    public const int WalkForwardParameters = 1000;
}
