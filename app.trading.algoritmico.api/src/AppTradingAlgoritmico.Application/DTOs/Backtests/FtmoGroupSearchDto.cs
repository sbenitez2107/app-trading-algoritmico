using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-group-search D7 — the job status. The zero value is <see cref="Unknown"/>, so a default-constructed status
/// never reads as <see cref="Completed"/>. <see cref="StoppedAtBudget"/> is a distinct terminal status, never a
/// <see cref="Completed"/> with a flag.
/// </summary>
public enum FtmoGroupSearchStatus
{
    Unknown = 0,
    Running = 1,
    Completed = 2,
    StoppedAtBudget = 3,
    Cancelled = 4,
    Failed = 5,
}

/// <summary>The stage a running job reports. The zero value is <see cref="Unknown"/>.</summary>
public enum FtmoGroupSearchStage
{
    Unknown = 0,
    Loading = 1,
    Eligibility = 2,
    Proxy = 3,
    Simulating = 4,
    Ranking = 5,
}

/// <summary>Which budget ended the full computation early. A cancel is NOT a stop reason. The zero value is <see cref="Unknown"/>.</summary>
public enum FtmoGroupSearchStopReason
{
    Unknown = 0,
    None = 1,
    MaxFullSimulations = 2,
    WallClock = 3,
}

/// <summary>Why a strategy never entered the funnel. The zero value is <see cref="Unknown"/>, never a real reason.</summary>
public enum FtmoGroupSearchIneligibleReason
{
    Unknown = 0,
    MissingKind = 1,
    SymbolRefused = 2,
    ZoneUnresolved = 3,
    ProjectionRefused = 4,
    ProjectionRowless = 5,
    IdenticalDeployEval = 6,
}

/// <summary>
/// The request BODY of <c>POST api/ftmo-simulations/group-search</c>. Every scalar is nullable so a missing field is
/// distinguishable from an explicit <c>0</c> (<c>SizeDecimals = 0</c> is a legal whole-lot grid); the controller (2b)
/// validates it. Shares, headroom and the ceiling are fractions on the wire.
/// </summary>
public sealed record FtmoGroupSearchRequest(
    Guid? TradingAccountId = null,
    int? MinMembers = null,
    int? MaxMembers = null,
    int? MaxPerInstrument = null,
    bool? IncludeIdenticalDeployEval = null,
    bool? OnePercentRule = null,
    decimal? EliminationCeiling = null,
    string? Broker = null,
    decimal? InitialCapital = null,
    decimal? TargetRiskPerTrade = null,
    decimal? FxLow = null,
    decimal? FxHigh = null,
    int? SizeDecimals = null,
    decimal? Step = null,
    decimal? MinLot = null,
    decimal? MaxLots = null,
    int? MaxFullSimulations = null,
    int? MaxWallClockSeconds = null);

/// <summary>Candidate counts through the funnel. <see cref="Examined"/> is every candidate enumerated BEFORE any filtering.</summary>
public sealed record FtmoGroupSearchFunnelDto(
    int Examined,
    int RemovedByCap,
    int RemovedByPairConflict,
    int RemovedByOnePercentRule,
    int RemovedNoCommonWindow,
    int RemovedMemberHasNoTrades,
    int Shortlisted);

/// <summary>An immutable snapshot of a running job. <c>ElapsedMs</c> is stamped by the worker, never by the runner.</summary>
public sealed record FtmoGroupSearchProgressDto(
    FtmoGroupSearchStage Stage,
    int Processed,
    int Total,
    long ElapsedMs,
    int FullSimulationsDone,
    int MaxFullSimulations,
    FtmoGroupSearchFunnelDto Funnel);

public sealed record FtmoGroupSearchIneligibleDto(
    Guid StrategyId, string Name, FtmoGroupSearchIneligibleReason Reason, FtmoSimulationRefusal? Refusal);

/// <summary>One kind's own limit usage, unblended; fractions of the allowance (0.8 = 80% used).</summary>
public sealed record FtmoGroupSearchKindHeadroomDto(
    BacktestRunKind Kind, decimal WorstDailyUsed, decimal WorstMaxUsed, decimal MedianMaxUsed, decimal Headroom);

/// <param name="WithinCeiling">The breach share (worse kind) is at or under the ceiling: a highlight, never a ranking key.</param>
/// <param name="Symbols">The distinct instrument symbols of the members, ordinal order; the runner always fills it.</param>
public sealed record FtmoGroupSearchRowDto(
    int Rank,
    IReadOnlyList<Guid> MemberIds,
    IReadOnlyList<string> MemberNames,
    int PeakConcurrentOpen,
    bool IdenticalDeployEval,
    bool WithinCeiling,
    decimal Headroom,
    IReadOnlyList<FtmoGroupSearchKindHeadroomDto> KindHeadrooms,
    IReadOnlyList<FtmoGroupKindResultDto> Kinds,
    IReadOnlyList<string>? Symbols = null);

/// <summary>What a runner hands back: the engine outcome, already mapped to wire shapes.</summary>
/// <param name="Cancelled">The cancel token fired; <paramref name="Rows"/> holds what completed before it.</param>
public sealed record FtmoGroupSearchRunResult(
    bool Cancelled,
    FtmoGroupSearchStopReason StopReason,
    int NotComputed,
    int FullySimulated,
    FtmoGroupSearchFunnelDto Funnel,
    IReadOnlyList<FtmoGroupSearchIneligibleDto> Ineligible,
    IReadOnlyList<FtmoGroupSearchRowDto> Rows);

/// <summary>
/// One job in effect (design D7). <see cref="Disclosures"/> is ALWAYS present, including while running and when a
/// budget stopped it; it is lost on an API restart (a poll then answers 404). <see cref="Request"/> is the request as
/// submitted (fractions stay fractions), so a re-attached page can rebuild its deep link and ceiling.
/// </summary>
public sealed record FtmoGroupSearchJobDto(
    Guid JobId,
    FtmoGroupSearchStatus Status,
    FtmoGroupSearchProgressDto Progress,
    FtmoGroupSearchStopReason StopReason,
    int NotComputed,
    IReadOnlyList<FtmoGroupSearchIneligibleDto> Ineligible,
    IReadOnlyList<FtmoGroupSearchRowDto> Rows,
    IReadOnlyList<string> Disclosures,
    string? ErrorMessage,
    FtmoGroupSearchRequest Request);

/// <summary>The disclosure texts of a search result. No text affirms an outcome: it is elimination risk, not certification.</summary>
public static class FtmoGroupSearchDisclosures
{
    public static IReadOnlyList<string> Build(
        int examined, int shortlisted, int fullySimulated, IReadOnlyList<string> groupDisclosures)
        =>
        [
            $"{examined} groups examined, {shortlisted} shortlisted by the proxy and {fullySimulated} fully simulated. "
                + "Choosing the best of many backtested groups is optimistic (selection bias): the more groups examined, "
                + "the better the best one looks by chance alone.",
            "The figures are elimination risk on closed-trade daily aggregates, not a certification or a forecast.",
            .. groupDisclosures,
            "Results are not persisted: the job and its results are lost when the API restarts.",
        ];
}
