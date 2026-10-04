using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-group-simulation B2 (design.md D5) — the request BODY of <c>POST api/ftmo-simulations/group</c>.
/// Every scalar is nullable so a missing field is distinguishable from an explicit <c>0</c>
/// (<c>SizeDecimals = 0</c> is a legal whole-lot grid); the controller turns a missing required field
/// into a 400 and builds a <see cref="FtmoGroupSimulationParameters"/>. Carries no symbol: each member's
/// symbol is resolved from its own run. Plain JSON, so a later "save as portfolio" can reuse it.
/// </summary>
public sealed record FtmoGroupSimulationRequest(
    IReadOnlyList<Guid>? MemberStrategyIds,
    string? Broker,
    decimal? InitialCapital,
    decimal? TargetRiskPerTrade,
    decimal? FxLow,
    decimal? FxHigh,
    int? SizeDecimals,
    decimal? Step,
    decimal? MinLot,
    decimal? MaxLots);

/// <summary>
/// The validated, non-null form of <see cref="FtmoGroupSimulationRequest"/> that the read service takes.
/// A present-but-unusable value (non-positive capital or risk, an invalid source grid) is NOT rejected
/// here: the service refuses it as <see cref="FtmoGroupRefusal.InvalidRequest"/>, like the single endpoints.
/// </summary>
public sealed record FtmoGroupSimulationParameters(
    IReadOnlyList<Guid> MemberStrategyIds,
    string Broker,
    decimal InitialCapital,
    decimal TargetRiskPerTrade,
    decimal? FxLow,
    decimal? FxHigh,
    int SizeDecimals,
    decimal Step,
    decimal MinLot,
    decimal MaxLots)
{
    /// <summary>The declared SOURCE grid, or null when the four fields do not describe a valid one.</summary>
    public LotGrid? TryBuildSourceGrid()
    {
        try
        {
            return new LotGrid(SizeDecimals, Step, MinLot, MaxLots);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

/// <summary>
/// One member in effect, echoed in <c>memberOrder</c> (ascending <c>StrategyId</c>). The currency, zone and
/// FX band come from the member's own symbol resolution and are null when that did not resolve (no run, or
/// a symbol-level refusal). <see cref="SourceTimeZoneId"/> lets a <see cref="FtmoGroupRefusal.MixedSourceTimeZones"/>
/// refusal list each member with its zone.
/// </summary>
public sealed record FtmoGroupMemberDto(
    Guid StrategyId,
    string Name,
    int MemberOrder,
    string? ProfitCurrency,
    string? SourceTimeZoneId,
    decimal? FxLow,
    decimal? FxHigh);

/// <summary>Two or more DISTINCT members sharing a name (case-insensitive): likely one strategy imported on two accounts. Never refused or merged.</summary>
public sealed record FtmoGroupNameWarningDto(string Name, IReadOnlyList<Guid> StrategyIds);

/// <summary>
/// One failing member of a refused kind. <see cref="RunReason"/> is non-null only when
/// <see cref="Reason"/> is <see cref="FtmoGroupRefusal.MemberRunRefused"/> (the shipped per-run reason).
/// </summary>
public sealed record FtmoGroupMemberRefusalDto(
    Guid StrategyId,
    string Name,
    FtmoGroupRefusal Reason,
    FtmoSimulationRefusal? RunReason);

public sealed record FtmoGroupWindowDto(DateTime Start, DateTime End);

/// <summary>One member's coverage of a kind: its first open, last close (over ALL its rows) and in-window row count.</summary>
public sealed record FtmoGroupMemberCoverageDto(
    Guid StrategyId,
    string Name,
    DateTime? FirstOpen,
    DateTime? LastClose,
    int InWindowTrades);

/// <summary>
/// One kind's result. A refused kind carries <see cref="Refusal"/> and no <see cref="Run"/>. A successful
/// kind's <see cref="Run"/> is the shipped <see cref="FtmoMultiStartRunDto"/> unchanged, with an empty
/// <c>RunId</c> (a merged series is not a held run).
/// </summary>
public sealed record FtmoGroupKindResultDto(
    BacktestRunKind Kind,
    FtmoSimulationStatus Status,
    FtmoGroupRefusal? Refusal,
    IReadOnlyList<FtmoGroupMemberRefusalDto> MemberRefusals,
    FtmoGroupWindowDto? Window,
    IReadOnlyList<FtmoGroupMemberCoverageDto> Coverage,
    FtmoMultiStartRunDto? Run);

/// <summary>
/// The response envelope. <see cref="Status"/> is <see cref="FtmoSimulationStatus.Refused"/> only for a
/// group-wide refusal (then <see cref="Kinds"/> is empty); member-level problems live in <see cref="Kinds"/>.
/// <see cref="UnknownStrategyIds"/> names the ids behind <see cref="FtmoGroupRefusal.MemberNotFound"/>.
/// <see cref="Disclosures"/> always carries the three group texts.
/// </summary>
public sealed record FtmoGroupSimulationDto(
    FtmoSimulationStatus Status,
    FtmoGroupRefusal? Refusal,
    FtmoSimulationRefusal? SharedRefusal,
    decimal? DailyLossLimitPct,
    decimal? MaxLossLimitPct,
    IReadOnlyList<FtmoGroupMemberDto> Members,
    IReadOnlyList<Guid> DuplicateIdsRemoved,
    IReadOnlyList<FtmoGroupNameWarningDto> DuplicateNameWarnings,
    IReadOnlyList<Guid> UnknownStrategyIds,
    IReadOnlyList<FtmoGroupKindResultDto> Kinds,
    IReadOnlyList<string> Disclosures);
