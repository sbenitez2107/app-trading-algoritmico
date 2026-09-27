using AppTradingAlgoritmico.Domain.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Application.DTOs.Backtests;

/// <summary>
/// One breach-simulation request for a strategy (PR P4 — design.md Data Flow). The FTMO symbol
/// mapping and lot grid are NOT declared here: design.md Decision 4 homes the FTMO contract facts
/// (contract size, currency, min/step/max, source zone) in the persisted <c>FtmoInstrumentSpec</c>
/// row keyed by <see cref="SqxSymbol"/>, captured once with provenance — never re-declared per
/// request and never inferred by string similarity (spec.md "FTMO Symbol Mapping Is Declared,
/// Never Inferred" / "FTMO Lot Grid Is A Required Caller Input"). A request naming a
/// <see cref="SqxSymbol"/> with no matching row is refused with
/// <see cref="FtmoSimulationRefusal.InstrumentSpecMissing"/> — never a substituted grid, never a
/// fuzzy match.
/// </summary>
/// <param name="StrategyId">The strategy whose held runs (Deploy/Evaluation) are simulated — one result per run, never merged.</param>
/// <param name="Broker">The <c>BrokerRiskLimits.Broker</c> row to read the FTMO product/limits from.</param>
/// <param name="SqxSymbol">The verbatim SQX symbol whose <c>FtmoInstrumentSpec</c> row supplies the FTMO contract facts.</param>
/// <param name="InitialCapital">The FTMO account's starting balance.</param>
/// <param name="TargetRiskPerTrade">The FTMO-side target risk per trade — the sizing decision.</param>
/// <param name="FxLow">USD-per-unit low end of the declared FX band. Null only valid when the symbol settles in USD.</param>
/// <param name="FxHigh">USD-per-unit high end of the declared FX band.</param>
/// <param name="SizeDecimals">Declared SOURCE lot-grid decimals (the backtest's own grid, for <c>TradeRiskNormalizer</c>). REQUIRED, never defaulted — the four grid values are the caller's declaration and are validated only by <see cref="TryBuildSourceGrid"/>.</param>
/// <param name="Step">Declared source lot-grid step.</param>
/// <param name="MinLot">Declared source lot-grid minimum.</param>
/// <param name="MaxLots">Declared source lot-grid maximum.</param>
public sealed record FtmoBreachSimulationRequest(
    Guid StrategyId,
    string Broker,
    string SqxSymbol,
    decimal InitialCapital,
    decimal TargetRiskPerTrade,
    decimal? FxLow,
    decimal? FxHigh,
    int SizeDecimals,
    decimal Step,
    decimal MinLot,
    decimal MaxLots)
{
    /// <summary>
    /// The declared SOURCE grid, or null when the four fields do not describe a valid one
    /// (<see cref="LotGrid"/>'s constructor validates rather than clamps).
    /// </summary>
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
/// One limit's (daily or max loss) three-state finding, mapped from the internal
/// <c>FtmoBreachEvaluator.FtmoLimitFinding</c> (Infrastructure-internal, not exposed across the
/// Application boundary) into a public shape. Never a boolean — spec.md's Three-State Finding requirement.
/// </summary>
public sealed record FtmoLimitFindingDto(
    FtmoBreachVerdict Verdict,
    IReadOnlyList<BreachContingencyCause> Causes,
    string DisclosureText)
{
    /// <summary>The limit's first breaching close's timing (ftmo-first-breach-timing). Null when the limit was never breached.</summary>
    public required FtmoBreachTimingDto? FirstBreach { get; init; }

    /// <summary>The limit's first CLEAN breaching close's timing. Null when no clean breach exists (or the limit was never breached).</summary>
    public required FtmoBreachTimingDto? FirstCleanBreach { get; init; }
}

/// <summary>
/// One breaching close's first-breach timing (ftmo-first-breach-timing, design.md Decision 1, 8).
/// <see cref="PointClass"/> is derived when mapping (<c>Causes.Count == 0 ? Clean : Contingent</c>) —
/// there is one source of truth, so the class and the causes cannot disagree.
/// </summary>
public sealed record FtmoBreachTimingDto(
    DateTime SourceCloseTime,
    DateOnly FtmoTradingDay,
    decimal BalanceAfterClose,
    decimal FloorLevel,
    FtmoBreachPointClass PointClass,
    IReadOnlyList<BreachContingencyCause> Causes,
    int FtmoTradingDaysElapsed,
    int CalendarDaysElapsed,
    FtmoFxBandEnd FxBandEnd);

/// <summary>
/// Which limit broke first, per run (ftmo-first-breach-timing, design.md Decision 7). No nested
/// <c>Timing</c> field: with <see cref="FtmoFirstBreachingLimit.BothSameClose"/>, one <c>Timing</c>
/// would have to pick one limit's floor, causes and class, which contradicts the two limits' findings
/// staying independent.
/// </summary>
public sealed record FtmoFirstLimitBreachDto(
    FtmoFirstBreachingLimit Limit,
    DateTime SourceCloseTime,
    DateOnly FtmoTradingDay,
    int FtmoTradingDaysElapsed,
    int CalendarDaysElapsed);

/// <summary>
/// One run's (Deploy or Evaluation, i.e. one segment) simulation outcome. A <see cref="FtmoSimulationStatus.Refused"/>
/// result carries no findings (design.md Decision 6) — <see cref="Daily"/>/<see cref="Max"/> are null
/// and the resize counts are zero. <see cref="NotModelled"/> and <see cref="EmbeddedCommissionDisclosure"/>
/// are carried regardless of status (spec.md's disclosure requirements).
/// </summary>
public sealed record FtmoRunSimulationResultDto(
    Guid RunId,
    BacktestRunKind Kind,
    BacktestSegment Segment,
    FtmoSimulationStatus Status,
    FtmoSimulationRefusal? Refusal,
    FtmoLimitFindingDto? Daily,
    FtmoLimitFindingDto? Max,
    int RaisedToMinimumCount,
    int CappedAtMaximumCount,
    int UnscalableCount,
    decimal? FxLow,
    decimal? FxHigh,
    IReadOnlyList<string> NotModelled,
    string EmbeddedCommissionDisclosure)
{
    /// <summary>Swap, FTMO commission timing and intraday-equity replay are never modelled (spec.md).</summary>
    public static readonly IReadOnlyList<string> DefaultNotModelled = ["Swap", "FtmoCommission", "IntradayEquity"];

    /// <summary>
    /// Discloses that the source backtest's <c>Profit</c> already embeds the configured commission,
    /// rescaled together with the P/L — not absent (design.md Corrections #2).
    /// </summary>
    public const string DefaultEmbeddedCommissionDisclosure =
        "Swap/overnight financing is not modelled. The source backtest's Profit already embeds the "
        + "configured commission, rescaled together with the P/L during resizing — it is not absent.";

    /// <summary>Which limit broke first, and when (ftmo-first-breach-timing). Null when neither limit was breached, or the run was refused.</summary>
    public required FtmoFirstLimitBreachDto? FirstLimitBreach { get; init; }

    /// <summary>The replay-start anchor's source instant (ftmo-first-breach-timing). Null on a refused run.</summary>
    public required DateTime? ReplayStartSourceTime { get; init; }

    /// <summary>The replay-start anchor's FTMO trading day (ftmo-first-breach-timing). Null on a refused run.</summary>
    public required DateOnly? ReplayStartFtmoDay { get; init; }
}

/// <summary>Root result: one <see cref="FtmoRunSimulationResultDto"/> per held run, never merged (spec.md IS/OOS requirement).</summary>
public sealed record FtmoBreachSimulationDto(
    Guid StrategyId,
    IReadOnlyList<FtmoRunSimulationResultDto> Runs);
