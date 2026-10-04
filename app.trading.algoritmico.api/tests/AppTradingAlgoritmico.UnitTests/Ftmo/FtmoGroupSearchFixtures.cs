using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupComputation;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupMemberResolution;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoTradeProjector;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1b — test-only support for the pure funnel tests: hand-built members and cached projections,
/// no database. Closed-form ids and instants, no <see cref="Random"/>.
/// </summary>
internal static class FtmoGroupSearchFixtures
{
    internal static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    internal static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>One strategy of the pool. A null series means "no run of that kind".</summary>
    internal sealed record Strat(
        int N,
        string Symbol,
        IReadOnlyList<ProjectedTrade>? Deploy,
        IReadOnlyList<ProjectedTrade>? Eval,
        FtmoSimulationRefusal? SymbolRefusal = null,
        FtmoSimulationRefusal? ProjectionRefusal = null,
        decimal HighNetScale = 1m);

    /// <summary>Ids ascend with <paramref name="n"/> (Guid comparison starts at the first field).</summary>
    internal static Guid Id(int n) => new(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    internal static DateTime At(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    internal static ProjectedTrade Trade(int row, DateTime open, DateTime close, decimal? net) =>
        new(row, open, close, net, net is null ? ResizeOutcome.Unscalable : ResizeOutcome.OnTarget, net is null ? 0m : 1m);

    /// <summary>A strategy whose Evaluation series differs from its Deploy series (every net is one higher).</summary>
    internal static Strat Distinct(int n, string symbol, params ProjectedTrade[] deploy) =>
        new(n, symbol, deploy, deploy.Select(t => t with { Net = t.Net + 1m }).ToList());

    internal static FtmoSimulationInputs.SymbolResolution Resolution(
        FtmoSimulationRefusal? refusal = null, FtmoSimulationRefusal? zoneRefusal = null) =>
        new(refusal, null, null, 0m, (1m, 1m), zoneRefusal, zoneRefusal is null ? Jerusalem : null);

    /// <summary>Pool-wide parameters: Jerusalem source, Berlin clock, the given capital and daily allowance fraction.</summary>
    internal static GroupParams Params(decimal capital = 10_000m, decimal dailyPct = 0.05m) =>
        new(Jerusalem, Berlin, capital, dailyPct, 0.10m, null, (1m, 1m),
            new AppTradingAlgoritmico.Application.DTOs.Backtests.FtmoChallengeRulesDto(
                FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct, FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null));

    internal static FtmoProjectionCache Cache(params Strat[] pool)
    {
        var members = new List<Member>();
        var inputs = new Dictionary<(Guid, BacktestRunKind), GroupMemberKindInput>();
        foreach (var s in pool)
        {
            var id = Id(s.N);
            var runs = new Dictionary<BacktestRunKind, MemberRun>();
            foreach (var (kind, rows) in new[] { (BacktestRunKind.Deploy, s.Deploy), (BacktestRunKind.Evaluation, s.Eval) })
            {
                if (rows is null)
                {
                    inputs[(id, kind)] = new GroupMemberKindInput(id, $"S{s.N}", 0, null, null, null);
                    continue;
                }

                var runId = Id(1000 + (s.N * 10) + (int)kind);
                runs[kind] = new MemberRun(runId, kind, s.Symbol);
                var projection = s.ProjectionRefusal is { } refusal
                    ? new FtmoSimulationInputs.RunProjection(refusal, BacktestSegment.Unknown, null, null, 0, 0, 0)
                    : new FtmoSimulationInputs.RunProjection(null, BacktestSegment.Unknown, [.. rows], [.. rows.Select(t => t with { Net = t.Net * s.HighNetScale })], 0, 0, 0);
                inputs[(id, kind)] = new GroupMemberKindInput(id, $"S{s.N}", 0, runId, s.SymbolRefusal, projection);
            }

            members.Add(new Member(id, $"S{s.N}", 0, runs, s.SymbolRefusal is { } r ? Resolution(r) : null, null));
        }

        return FtmoProjectionCache.Create(members, inputs, Jerusalem, Berlin);
    }
}
