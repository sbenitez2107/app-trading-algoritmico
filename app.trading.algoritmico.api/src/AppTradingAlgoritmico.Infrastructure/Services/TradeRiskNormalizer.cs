using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Pure computation layer for a run's own risk-per-trade estimate. Stateless, no DbContext, fed
/// already-persisted <see cref="BacktestTrade"/> rows — the <c>SymbolPointValueCalibrator</c> shape
/// (design.md D10).
/// </summary>
public static class TradeRiskNormalizer
{
    /// <summary>
    /// The consistency gate (design.md D4). Settled at 0.85 and UNCALIBRATED: it has zero measured
    /// failures — the two committed exports score 100% and 93%, both passes — so it guards against a
    /// run broken in some way not yet observed. It is not the number that discriminates a coarse
    /// grid from a fine one; <c>MinLotPinnedFraction</c> does that job (D11).
    /// </summary>
    public const decimal MinimumConsistencyFraction = 0.85m;

    /// <summary>
    /// Sample floor, reusing <c>SymbolPointValueCalibrator</c>'s correction C1: 3, not 30. A higher
    /// floor permanently strands thin runs, and the consistency gate is the real guard — one sample
    /// of three disagreeing already fails 85%.
    /// </summary>
    public const int MinimumSlSamples = SymbolPointValueCalibrator.MinimumSlSamples;

    /// <summary>
    /// Estimates <c>Â</c>, the amount this run appears to have risked per trade, from its own
    /// SL closes (design.md D1/D2).
    /// <para>
    /// THE INVERSION. SQX sizes a trade by dividing the risked amount by the per-lot risk and then
    /// flooring onto the grid, so the unrounded lot count <c>u</c> satisfies
    /// <c>q &lt;= u &lt; q + step</c>. Realized risk is <c>r = q·Â/u</c>, which inverts to
    /// <c>Â ∈ [r, r·(q+step)/q)</c> — one feasible band per SL close, computed from
    /// <see cref="BacktestTrade.RealizedRisk"/> (<c>|MAE|</c>) and NEVER from
    /// <see cref="BacktestTrade.Profit"/>, which carries spread and commission and breaks the
    /// intersection outright.
    /// </para>
    /// <para>
    /// THE CHOICE. <c>Â</c> is the candidate contained by the most bands, candidates being the
    /// bands' lower endpoints, ties broken by the SMALLEST value. A strict intersection was
    /// rejected: it is non-empty on the 2-decimal export and empty on the 1-decimal one, so it
    /// cannot be the general rule — where the strict form returns nothing, this one degrades to 93%.
    /// A mean or median of endpoints was also rejected, because no band need contain it. The
    /// deterministic tie-break exists for the same reason <c>SelectDistinctContentRuns</c> takes the
    /// minimum GUID: one database must not estimate the same run two ways.
    /// </para>
    /// <para>
    /// <c>Â</c> is never seeded from a configured amount. That it lands on the configured band on
    /// both committed exports is a RESULT, not a guarantee — the 1-decimal run's seven clamped
    /// trades realize $229–$405 against that same nominal $200, and a seeded constant would have
    /// reported the intent while hiding the outcome.
    /// </para>
    /// </summary>
    public static RunRiskEstimate Estimate(IEnumerable<BacktestTrade> trades, LotGrid grid)
    {
        var all = trades as IReadOnlyList<BacktestTrade> ?? trades.ToList();

        var bands = new List<(decimal Low, decimal High)>();
        foreach (var trade in all)
        {
            if (!TryFeasibleBand(trade, grid, out var band))
                continue;

            bands.Add(band);
        }

        // Grid adequacy, over ALL trades and not just the SL closes (D11). It is REPORTED, never
        // gating: it asks whether the grid can express the target, which is a different question
        // from whether the sizing model fits, and the two separate in opposite directions.
        var minLotPinnedFraction = all.Count == 0
            ? 0m
            : (decimal)all.Count(t => t.Size == grid.MinLot) / all.Count;

        if (bands.Count < MinimumSlSamples)
        {
            return new RunRiskEstimate(
                Status: RunRiskEstimateStatus.InsufficientSamples,
                RiskPerTrade: null,
                ConsistencyFraction: 0m,
                MinLotPinnedFraction: minLotPinnedFraction,
                SlSampleCount: bands.Count);
        }

        var (riskPerTrade, coveredCount) = BestSupported(bands);
        var consistencyFraction = (decimal)coveredCount / bands.Count;

        if (consistencyFraction < MinimumConsistencyFraction)
        {
            // The value is withheld, the EVIDENCE is not: the fraction is the diagnosis, and a
            // caller that only learned "refused" could not tell a broken run from a thin one.
            return new RunRiskEstimate(
                Status: RunRiskEstimateStatus.Inconsistent,
                RiskPerTrade: null,
                ConsistencyFraction: consistencyFraction,
                MinLotPinnedFraction: minLotPinnedFraction,
                SlSampleCount: bands.Count);
        }

        return new RunRiskEstimate(
            Status: RunRiskEstimateStatus.Estimated,
            RiskPerTrade: riskPerTrade,
            ConsistencyFraction: consistencyFraction,
            MinLotPinnedFraction: minLotPinnedFraction,
            SlSampleCount: bands.Count);
    }

    /// <summary>
    /// Labels every trade of a run with its risk basis, or refuses the run entirely (design.md D4/D5).
    /// <para>
    /// <b>A refused run produces NO per-trade output.</b> Not an empty list, not a list of
    /// <see cref="RiskBasis.Unavailable"/> rows — <paramref name="profile"/> is <c>null</c>. A
    /// collection of rows carrying nothing is still a collection, and a caller would iterate, count
    /// and average it exactly as though it were evidence. The refusal's own evidence survives in
    /// <see cref="Estimate"/>, which the caller can still ask for.
    /// </para>
    /// <para>
    /// The <c>out</c> parameter is nullable so that ignoring the returned <c>bool</c> and
    /// dereferencing the profile is a compiler warning under this project's nullable settings,
    /// rather than a runtime surprise.
    /// </para>
    /// <para>
    /// BASIS PRECEDENCE is <c>Measured &gt; Unavailable &gt; Unbounded &gt; Imputed</c>. A measured
    /// SL close stays measured even when its <c>Size</c> is unusable, because the measurement never
    /// needed the size; everything else needs a positive size before any band can be computed.
    /// </para>
    /// </summary>
    public static bool TryNormalize(IReadOnlyList<BacktestTrade> trades, LotGrid grid, out RunRiskProfile? profile)
    {
        profile = null;

        var estimate = Estimate(trades, grid);
        if (estimate.Status != RunRiskEstimateStatus.Estimated)
            return false;

        var estimated = estimate.RiskPerTrade!.Value;
        var rows = new List<NormalizedTrade>(trades.Count);

        foreach (var trade in trades)
        {
            var (basis, risk) = Classify(trade, grid, estimated);
            var (rLow, rHigh) = RBounds(trade.Profit, risk);

            rows.Add(new NormalizedTrade(
                TradeId: trade.Id,
                RowIndex: trade.RowIndex,
                Ticket: trade.Ticket,
                CloseType: trade.CloseType,
                Size: trade.Size,
                Profit: trade.Profit,
                Basis: basis,
                Risk: risk,
                RLow: rLow,
                RHigh: rHigh));
        }

        profile = new RunRiskProfile(estimate, rows);
        return true;
    }

    /// <summary>
    /// One trade's basis and band (design.md D5). A trailing stop changes the EXIT, not the sizing —
    /// every trade was sized from its initial stop — so imputing <c>Â</c> for the roughly 73% of
    /// non-SL exits RECOVERS the amount the platform used rather than guessing at it. What it cannot
    /// recover is the unrounded lot count, hence a band rather than a point.
    /// </summary>
    private static (RiskBasis Basis, TradeRiskInterval Risk) Classify(BacktestTrade trade, LotGrid grid, decimal estimated)
    {
        var isSl = trade.CloseType == "SL";
        var realized = trade.RealizedRisk;

        if (isSl && realized is not null && realized.Value != 0m)
            return (RiskBasis.Measured, TradeRiskInterval.Point(Math.Abs(realized.Value)));

        var size = trade.Size;
        if (size <= 0m || isSl)
            return (RiskBasis.Unavailable, TradeRiskInterval.Unknown);

        // The lower edge of the band the floor rounding admits: had the unrounded count been any
        // larger, the size would have stepped up.
        var low = estimated * size / (size + grid.Step);

        if (size == grid.MinLot)
        {
            // A legitimate floor and a clamp UP are indistinguishable here, and a clamp up can only
            // RAISE realized risk. This is the mechanism behind the coarse export's $229-$405 trades
            // against a $200 intent, so the high side must stay open.
            return (RiskBasis.Unbounded, new TradeRiskInterval(low, null));
        }

        if (size == grid.MaxLots)
        {
            // A cap can only LOWER it, so the open side is the other one.
            return (RiskBasis.Unbounded, new TradeRiskInterval(null, estimated));
        }

        return (RiskBasis.Imputed, new TradeRiskInterval(low, estimated));
    }

    /// <summary>
    /// R bounds for one trade (design.md D6).
    /// <para>
    /// THE GOTCHA: the endpoints SWAP when <c>Profit &lt; 0</c>. R is <c>Profit/risk</c>, so with a
    /// positive profit the largest R comes from the SMALLEST risk — but with a negative profit
    /// dividing by a smaller number produces a MORE negative result, which is the LOW bound. Applying
    /// the positive-profit formula to a loss yields an interval whose low sits above its high.
    /// </para>
    /// <para>
    /// A null or zero endpoint yields a null bound. It is never divided by — an open band means the
    /// bound genuinely does not exist, and a zero endpoint would be an infinite R rather than a large one.
    /// </para>
    /// </summary>
    private static (decimal? RLow, decimal? RHigh) RBounds(decimal profit, TradeRiskInterval risk)
    {
        var fromLow = Divide(profit, risk.Low);
        var fromHigh = Divide(profit, risk.High);

        return profit >= 0m
            ? (fromHigh, fromLow)
            : (fromLow, fromHigh);
    }

    private static decimal? Divide(decimal profit, decimal? endpoint)
        => endpoint is null or 0m ? null : profit / endpoint.Value;

    /// <summary>
    /// The feasible band <c>[r, r·(q+step)/q)</c> for one SL close, or false when the row cannot
    /// contribute: a non-SL exit, a missing or zero <c>RealizedRisk</c>, or a non-positive
    /// <c>Size</c> (which would be a division by zero, never an exception thrown at a caller).
    /// </summary>
    private static bool TryFeasibleBand(BacktestTrade trade, LotGrid grid, out (decimal Low, decimal High) band)
    {
        band = default;

        if (trade.CloseType != "SL")
            return false;

        var realized = trade.RealizedRisk;
        if (realized is null or 0m)
            return false;

        var size = trade.Size;
        if (size <= 0m)
            return false;

        var low = Math.Abs(realized.Value);
        band = (low, low * (size + grid.Step) / size);
        return true;
    }

    /// <summary>
    /// The stabbing point: the candidate covered by the most bands, ties broken by the smallest
    /// value. Candidates are the lower endpoints, which is exact and finite — a stabbing point of a
    /// set of intervals always exists at one of them.
    /// </summary>
    private static (decimal? Value, int Covered) BestSupported(List<(decimal Low, decimal High)> bands)
    {
        decimal? best = null;
        var bestCovered = 0;

        foreach (var candidate in bands.Select(b => b.Low))
        {
            var covered = bands.Count(b => b.Low <= candidate && candidate < b.High);

            if (best is null || covered > bestCovered || (covered == bestCovered && candidate < best.Value))
            {
                best = candidate;
                bestCovered = covered;
            }
        }

        return (best, bestCovered);
    }
}
