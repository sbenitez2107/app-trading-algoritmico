namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-group-simulation B3 (design.md D6) — one member's contribution over the WHOLE window (not per start:
/// per-start nets over overlapping suffixes recount the same trades). Nets sum Net over the in-window rows at
/// each FX end; an Unscalable row (null Net) adds nothing and is counted in <see cref="Unscalable"/>.
/// </summary>
public sealed record FtmoGroupMemberContributionDto(
    Guid StrategyId,
    string Name,
    int InWindowTrades,
    int ScalableTrades,
    decimal NetLow,
    decimal NetHigh,
    int RaisedToMinimum,
    int CappedAtMaximum,
    int Unscalable);

/// <summary>
/// One member's share of the first-breach attribution. A start is credited to EVERY member with a trade closing
/// at the deciding breach's close instant, so a tied start appears under each of them (the per-phase counts and
/// <see cref="SharedCloseStarts"/> are therefore not additive across members).
/// </summary>
public sealed record FtmoGroupMemberAttributionDto(
    Guid StrategyId,
    string Name,
    int Phase1Starts,
    int Phase2Starts,
    int FundedStarts,
    int SoleContributorStarts,
    int SharedCloseStarts);

/// <summary>
/// First-breach attribution for one kind. <see cref="DecidingBreachStarts"/> = the sole-contributor starts of
/// every member + <see cref="SharedCloseStarts"/> + <see cref="UnattributedStarts"/> (which is 0 unless the
/// merged series has no trade at a breach close, a data defect worth seeing). A start with no breach is
/// credited to nobody and counted nowhere here.
/// </summary>
public sealed record FtmoGroupAttributionDto(
    int DecidingBreachStarts,
    int SharedCloseStarts,
    int UnattributedStarts,
    IReadOnlyList<FtmoGroupMemberAttributionDto> Members);

/// <summary>
/// Peak number of simultaneously open positions over the in-window scalable rows with <c>Close &gt; Open</c>.
/// <see cref="FirstReachedSource"/> and <see cref="MemberIdsAtPeak"/> are null/empty when the peak is 0.
/// </summary>
public sealed record FtmoGroupPeakConcurrencyDto(
    int PeakConcurrentOpen,
    DateTime? FirstReachedSource,
    IReadOnlyList<Guid> MemberIdsAtPeak);

/// <summary>The group diagnostics of one SUCCESSFUL kind (design.md D6); a refused kind carries none.</summary>
public sealed record FtmoGroupDiagnosticsDto(
    IReadOnlyList<FtmoGroupMemberContributionDto> Contributions,
    FtmoGroupAttributionDto Attribution,
    FtmoGroupPeakConcurrencyDto Peak);
