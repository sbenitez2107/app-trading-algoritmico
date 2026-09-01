namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// Walk-forward segment label preserved from the SQX/AlgoWizard <c>Sample type</c> column.
/// <see cref="Unknown"/> is the default (0) so an unrecognised future label degrades safely
/// instead of pointing at a meaningful segment. The raw text is never lost — it is kept
/// verbatim alongside this enum on <see cref="AppTradingAlgoritmico.Domain.Entities.BacktestTrade.SampleTypeRaw"/>.
/// </summary>
public enum BacktestSegment
{
    Unknown = 0,
    InSample,
    OutOfSample,
    InSampleTest,
}
