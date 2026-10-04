using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-simulation B3 (design.md D8) — the picker's candidates read. Five fixed, <c>AsNoTracking</c>
/// queries, none per strategy: the account's strategies (with the other-account name flag as a subquery), their
/// runs, ONE aggregate over the trades of those runs (count, first open, last close; no trade rows are loaded),
/// and the specs and calibrations of the distinct run symbols (<c>Contains</c>). It does not use the
/// cross-broker resizer path.
/// <para>
/// The spec and calibration flags come from the SAME pure <see cref="FtmoSimulationInputs.ResolveSymbol"/> the
/// simulation uses (with no FX band), so the picker can never promise what the simulation refuses: a usable spec
/// is <c>ContractSize &gt; 0</c> with a constructible lot grid, and calibration is only consulted for a usable spec.
/// </para>
/// </summary>
public sealed class FtmoGroupCandidatesReadService(AppDbContext db) : IFtmoGroupCandidatesReadService
{
    private sealed record HeldRun(Guid RunId, Guid StrategyId, BacktestRunKind Kind, string? Symbol);

    private sealed record TradeAggregate(Guid RunId, int Count, DateTime FirstOpen, DateTime LastClose);

    public async Task<FtmoGroupCandidatesDto> GetCandidatesAsync(Guid tradingAccountId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var strategies = await db.Strategies.AsNoTracking()
            .Where(s => s.TradingAccountId == tradingAccountId)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Symbol,
                NameExistsOnOtherAccount = db.Strategies.Any(o =>
                    o.TradingAccountId != null && o.TradingAccountId != tradingAccountId && o.Name.ToLower() == s.Name.ToLower()),
            })
            .ToListAsync(ct);

        if (strategies.Count == 0)
            return new FtmoGroupCandidatesDto(tradingAccountId, FtmoGroupSimulationLimits.MaxMembers, []);

        var strategyIds = strategies.Select(s => s.Id).ToList();
        var runs = (await db.BacktestRuns.AsNoTracking()
                .Where(r => strategyIds.Contains(r.StrategyId))
                .Select(r => new { r.Id, r.StrategyId, r.Kind, r.Symbol })
                .ToListAsync(ct))
            .Select(r => new HeldRun(r.Id, r.StrategyId, r.Kind, r.Symbol))
            .ToList();

        var aggregates = new Dictionary<Guid, TradeAggregate>();
        var specBySymbol = new Dictionary<string, FtmoInstrumentSpec>(StringComparer.OrdinalIgnoreCase);
        var calibrationBySymbol = new Dictionary<string, SymbolCalibration>(StringComparer.OrdinalIgnoreCase);

        if (runs.Count > 0)
        {
            var runIds = runs.Select(r => r.RunId).ToList();
            aggregates = (await db.BacktestTrades.AsNoTracking()
                    .Where(t => runIds.Contains(t.BacktestRunId))
                    .GroupBy(t => t.BacktestRunId)
                    .Select(g => new { RunId = g.Key, Count = g.Count(), FirstOpen = g.Min(t => t.OpenTime), LastClose = g.Max(t => t.CloseTime) })
                    .ToListAsync(ct))
                .ToDictionary(a => a.RunId, a => new TradeAggregate(a.RunId, a.Count, a.FirstOpen, a.LastClose));

            var symbols = runs
                .Select(r => r.Symbol)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var specs = await db.FtmoInstrumentSpecs.AsNoTracking().Where(s => symbols.Contains(s.SqxSymbol)).ToListAsync(ct);
            var calibrations = await db.SymbolCalibrations.AsNoTracking().Where(c => symbols.Contains(c.Symbol)).ToListAsync(ct);

            foreach (var spec in specs)
                specBySymbol.TryAdd(spec.SqxSymbol, spec);
            foreach (var calibration in calibrations)
                calibrationBySymbol.TryAdd(calibration.Symbol, calibration);
        }

        // Same pick as the simulation (first run of a kind), so a candidate shows the run the simulation will use.
        var heldByStrategy = runs
            .GroupBy(r => r.StrategyId)
            .ToDictionary(g => g.Key, g => g.GroupBy(r => r.Kind).ToDictionary(k => k.Key, k => k.First()));

        FtmoGroupCandidateRunDto? ToDto(HeldRun? run)
        {
            if (run is null)
                return null;

            var spec = run.Symbol is { } symbol ? specBySymbol.GetValueOrDefault(symbol) : null;
            var calibration = run.Symbol is { } name ? calibrationBySymbol.GetValueOrDefault(name) : null;
            var resolution = FtmoSimulationInputs.ResolveSymbol(spec, calibration, fxLow: null, fxHigh: null);
            var hasSpec = resolution.Refusal != FtmoSimulationRefusal.InstrumentSpecMissing;
            aggregates.TryGetValue(run.RunId, out var aggregate);

            return new FtmoGroupCandidateRunDto(
                run.RunId,
                run.Symbol,
                aggregate?.Count ?? 0,
                aggregate?.FirstOpen,
                aggregate?.LastClose,
                hasSpec,
                IsCalibrated: hasSpec && resolution.Refusal != FtmoSimulationRefusal.PointValueNotCalibrated,
                hasSpec ? spec!.ProfitCurrency : null,
                NeedsFxBand: hasSpec && !FtmoSimulationInputs.SettlesInAccountCurrency(spec!),
                hasSpec ? spec!.SourceTimeZoneId : null);
        }

        var candidates = strategies
            .Select(s =>
            {
                var held = heldByStrategy.GetValueOrDefault(s.Id);
                return new FtmoGroupCandidateDto(
                    s.Id,
                    s.Name,
                    s.Symbol,
                    ToDto(held?.GetValueOrDefault(BacktestRunKind.Deploy)),
                    ToDto(held?.GetValueOrDefault(BacktestRunKind.Evaluation)),
                    s.NameExistsOnOtherAccount);
            })
            .ToList();

        return new FtmoGroupCandidatesDto(tradingAccountId, FtmoGroupSimulationLimits.MaxMembers, candidates);
    }
}
