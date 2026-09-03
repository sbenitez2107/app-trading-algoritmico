using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Pure computation layer for rescaling a normalized run onto a lot grid. Stateless, no DbContext —
/// the <c>SymbolPointValueCalibrator</c> shape (design.md D10).
/// </summary>
public static class TradeResizer
{
    /// <summary>
    /// Re-expresses every trade of <paramref name="profile"/> at
    /// <paramref name="targetRiskPerTrade"/> on <paramref name="grid"/> (design.md D3/D7/D8).
    /// <para>
    /// NEAR-TOTAL: it refuses only inputs that are programming errors rather than data conditions
    /// — a non-positive <paramref name="targetRiskPerTrade"/>, or a hand-built profile carrying no
    /// estimate. It never refuses on the shape of the DATA. Holding a <see cref="RunRiskProfile"/> already proves
    /// the estimate passed, and clamping is not a failure to be rejected but an outcome to be
    /// labelled and counted.
    /// </para>
    /// <para>
    /// THE RULE. <c>q' = clamp(⌊qᵢ·target/(Â·step)⌋·step)</c> — evaluated as ONE quotient, never as
    /// <c>qᵢ·(target/Â)</c>, which rounds twice and drifts a step low; achieved risk is
    /// the trade's OWN band scaled by <c>q'/qᵢ</c>. Rounding is FLOOR, matching the direction the
    /// estimator inverted (D3) — the 2-decimal export's realized risk tops out at $199.98 and never
    /// exceeds $200, whereas round-half reproduces only 55 of its 90 sizes and would have
    /// over-risked 35 of them. A round-half resizer emits positions systematically larger than the
    /// backtest simulated.
    /// </para>
    /// <para>
    /// WHY THE BAND IS SCALED RATHER THAN RECOMPUTED. Recomputing every achieved risk from <c>Â</c>
    /// uniformly would throw away the exact measured value on SL closes and quietly replace a
    /// measurement with an imputation. Scaling the trade's own band keeps the provenance intact:
    /// a measured point stays a point, an imputed band stays a band, and an open side stays open.
    /// </para>
    /// <para>
    /// The unrounded lot count <c>uᵢ</c> is taken as its own LOWER endpoint <c>qᵢ</c>. Taking the
    /// band's midpoint was rejected because it breaks the round trip — <c>⌊qᵢ·1.005⌋</c> can exceed
    /// <c>qᵢ</c>, so resizing to the run's own estimate would no longer reproduce the run's own
    /// sizes. At <c>target == Â</c> the scale is exactly 1 and every size is already on the grid, so
    /// the operation is the identity, and the resizer therefore invents no precision the normalizer
    /// did not have.
    /// </para>
    /// </summary>
    public static ResizedTradeSeries Resize(RunRiskProfile profile, decimal targetRiskPerTrade, LotGrid grid)
    {
        var estimated = profile.Estimate.RiskPerTrade
            ?? throw new InvalidOperationException("A RunRiskProfile always carries an estimated risk per trade.");

        if (targetRiskPerTrade <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetRiskPerTrade),
                targetRiskPerTrade,
                "The target risk per trade must be positive. A non-positive target floors negative, "
                + "trips the minimum-lot branch for every row, and returns a complete, plausible-looking "
                + "series that is entirely wrong.");
        }

        var rows = new List<ResizedTrade>(profile.Trades.Count);
        var onTarget = 0;
        var raised = 0;
        var capped = 0;
        decimal? maxAchieved = null;
        var unknownAchieved = 0;
        var unscalable = 0;

        foreach (var trade in profile.Trades)
        {
            var original = trade.Size;
            var (resized, outcome) = ResizeOne(original, targetRiskPerTrade, estimated, grid);

            switch (outcome)
            {
                case ResizeOutcome.RaisedToMinimum: raised++; break;
                case ResizeOutcome.CappedAtMaximum: capped++; break;
                case ResizeOutcome.Unscalable: unscalable++; break;
                default: onTarget++; break;
            }

            // A non-positive original size gives nothing to scale FROM, so the achieved band is open
            // on both sides. It is never divided by — the row already carries RiskBasis.Unavailable.
            var achieved = original > 0m
                ? new TradeRiskInterval(
                    Scale(trade.Risk.Low, resized, original),
                    Scale(trade.Risk.High, resized, original))
                : TradeRiskInterval.Unknown;

            if (achieved.High is null)
                unknownAchieved++;
            else if (maxAchieved is null || achieved.High.Value > maxAchieved.Value)
                maxAchieved = achieved.High.Value;

            rows.Add(new ResizedTrade(
                RowIndex: trade.RowIndex,
                Ticket: trade.Ticket,
                OriginalSize: original,
                ResizedSize: resized,
                AchievedRisk: achieved,
                Outcome: outcome,
                Basis: trade.Basis));
        }

        return new ResizedTradeSeries(
            TargetRiskPerTrade: targetRiskPerTrade,
            Grid: grid,
            Trades: rows,
            OnTargetCount: onTarget,
            RaisedToMinimumCount: raised,
            CappedAtMaximumCount: capped,
            MaxAchievedRisk: maxAchieved,
            UnknownAchievedRiskCount: unknownAchieved,
            UnscalableCount: unscalable);
    }

    /// <summary>
    /// One size onto the grid: floor to the step, then clamp, reporting WHICH of the four things
    /// happened. <see cref="ResizeOutcome.RaisedToMinimum"/> is the one that over-risks — it is the
    /// mechanism behind a $200 intent realizing $229–$405 on a coarse grid.
    /// </summary>
    private static (decimal Size, ResizeOutcome Outcome) ResizeOne(
        decimal originalSize, decimal target, decimal estimated, LotGrid grid)
    {
        // Nothing to scale FROM. Reported as its own outcome rather than clamped up to MinLot,
        // which would both fabricate a size and count the row as OVER-risked while its achieved
        // risk is unknown — two reported aggregates contradicting each other for one row.
        if (originalSize <= 0m)
            return (originalSize, ResizeOutcome.Unscalable);

        // ONE rounding, not two. Computing `scale = target / estimated` first rounds the quotient
        // to 28 digits and only then multiplies, which floors a step LOW whenever the exact lot
        // count is integral and the quotient does not terminate: Â=199.98, target=66.66, size=3.00
        // gives 0.99 where the exact rule gives 1.00 — that case is exact and reproducible. Across a
        // size×target sweep the two forms disagree on a few percent of pairs, and in EVERY observed
        // disagreement the two-rounding form was the lower one. The rate itself depends on which
        // targets are swept, so no single percentage is quoted here: the load-bearing claim is the
        // direction, because a one-sided downward drift is invisible to `achieved <= target`.
        var floored = Math.Floor(originalSize * target / (estimated * grid.Step)) * grid.Step;

        if (floored < grid.MinLot)
            return (grid.MinLot, ResizeOutcome.RaisedToMinimum);

        if (floored > grid.MaxLots)
            return (grid.MaxLots, ResizeOutcome.CappedAtMaximum);

        return (floored, ResizeOutcome.OnTarget);
    }

    private static decimal? Scale(decimal? endpoint, decimal resizedSize, decimal originalSize)
        => endpoint is null ? null : endpoint.Value * resizedSize / originalSize;
}
