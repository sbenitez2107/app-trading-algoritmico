using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// ftmo-multi-start PR2, task 2.3 (design.md Decision 7) — nearest-rank order statistics over an
/// integer sample (days to some event). <c>N</c> is the observation count; every quantile is
/// <c>null</c> only when <c>N</c> is 0.
/// <para>
/// <strong>Apply-time deviation (flagged, not silent):</strong> design.md's File Changes table places
/// this file's whole DTO group (<see cref="FtmoMultiStartDto"/> and its siblings) in PR4, alongside
/// <c>FtmoMultiStartReadService.cs</c>. This one record is pulled forward into PR2 instead, because
/// <c>FtmoOrderStatistics.Compute</c> (task 2.3.4) is spec'd to return exactly this shape and this
/// record has no dependency on the PR4 service, DI, controller, or any other PR4 file — pulling it
/// forward changes no PR4 file count or scope. Confirm before PR4 starts that PR4 simply adds its
/// sibling records (<c>FtmoMultiStartDto</c>, <c>FtmoMultiStartRunDto</c>, etc.) to this same file
/// rather than re-declaring <see cref="FtmoOrderStatisticsDto"/>.
/// </para>
/// </summary>
/// <param name="N">Observation count.</param>
/// <param name="Min">Nearest-rank p=0 (the smallest observed value); <c>null</c> only when <c>N</c> is 0.</param>
/// <param name="Q1">Nearest-rank p=0.25.</param>
/// <param name="Median">Nearest-rank p=0.5.</param>
/// <param name="Q3">Nearest-rank p=0.75.</param>
/// <param name="Max">Nearest-rank p=1 (the largest observed value).</param>
public sealed record FtmoOrderStatisticsDto(int N, int? Min, int? Q1, int? Median, int? Q3, int? Max);

/// <summary>
/// ftmo-multi-start PR4 (design.md Interfaces/Contracts) — the funded phase's per-start result. Mirrors
/// <see cref="FtmoFundedPhase.FundedResult"/> field-for-field (Infrastructure-internal, not exposed
/// across the Application boundary): outcome, its own start/outcome timing, the breaching limit/class,
/// and elapsed days from BOTH the funded start and this start's own chain start (spec.md "Funded
/// Duration Is Reported From Both The Funded Start And The Chain Start").
/// </summary>
public sealed record FtmoFundedPhaseDto(
    FtmoFundedOutcome Outcome,
    DateTime? StartSourceOpen,
    DateTime? OutcomeSourceClose,
    FtmoFirstBreachingLimit? BreachLimit,
    FtmoBreachPointClass? BreachPointClass,
    int? CalendarDaysFromFundedStart,
    int? FtmoTradingDaysFromFundedStart,
    int? CalendarDaysFromChainStart,
    int? FtmoTradingDaysFromChainStart);

/// <summary>
/// ftmo-multi-start PR4 — one start's whole three-phase chain row (design.md Interfaces/Contracts).
/// <see cref="Outcome"/> is exactly one of the six chain outcomes (spec.md "Six Chain Outcomes, Three
/// Right-Censored And Labelled As Such"); <see cref="IsCensored"/> is true for the three right-censored
/// outcomes and <see cref="RunwayCalendarDays"/> carries that censored phase's own elapsed days (never a
/// fabricated cutoff). <see cref="CalendarDaysToBothTargets"/> is the calendar-day span from this
/// start's own phase-1 anchor to phase 2's target close, present only when phase 2 reaches its target.
/// </summary>
public sealed record FtmoMultiStartRowDto(
    int Index,
    DateTime StartSourceOpen,
    DateOnly StartFtmoDay,
    DateOnly FtmoMonth,
    FtmoChainOutcome Outcome,
    bool IsCensored,
    int? RunwayCalendarDays,
    int? CalendarDaysToBothTargets,
    FtmoChallengePhaseDto Phase1,
    FtmoChallengePhaseDto Phase2,
    FtmoFundedPhaseDto Funded,
    FtmoFxBandEnd FxBandEnd,
    bool FxRoundingSensitive);

/// <summary>
/// ftmo-multi-start PR4 (design.md Decision 6 / spec.md "Aggregates Are Counts, Shares, And Order
/// Statistics") — one chain outcome's count and share over the FULL start count, zeros included.
/// <see cref="IsCensored"/> mirrors <see cref="FtmoMultiStartRowDto.IsCensored"/> for the same outcome so
/// the UI never needs a separate lookup.
/// </summary>
public sealed record FtmoOutcomeCountDto(FtmoChainOutcome Outcome, bool IsCensored, int Count, decimal Share);

/// <summary>
/// ftmo-multi-start PR4 (design.md Decision 6) — the run-level aggregate: all six outcome counts/shares
/// (zeros included), the FX-rounding-sensitive count, and nearest-rank order statistics for days to
/// phase-1 target, days to phase-2 target, days to both targets, funded days to breach (from both
/// anchors), and censored runway. Never a mean, never a "within N days" bucket (spec.md).
/// </summary>
public sealed record FtmoMultiStartSummaryDto(
    int StartCount,
    IReadOnlyList<FtmoOutcomeCountDto> Outcomes,
    int FxRoundingSensitiveCount,
    FtmoOrderStatisticsDto DaysToPhase1Target,
    FtmoOrderStatisticsDto DaysToPhase2Target,
    FtmoOrderStatisticsDto DaysToBothTargets,
    FtmoOrderStatisticsDto FundedDaysToBreachFromFundedStart,
    FtmoOrderStatisticsDto FundedDaysToBreachFromChainStart,
    FtmoOrderStatisticsDto CensoredRunway);

/// <summary>
/// ftmo-multi-start PR4 — one held run's multi-start result (design.md Interfaces/Contracts). Reuses
/// <see cref="FtmoSimulationStatus"/>/<see cref="FtmoSimulationRefusal"/> for the whole-run refusal path
/// and <see cref="FtmoChallengeRaceRefusal"/> for the race-only refusal (<c>ProfitTargetMismatch</c>),
/// exactly like the single-start <see cref="FtmoRunSimulationResultDto"/> — this is the SAME guard
/// chain, reused (design.md Decision 8), never a second, divergent refusal vocabulary.
/// <para>
/// <see cref="Start1DiffersFromSingleStartAnchor"/> discloses when start 1 (first scalable trade of the
/// first month) differs from the single-start endpoint's own anchor, which includes <c>Unscalable</c>
/// rows (spec.md "Start 1 May Diverge From The Single-Start Anchor..."). <see cref="Summary"/> is null
/// only when there are no starts to summarize (an empty <see cref="Starts"/> list).
/// </para>
/// </summary>
public sealed record FtmoMultiStartRunDto(
    Guid RunId,
    BacktestRunKind Kind,
    BacktestSegment Segment,
    FtmoSimulationStatus Status,
    FtmoSimulationRefusal? Refusal,
    FtmoChallengeRaceRefusal? RaceRefusal,
    decimal? StoredProfitTargetPct,
    FtmoStartGrain Grain,
    FtmoChallengeRulesDto Rules,
    IReadOnlyList<FtmoMultiStartRowDto> Starts,
    FtmoMultiStartSummaryDto? Summary,
    IReadOnlyList<DateOnly> MonthsWithoutStart,
    bool Start1DiffersFromSingleStartAnchor,
    decimal? FxLow,
    decimal? FxHigh,
    int UnscalableCount,
    IReadOnlyList<string> NotModelled,
    string Disclosure);

/// <summary>Root result: one <see cref="FtmoMultiStartRunDto"/> per held run, never merged (mirrors <see cref="FtmoBreachSimulationDto"/>).</summary>
public sealed record FtmoMultiStartDto(Guid StrategyId, IReadOnlyList<FtmoMultiStartRunDto> Runs);
