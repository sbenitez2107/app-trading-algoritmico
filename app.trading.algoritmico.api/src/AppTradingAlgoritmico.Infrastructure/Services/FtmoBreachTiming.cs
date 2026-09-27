using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-first-breach-timing (design.md Decision 6, 7, 8) — merges first-breach timing across the
/// declared FX band, picks which limit broke first, and maps a merged point to its public DTO.
/// <c>internal static</c>, pure: no I/O.
/// </summary>
internal static class FtmoBreachTiming
{
    /// <summary>
    /// Earliest point across both FX-band ends, ordered by <c>(SourceTime, RowIndex)</c>. On a
    /// same-row tie (or a same-currency caller passing the identical point for both ends), reports
    /// <see cref="FtmoFxBandEnd.BothEnds"/> with <paramref name="low"/>'s values — never
    /// <paramref name="high"/>'s, never a synthesized blend (design.md Decision 6, consistent with
    /// <c>MergeFinding</c>'s existing fxLow preference on verdict agreement).
    /// </summary>
    internal static (FtmoBreachEvaluator.BreachPoint Point, FtmoFxBandEnd End)? Earliest(
        FtmoBreachEvaluator.BreachPoint? low, FtmoBreachEvaluator.BreachPoint? high)
    {
        if (low is null && high is null)
            return null;

        if (low is null)
            return (high!.Value, FtmoFxBandEnd.FxHigh);

        if (high is null)
            return (low.Value, FtmoFxBandEnd.FxLow);

        if (low.Value.RowIndex == high.Value.RowIndex)
            return (low.Value, FtmoFxBandEnd.BothEnds);

        var lowIsEarlier = (low.Value.SourceTime, low.Value.RowIndex).CompareTo((high.Value.SourceTime, high.Value.RowIndex)) < 0;
        return lowIsEarlier ? (low.Value, FtmoFxBandEnd.FxLow) : (high.Value, FtmoFxBandEnd.FxHigh);
    }

    /// <summary>
    /// Which limit's merged first-breach point is earliest. Sameness is detected by ROW IDENTITY —
    /// never by comparing timestamps alone (design.md Decision 7 / spec.md "Identical timestamps on
    /// different rows are not treated as a tie").
    /// </summary>
    internal static FtmoFirstBreachingLimit? FirstLimit(
        FtmoBreachEvaluator.BreachPoint? dailyMerged, FtmoBreachEvaluator.BreachPoint? maxMerged)
    {
        if (dailyMerged is null && maxMerged is null)
            return null;

        if (dailyMerged is null)
            return FtmoFirstBreachingLimit.Max;

        if (maxMerged is null)
            return FtmoFirstBreachingLimit.Daily;

        if (dailyMerged.Value.RowIndex == maxMerged.Value.RowIndex)
            return FtmoFirstBreachingLimit.BothSameClose;

        var dailyIsEarlier = (dailyMerged.Value.SourceTime, dailyMerged.Value.RowIndex)
            .CompareTo((maxMerged.Value.SourceTime, maxMerged.Value.RowIndex)) < 0;
        return dailyIsEarlier ? FtmoFirstBreachingLimit.Daily : FtmoFirstBreachingLimit.Max;
    }

    /// <summary>
    /// Maps a merged <c>(BreachPoint, FtmoFxBandEnd)</c> plus its elapsed-day figures into the public
    /// DTO. <c>PointClass</c> is derived here (design.md Decision 8) — never stored independently of
    /// <c>Causes</c>.
    /// </summary>
    internal static FtmoBreachTimingDto ToDto(
        (FtmoBreachEvaluator.BreachPoint Point, FtmoFxBandEnd End) merged,
        int FtmoTradingDaysElapsed,
        int CalendarDaysElapsed)
    {
        var (point, end) = merged;
        return new FtmoBreachTimingDto(
            point.SourceTime,
            point.FtmoDay,
            point.Balance,
            point.Level,
            point.Causes.Count == 0 ? FtmoBreachPointClass.Clean : FtmoBreachPointClass.Contingent,
            point.Causes,
            FtmoTradingDaysElapsed,
            CalendarDaysElapsed,
            end);
    }
}
