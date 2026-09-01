using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Pure computation layer for the per-symbol point-value assessment (CAL-1..CAL-5). Stateless —
/// fed already-persisted <see cref="BacktestTrade"/> rows for one symbol, recomputed over the
/// UNION of ALL SL-closed trades for that symbol every time (design.md D4) so import order never
/// changes the result. Formula: <c>|MAE| / (|ClosePrice - OpenPrice| * Size)</c>, using
/// <see cref="BacktestTrade.RealizedRisk"/> (which equals <c>|MAE|</c> exactly for SL-closed
/// trades) — NEVER <see cref="BacktestTrade.Profit"/>, which carries spread and commission.
/// </summary>
public static class SymbolPointValueCalibrator
{
    /// <summary>
    /// Binding correction C1 (overrides the original 30-sample proposal): 3, not 30.
    /// PointValue is a contract constant with zero measured variance across 185 SL closes; a
    /// large count floor would permanently strand thinly-traded symbols. The <c>Inconsistent</c>
    /// status (spread gate below) is the real guard against a genuinely bad sample.
    /// </summary>
    public const int MinimumSlSamples = 3;

    private const decimal MaxSpreadFraction = 0.005m; // 0.5%

    /// <summary>
    /// THE run-selection rule (design.md D4): the SL-closed population draws from one run per
    /// distinct <c>ContentHash</c>.
    /// <para>
    /// Attribution by foreign key reintroduced exactly the double-counting the previous revision's
    /// join table prevented. One SQX strategy deployed on two accounts is two <c>Strategy</c> rows,
    /// and the same exported file legitimately backs a run under each — that is the whole reason
    /// <c>ContentHash</c> lost its unique index. Pooled naively, the same 90 SL closes would be
    /// counted 180 times. The median is immune (every sample is exactly 100.000), but
    /// <c>SampleCount</c> is the precise value the <c>InsufficientSamples</c> floor evaluates, so
    /// a thin symbol could clear the floor by re-importing one file under a second strategy.
    /// </para>
    /// <para>
    /// The kept run is the lowest <see cref="Guid"/> among the duplicates, not "the first one the
    /// query returned": without a total order the same database could calibrate differently
    /// between two identical requests.
    /// </para>
    /// </summary>
    public static IReadOnlySet<Guid> SelectDistinctContentRuns(IEnumerable<(Guid RunId, string ContentHash)> runs)
        => runs
            .GroupBy(r => r.ContentHash, StringComparer.Ordinal)
            .Select(g => g.Min(r => r.RunId))
            .ToHashSet();

    public static SymbolCalibrationDto Calibrate(string symbol, IEnumerable<BacktestTrade> trades, DateTime calibratedAt)
    {
        var samples = new List<decimal>();

        foreach (var trade in trades)
        {
            if (trade.CloseType != "SL")
                continue;

            if (trade.ClosePrice == trade.OpenPrice || trade.Size == 0m)
                continue; // degenerate denominator — skipped, never divided

            var mae = trade.RealizedRisk; // == |MAE| for every SL-closed trade
            if (mae is null or 0m)
                continue;

            var denominator = Math.Abs(trade.ClosePrice - trade.OpenPrice) * trade.Size;
            samples.Add(Math.Abs(mae.Value) / denominator);
        }

        var sampleCount = samples.Count;

        if (sampleCount < MinimumSlSamples)
        {
            return new SymbolCalibrationDto(
                Symbol: symbol,
                PointValue: null,
                SampleCount: sampleCount,
                MinObserved: sampleCount > 0 ? samples.Min() : null,
                MaxObserved: sampleCount > 0 ? samples.Max() : null,
                Status: CalibrationStatus.InsufficientSamples,
                CalibratedAt: calibratedAt);
        }

        var median = Median(samples);
        var min = samples.Min();
        var max = samples.Max();
        var spread = median == 0m ? 0m : (max - min) / median;

        if (spread > MaxSpreadFraction)
        {
            return new SymbolCalibrationDto(
                Symbol: symbol,
                PointValue: null,
                SampleCount: sampleCount,
                MinObserved: min,
                MaxObserved: max,
                Status: CalibrationStatus.Inconsistent,
                CalibratedAt: calibratedAt);
        }

        return new SymbolCalibrationDto(
            Symbol: symbol,
            PointValue: median,
            SampleCount: sampleCount,
            MinObserved: min,
            MaxObserved: max,
            Status: CalibrationStatus.Calibrated,
            CalibratedAt: calibratedAt);
    }

    private static decimal Median(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2m;
    }
}
