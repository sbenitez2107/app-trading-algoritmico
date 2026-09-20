using AppTradingAlgoritmico.Application.DTOs.Divergence;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// A 1:1 projection of <see cref="AppTradingAlgoritmico.Domain.Entities.SymbolCalibration"/>'s own
/// fields (design D5), minus <c>BaseEntity</c>'s audit columns, which the calculator has no use for.
/// <see cref="CalibratedAt"/> is carried verbatim — the same <see cref="DateTime"/> value read off
/// the entity, never re-derived, and this type MUST NOT carry any derived or compared value (no
/// staleness flag, no "is current" boolean, no age computation): <see cref="CalibratedAt"/> is
/// compared to nothing anywhere in this capability (design D8).
/// </summary>
internal readonly record struct SymbolCalibrationSnapshot(
    string Symbol,
    CalibrationStatus Status,
    decimal? PointValue,
    int SampleCount,
    decimal? MinObserved,
    decimal? MaxObserved,
    DateTime CalibratedAt);

/// <summary>
/// PR B2 — composes swap, embedded cost, and the execution residual over slice A's comparability
/// DTO (mandatory, never recomputed — design D5) plus B1's coverage component. `internal static`,
/// pure: no I/O, no randomness, no seed (spec "The Calculator Is Deterministic").
/// <para>
/// <c>comparability</c> is the ONLY source of the paired/demo-only/backtest-only net P/L figures —
/// this calculator never re-derives slice A's minute+direction pairing for net P/L. It DOES locally
/// re-derive the same minute+direction grouping to isolate the swap contribution of the paired
/// subset only (<see cref="PairedSwap"/>), because slice A's own output carries no such figure and
/// design D5's signature exposes no other way to obtain it — this is a private, swap-only
/// computation, not a recomputation of comparability itself (13.2's guard concerns slice A's own
/// pairing entry point specifically, never called here).
/// </para>
/// </summary>
internal static class CostDecompositionCalculator
{
    public static CostDecompositionDto Decompose(
        PriceOffsetComparabilityDto comparability,
        CoverageComponentDto coverage,
        IReadOnlyList<CostObservation> demo,
        IReadOnlyList<CostObservation> backtest,
        SymbolCalibrationSnapshot? calibration)
    {
        ArgumentNullException.ThrowIfNull(comparability);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(backtest);

        var swap = ComputeSwap(demo);
        var embedded = ComputeEmbeddedCost(backtest, calibration);
        var pairedSwap = ComputePairedSwap(demo, backtest);
        var residual = ComputeResidual(comparability, pairedSwap, embedded.EmbeddedCostEstimate);

        return new CostDecompositionDto(
            comparability.StrategyId,
            comparability.Kind,
            CostDecompositionStatus.Decomposed,
            comparability,
            coverage,
            swap,
            embedded,
            residual);
    }

    private static SwapComponentDto ComputeSwap(IReadOnlyList<CostObservation> demo)
    {
        if (demo.Count == 0)
            return new SwapComponentDto(null, 0, 0);

        decimal sum = 0;
        var payingCount = 0;
        foreach (var observation in demo)
        {
            var swap = observation.Swap ?? 0m;
            sum += swap;
            if (swap != 0m) payingCount++;
        }

        return new SwapComponentDto(sum, payingCount, demo.Count);
    }

    private static EmbeddedCostComponentDto ComputeEmbeddedCost(
        IReadOnlyList<CostObservation> backtest, SymbolCalibrationSnapshot? calibration)
    {
        if (calibration is null)
            return new EmbeddedCostComponentDto(EmbeddedCostAvailability.NoCalibrationRow, null, null, null, null);

        var snapshot = calibration.Value;
        var state = snapshot.Status switch
        {
            CalibrationStatus.InsufficientSamples => EmbeddedCostAvailability.InsufficientSamples,
            CalibrationStatus.Inconsistent => EmbeddedCostAvailability.Inconsistent,
            CalibrationStatus.Calibrated => EmbeddedCostAvailability.Calibrated,
            _ => EmbeddedCostAvailability.NoCalibrationRow,
        };

        if (state != EmbeddedCostAvailability.Calibrated)
            return new EmbeddedCostComponentDto(state, null, null, null, snapshot.CalibratedAt);

        var estimate = ComputeEmbeddedCostEstimate(backtest, snapshot.PointValue!.Value);

        return new EmbeddedCostComponentDto(state, snapshot.PointValue, snapshot.SampleCount, estimate, snapshot.CalibratedAt);
    }

    /// <summary>
    /// Estimate = Σ gross − Σ Profit(NetPl), where signed gross = (buy ? Close − Open : Open − Close)
    /// × Size × PointValue (spec Interfaces / Contracts). A degenerate row (<c>Size == 0</c> or
    /// <c>ClosePrice == OpenPrice</c>) nulls the WHOLE subset rather than a total silently missing a
    /// term — slice A's <c>NetPlAccumulator</c> rule (design D8) — as does any row with a null
    /// <c>ClosePrice</c> or <c>NetPl</c>.
    /// </summary>
    private static decimal? ComputeEmbeddedCostEstimate(IReadOnlyList<CostObservation> backtest, decimal pointValue)
    {
        if (backtest.Count == 0) return null;

        decimal sum = 0;
        foreach (var trade in backtest)
        {
            if (trade.ClosePrice is null || trade.NetPl is null) return null;
            if (trade.Size == 0m || trade.ClosePrice.Value == trade.OpenPrice) return null;

            var isBuy = trade.Type.Equals("buy", StringComparison.OrdinalIgnoreCase);
            var gross = (isBuy ? trade.ClosePrice.Value - trade.OpenPrice : trade.OpenPrice - trade.ClosePrice.Value)
                * trade.Size * pointValue;

            sum += gross - trade.NetPl.Value;
        }

        return sum;
    }

    /// <summary>
    /// The sum of <see cref="CostObservation.Swap"/> across ONLY the exact-minute-paired demo
    /// trades — the same minute+truncation and direction grouping slice A's calculator uses for
    /// pairing, applied here purely to isolate a swap sum, never to recompute comparability's own
    /// output (design D5; spec "The Comparability Gate Is A Structural Ordering Dependency").
    /// <c>null</c> if the paired subset is empty or any paired demo trade's swap is null.
    /// </summary>
    private static decimal? ComputePairedSwap(IReadOnlyList<CostObservation> demo, IReadOnlyList<CostObservation> backtest)
    {
        var demoGroups = GroupByMinuteAndDirection(demo);
        var backtestGroups = GroupByMinuteAndDirection(backtest);

        decimal sum = 0;
        var any = false;
        var incomplete = false;

        foreach (var key in demoGroups.Keys)
        {
            if (!backtestGroups.TryGetValue(key, out var backtestList)) continue;
            if (!demoGroups.TryGetValue(key, out var demoList)) continue;
            if (demoList.Count != 1 || backtestList.Count != 1) continue;

            any = true;
            var swap = demoList[0].Swap;
            if (swap is null) incomplete = true;
            else sum += swap.Value;
        }

        return any && !incomplete ? sum : null;
    }

    private static Dictionary<(long MinuteTicks, string Direction), List<CostObservation>> GroupByMinuteAndDirection(
        IReadOnlyList<CostObservation> observations)
    {
        var groups = new Dictionary<(long, string), List<CostObservation>>();
        foreach (var observation in observations)
        {
            var minuteTicks = observation.OpenTime.Ticks - (observation.OpenTime.Ticks % TimeSpan.TicksPerMinute);
            var key = (minuteTicks, observation.Type.ToUpperInvariant());
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(observation);
        }

        return groups;
    }

    /// <summary>
    /// Residual = PairedDemoNetPl − PairedBacktestNetPl − PairedSwap + EmbeddedCostEstimate, over
    /// the paired subset only; <c>null</c> if any term is <c>null</c> (spec Interfaces / Contracts).
    /// Demo-only and backtest-only figures travel on the result for context only — they are never
    /// read here, so mutating them can never move the residual (spec "Unpaired subsets are reported
    /// separately and are not summands"; proposal R1).
    /// </summary>
    private static ExecutionResidualDto ComputeResidual(
        PriceOffsetComparabilityDto comparability, decimal? pairedSwap, decimal? embeddedCostEstimate)
    {
        decimal? residual = null;
        if (comparability.PairedDemoNetPl is not null
            && comparability.PairedBacktestNetPl is not null
            && pairedSwap is not null
            && embeddedCostEstimate is not null)
        {
            residual = comparability.PairedDemoNetPl.Value
                - comparability.PairedBacktestNetPl.Value
                - pairedSwap.Value
                + embeddedCostEstimate.Value;
        }

        return new ExecutionResidualDto(
            residual,
            comparability.DemoOnlyNetPl,
            comparability.BacktestOnlyNetPl,
            comparability.PairedDemoNetPl,
            comparability.PairedBacktestNetPl);
    }
}
