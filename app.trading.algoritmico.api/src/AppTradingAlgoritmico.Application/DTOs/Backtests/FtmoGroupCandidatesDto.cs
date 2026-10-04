namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-group-simulation B3 (design.md D8) — the facts the group picker needs, for ONE account. Carries the
/// member cap (<c>MaxMembers</c>) because the simulation envelope deliberately does not.
/// </summary>
public sealed record FtmoGroupCandidatesDto(
    Guid TradingAccountId,
    int MaxMembers,
    IReadOnlyList<FtmoGroupCandidateDto> Candidates);

/// <summary>
/// One strategy of the account. <see cref="Deploy"/> / <see cref="Evaluation"/> are null when the strategy
/// holds no run of that kind. <see cref="Symbol"/> is the strategy's own symbol, verbatim.
/// <see cref="NameExistsOnOtherAccount"/> is case-insensitive.
/// </summary>
public sealed record FtmoGroupCandidateDto(
    Guid StrategyId,
    string Name,
    string? Symbol,
    FtmoGroupCandidateRunDto? Deploy,
    FtmoGroupCandidateRunDto? Evaluation,
    bool NameExistsOnOtherAccount);

/// <summary>
/// One held run's facts. The flags are per run because the Deploy and Evaluation symbols could differ.
/// <see cref="HasInstrumentSpec"/> is true only for a USABLE spec (the simulation refuses an unusable one as
/// <c>InstrumentSpecMissing</c>); <see cref="IsCalibrated"/> mirrors the simulation's calibration rule (Calibrated,
/// positive point value) and, like the simulation, is only meaningful with a usable spec. The spec-derived fields
/// are null (and <see cref="NeedsFxBand"/> false) without one. <see cref="FirstOpen"/>/<see cref="LastClose"/> are null
/// for a run with no trades.
/// </summary>
public sealed record FtmoGroupCandidateRunDto(
    Guid RunId,
    string? Symbol,
    int TradeCount,
    DateTime? FirstOpen,
    DateTime? LastClose,
    bool HasInstrumentSpec,
    bool IsCalibrated,
    string? ProfitCurrency,
    bool NeedsFxBand,
    string? SourceTimeZoneId);
