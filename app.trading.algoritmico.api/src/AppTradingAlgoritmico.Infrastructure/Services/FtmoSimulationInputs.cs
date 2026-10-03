using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// PR3 (design.md Decision 8): the shipped guard chain and projection extracted verbatim out of
/// <see cref="FtmoBreachSimulationReadService"/>, so <c>FtmoMultiStartReadService</c> (PR4) can reuse
/// the exact same resolution without duplicating it. Refactor only — no behaviour change: this class
/// has no new consumer in this PR, and <see cref="FtmoBreachSimulationReadService"/> delegates to it
/// while its own <c>RefuseAll</c>/<c>Refused</c>/evaluation logic stay where they were shipped.
/// </summary>
internal static class FtmoSimulationInputs
{
    private const string BerlinIanaId = "Europe/Berlin";
    private const string UsdCurrency = "USD";

    /// <summary>
    /// Runs, limits, spec, calibration, FX, grid and zones resolution (design.md Decision 8). The guard
    /// chain runs in the ORDER design.md specifies: product -&gt; limits -&gt; drawdown model -&gt;
    /// instrument spec -&gt; point-value-null check -&gt; FX band -&gt; normalizer -&gt; segments -&gt;
    /// timezone -&gt; request validity. These are properties of the STRATEGY/request, not of one run,
    /// so they are evaluated ONCE.
    /// </summary>
    internal sealed record SharedResolution(
        bool NoRuns,
        FtmoSimulationRefusal? Refusal,
        List<(Guid Id, BacktestRunKind Kind)> Runs,
        decimal DailyPct,
        decimal MaxPct,
        FtmoInstrumentSpec? Spec,
        LotGrid? SourceGrid,
        LotGrid? FtmoGrid,
        decimal PointValue,
        (decimal Low, decimal High) FxBand,
        TimeZoneInfo? SourceZone,
        TimeZoneInfo? BerlinZone,
        decimal? ProfitTargetPct);

    /// <summary>
    /// Segments -&gt; normalizer -&gt; <see cref="FtmoTradeProjector.RefuseRunInputs"/> -&gt; low/high
    /// projection and counts (design.md Decision 8). These are properties of one run's own trades, so
    /// they are evaluated PER RUN.
    /// </summary>
    internal sealed record RunProjection(
        FtmoSimulationRefusal? Refusal,
        BacktestSegment Segment,
        List<FtmoTradeProjector.ProjectedTrade>? ProjectedLow,
        List<FtmoTradeProjector.ProjectedTrade>? ProjectedHigh,
        int RaisedToMinimumCount,
        int CappedAtMaximumCount,
        int UnscalableCount);

    /// <summary>
    /// ftmo-group-simulation B1 (design.md D1): the broker-wide half of the shipped guard chain — limits
    /// row, product, drawdown model. Independent of any symbol, so a group resolves it ONCE.
    /// <see cref="Refusal"/> is <see cref="FtmoSimulationRefusal.LimitsNotConfigured"/> (percentages zeroed),
    /// <see cref="FtmoSimulationRefusal.ProductNotTwoStep"/> or
    /// <see cref="FtmoSimulationRefusal.DrawdownModelNotStatic"/> (percentages and profit target carried).
    /// </summary>
    internal sealed record LimitsResolution(
        FtmoSimulationRefusal? Refusal, decimal DailyPct, decimal MaxPct, decimal? ProfitTargetPct);

    /// <summary>
    /// ftmo-group-simulation B1 (design.md D1): the symbol-wide half — spec, grid, point value, FX band
    /// and source zone. <see cref="Refusal"/> is the first failure of spec -&gt; calibration -&gt; FX; every
    /// other field holds the value it had at that point, exactly as the shipped chain did.
    /// <see cref="ZoneRefusal"/> is a SEPARATE channel (set only when <see cref="Refusal"/> is null) so the
    /// composition can keep <c>InvalidRequest</c> ahead of <c>TimeZoneDataUnavailable</c>.
    /// </summary>
    internal sealed record SymbolResolution(
        FtmoSimulationRefusal? Refusal,
        FtmoInstrumentSpec? Spec,
        LotGrid? FtmoGrid,
        decimal PointValue,
        (decimal Low, decimal High) FxBand,
        FtmoSimulationRefusal? ZoneRefusal,
        TimeZoneInfo? SourceZone);

    internal static async Task<LimitsResolution> ResolveLimitsAsync(AppDbContext db, string broker, CancellationToken ct)
    {
        var limits = await db.BrokerRiskLimits.AsNoTracking()
            .Where(l => l.Broker == broker && l.Kind == GuardrailKind.LossLimits)
            .FirstOrDefaultAsync(ct);

        // A null percentage means the rule is NOT configured — never a 0% rule: 0% would put the max
        // floor at initial capital and read every loss as a false Breached. A stored value outside
        // (0, 1] is not a usable rule either (the fraction contract RiskLimitsService enforces for
        // stages and VaR targets). Both are properties of the stored configuration, not of the
        // request, so they refuse as LimitsNotConfigured, exactly like a missing row. The pattern
        // binds dailyPct/maxPct: the compiler, not a fallback value, proves them set past this guard.
        if (limits is not
            {
                DailyLossLimitPct: { } dailyPct and > 0m and <= 1m,
                MaxLossLimitPct: { } maxPct and > 0m and <= 1m,
            })
        {
            return new LimitsResolution(FtmoSimulationRefusal.LimitsNotConfigured, 0m, 0m, null);
        }

        FtmoSimulationRefusal? refusal =
            limits.FtmoProduct is null || limits.FtmoProduct != FtmoProduct.TwoStep ? FtmoSimulationRefusal.ProductNotTwoStep
            : limits.DrawdownModel != DrawdownModel.Static ? FtmoSimulationRefusal.DrawdownModelNotStatic
            : null;

        return new LimitsResolution(refusal, dailyPct, maxPct, limits.ProfitTargetPct);
    }

    /// <summary>A spec is usable when its contract size is positive and its lot grid constructs.</summary>
    private static bool TryBuildSpecGrid(FtmoInstrumentSpec? spec, out LotGrid? grid)
    {
        grid = null;
        if (spec is null || spec.ContractSize <= 0m)
            return false;

        try
        {
            grid = new LotGrid(spec.SizeDecimals, spec.Step, spec.MinLot, spec.MaxLots);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal static SymbolResolution ResolveSymbol(
        FtmoInstrumentSpec? spec, SymbolCalibration? calibration, decimal? fxLow, decimal? fxHigh)
    {
        if (!TryBuildSpecGrid(spec, out var ftmoGrid))
        {
            // Spec refusal wins: the calibration is only consulted for a usable spec.
            return new SymbolResolution(
                FtmoSimulationRefusal.InstrumentSpecMissing, spec, null, 0m, (1m, 1m), null, null);
        }

        // Load-bearing null check (design.md Decision 6): Calibrated == 0 is the CLR default, so
        // a null PointValue must be checked independently of Status. Covers NQ.
        if (calibration is null
            || calibration.Status != CalibrationStatus.Calibrated
            || calibration.PointValue is null
            || calibration.PointValue.Value <= 0m)
        {
            return new SymbolResolution(
                FtmoSimulationRefusal.PointValueNotCalibrated, spec, ftmoGrid, 0m, (1m, 1m), null, null);
        }

        var pointValue = calibration.PointValue.Value;

        (decimal Low, decimal High) fxBand = (1m, 1m);
        if (!SettlesInAccountCurrency(spec!))
        {
            if (fxLow is null || fxHigh is null)
            {
                return new SymbolResolution(
                    FtmoSimulationRefusal.FxRateNotDeclared, spec, ftmoGrid, pointValue, fxBand, null, null);
            }

            if (fxLow.Value <= 0m || fxHigh.Value <= 0m || fxLow.Value > fxHigh.Value)
            {
                return new SymbolResolution(
                    FtmoSimulationRefusal.InvalidFxBand, spec, ftmoGrid, pointValue, fxBand, null, null);
            }

            fxBand = (fxLow.Value, fxHigh.Value);
        }

        // Zone resolution has no side effects, so it is evaluated here but reported on its OWN channel:
        // the shipped chain reported request validity (InvalidRequest) before the zone.
        var zoneRefusal = FtmoDayClock.TryResolveZone(spec!.SourceTimeZoneId, out var sourceZone)
            ? (FtmoSimulationRefusal?)null
            : FtmoSimulationRefusal.TimeZoneDataUnavailable;

        return new SymbolResolution(null, spec, ftmoGrid, pointValue, fxBand, zoneRefusal, sourceZone);
    }

    internal static async Task<SymbolResolution> ResolveSymbolAsync(
        AppDbContext db, string sqxSymbol, decimal? fxLow, decimal? fxHigh, CancellationToken ct)
    {
        var spec = await db.FtmoInstrumentSpecs.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SqxSymbol == sqxSymbol, ct);

        // The calibration query is only issued for a usable spec (preserves the shipped query shape).
        SymbolCalibration? calibration = null;
        if (TryBuildSpecGrid(spec, out _))
        {
            calibration = await db.SymbolCalibrations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Symbol == sqxSymbol, ct);
        }

        return ResolveSymbol(spec, calibration, fxLow, fxHigh);
    }

    /// <summary>
    /// Known, deliberate test gap: the false branch is unreachable in practice because
    /// <c>BerlinIanaId</c> is a hardcoded, always-shipped IANA id. It stays as a defensive guard for a
    /// host without ICU/tz data; no test contorts the environment to reach it.
    /// </summary>
    internal static bool TryResolveBerlin(out TimeZoneInfo? berlinZone)
        => FtmoDayClock.TryResolveZone(BerlinIanaId, out berlinZone);

    /// <summary>True when the spec's profit currency is the FTMO account currency (<c>UsdCurrency</c>).</summary>
    internal static bool SettlesInAccountCurrency(FtmoInstrumentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return string.Equals(spec.ProfitCurrency, UsdCurrency, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<SharedResolution> ResolveSharedAsync(AppDbContext db, FtmoBreachSimulationRequest request, CancellationToken ct)
    {
        var runs = await db.BacktestRuns.AsNoTracking()
            .Where(r => r.StrategyId == request.StrategyId)
            .Select(r => new { r.Id, r.Kind })
            .ToListAsync(ct);

        var runTuples = runs.Select(r => (r.Id, r.Kind)).ToList();

        if (runs.Count == 0)
            return new SharedResolution(true, null, runTuples, 0m, 0m, null, null, null, 0m, (1m, 1m), null, null, null);

        var limits = await ResolveLimitsAsync(db, request.Broker, ct);

        if (limits.Refusal == FtmoSimulationRefusal.LimitsNotConfigured)
        {
            return new SharedResolution(
                false, limits.Refusal, runTuples, 0m, 0m, null, null, null, 0m, (1m, 1m), null, null, null);
        }

        var sourceGrid = request.TryBuildSourceGrid();

        if (limits.Refusal is not null)
        {
            // Product / drawdown-model refusal: the symbol queries are never issued.
            return new SharedResolution(
                false, limits.Refusal, runTuples, limits.DailyPct, limits.MaxPct, null, sourceGrid, null, 0m, (1m, 1m),
                null, null, limits.ProfitTargetPct);
        }

        var symbol = await ResolveSymbolAsync(db, request.SqxSymbol, request.FxLow, request.FxHigh, ct);
        var sharedRefusal = symbol.Refusal;

        if (sharedRefusal is null
            && (sourceGrid is null || request.InitialCapital <= 0m || request.TargetRiskPerTrade <= 0m))
        {
            sharedRefusal = FtmoSimulationRefusal.InvalidRequest;
        }

        TimeZoneInfo? sourceZone = null;
        TimeZoneInfo? berlinZone = null;
        if (sharedRefusal is null)
        {
            sourceZone = symbol.SourceZone;
            if (symbol.ZoneRefusal is not null || !TryResolveBerlin(out berlinZone))
                sharedRefusal = FtmoSimulationRefusal.TimeZoneDataUnavailable;
        }

        return new SharedResolution(
            false, sharedRefusal, runTuples, limits.DailyPct, limits.MaxPct, symbol.Spec, sourceGrid, symbol.FtmoGrid,
            symbol.PointValue, symbol.FxBand, sourceZone, berlinZone, limits.ProfitTargetPct);
    }

    internal static RunProjection ProjectRun(
        List<BacktestTrade> trades,
        LotGrid sourceGrid,
        LotGrid ftmoGrid,
        decimal targetRiskPerTrade,
        decimal pointValue,
        decimal contractSize,
        (decimal Low, decimal High) fxBand)
    {
        var segment = trades.Count > 0 ? trades[0].Segment : BacktestSegment.Unknown;

        if (trades.Select(t => t.Segment).Distinct().Count() > 1)
            return new RunProjection(FtmoSimulationRefusal.RunSegmentsDisagree, segment, null, null, 0, 0, 0);

        // The RiskPerTrade-is-null disjunct is unreachable through TryNormalize: it returns true only
        // for Status == Estimated, which always carries a value. Kept as a null guard, not a tested path.
        if (!TradeRiskNormalizer.TryNormalize(trades, sourceGrid, out var profile)
            || profile!.Estimate.RiskPerTrade is null)
        {
            return new RunProjection(FtmoSimulationRefusal.RiskNotEstimable, segment, null, null, 0, 0, 0);
        }

        var estimated = profile.Estimate.RiskPerTrade!.Value;

        // Through this service, RefuseRunInputs' estimatedRisk <= 0 and target <= 0 branches are
        // unreachable: Â is |RealizedRisk| of a non-zero SL row (strictly positive), and a non-positive
        // target was already refused as InvalidRequest above. Both are covered in FtmoTradeProjectorTests.
        var runInputRefusal = FtmoTradeProjector.RefuseRunInputs(
            estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.Low);
        if (runInputRefusal is not null)
            return new RunProjection(runInputRefusal.Value, segment, null, null, 0, 0, 0);

        var projectedLow = trades
            .Select(t => FtmoTradeProjector.Project(t, estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.Low, ftmoGrid))
            .ToList();
        var projectedHigh = trades
            .Select(t => FtmoTradeProjector.Project(t, estimated, targetRiskPerTrade, pointValue, contractSize, fxBand.High, ftmoGrid))
            .ToList();

        return new RunProjection(
            null, segment, projectedLow, projectedHigh,
            projectedLow.Count(p => p.Outcome == ResizeOutcome.RaisedToMinimum),
            projectedLow.Count(p => p.Outcome == ResizeOutcome.CappedAtMaximum),
            projectedLow.Count(p => p.Outcome == ResizeOutcome.Unscalable));
    }
}
