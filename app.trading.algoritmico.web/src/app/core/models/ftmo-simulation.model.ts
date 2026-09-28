import { BacktestRunKind, BacktestSegment } from '../services/backtest.service';

/**
 * Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoSimulationStatus` (numeric, no
 * `JsonStringEnumConverter` is registered anywhere in the API — confirmed in `Program.cs`). `Refused`
 * is `0`, the CLR default, so a default-initialized status must never be treated as absent.
 */
export enum FtmoSimulationStatus {
  Refused = 0,
  Evaluated = 1,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoSimulationRefusal`. `InvalidRequest` is `0`. */
export enum FtmoSimulationRefusal {
  InvalidRequest = 0,
  ProductNotTwoStep = 1,
  LimitsNotConfigured = 2,
  DrawdownModelNotStatic = 3,
  InstrumentSpecMissing = 4,
  PointValueNotCalibrated = 5,
  FxRateNotDeclared = 6,
  InvalidFxBand = 7,
  RiskNotEstimable = 8,
  RunSegmentsDisagree = 9,
  TimeZoneDataUnavailable = 10,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoChallengeRaceRefusal`. `ProfitTargetMismatch` is `0`. */
export enum FtmoChallengeRaceRefusal {
  ProfitTargetMismatch = 0,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoChainOutcome`. `Phase1UndecidedAtEndOfData` is `0`. */
export enum FtmoChainOutcome {
  Phase1UndecidedAtEndOfData = 0,
  Phase1Breached = 1,
  Phase2Breached = 2,
  Phase2UndecidedAtEndOfData = 3,
  FundedBreached = 4,
  FundedNoBreachAtEndOfData = 5,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoPhaseOutcome`. `NotStarted` is `0`. */
export enum FtmoPhaseOutcome {
  NotStarted = 0,
  TargetReachedFirst = 1,
  BreachedFirst = 2,
  NeitherByEndOfData = 3,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoFundedOutcome`. `NotStarted` is `0`. */
export enum FtmoFundedOutcome {
  NotStarted = 0,
  BreachedFirst = 1,
  NoBreachByEndOfData = 2,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoFirstBreachingLimit`. `Daily` is `0`. */
export enum FtmoFirstBreachingLimit {
  Daily = 0,
  Max = 1,
  BothSameClose = 2,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoBreachPointClass`. `Clean` is `0`. */
export enum FtmoBreachPointClass {
  Clean = 0,
  Contingent = 1,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoFxBandEnd`. `FxLow` is `0`. */
export enum FtmoFxBandEnd {
  FxLow = 0,
  FxHigh = 1,
  BothEnds = 2,
}

/** Mirrors `AppTradingAlgoritmico.Domain.Enums.FtmoStartGrain`. `Monthly` is `0`, the only member. */
export enum FtmoStartGrain {
  Monthly = 0,
}

/**
 * The four SOURCE (SQX backtest) lot-grid query params. This is a hardcoded frontend constant that
 * mirrors `LotGrid.ImoxRetester` (`Domain/Backtests/LotGrid.cs:71`) — sizeDecimals 2, step 0.01,
 * minLot 0.01, maxLots 10. This is deliberately NOT the FTMO grid: the FTMO grid has maxLots=1000 for
 * XAUUSD (migration `20260925122543`), and is read by the backend itself from `FtmoInstrumentSpec`.
 * Prefilling the request's source grid from the FTMO grid would declare the wrong grid (design.md
 * "Resolved: the lot grid hazard"). Labelled in the UI as the backtest (IMOX retester) lot grid,
 * never as the FTMO grid.
 */
export const IMOX_RETESTER_LOT_GRID = {
  sizeDecimals: 2,
  step: 0.01,
  minLot: 0.01,
  maxLots: 10,
} as const;

/** Mirrors `Application.DTOs.Backtests.FtmoOrderStatisticsDto`. Every quantile is null only when N is 0. */
export interface FtmoOrderStatisticsDto {
  n: number;
  min: number | null;
  q1: number | null;
  median: number | null;
  q3: number | null;
  max: number | null;
}

/**
 * Mirrors `Application.DTOs.Backtests.FtmoChallengeRulesDto` (declared in `FtmoBreachSimulationDto.cs`)
 * — the fixed FTMO 2-Step ruleset. `timeLimitDays` is always null on the wire: no time limit is
 * modelled for either phase. Shared by the multi-start run and, from PR2a, the single-start race.
 */
export interface FtmoChallengeRulesDto {
  phase1TargetPct: number;
  phase2TargetPct: number;
  minTradingDaysPerPhase: number;
  timeLimitDays: number | null;
}

/**
 * Mirrors `Application.DTOs.Backtests.FtmoChallengePhaseDto` (declared in `FtmoBreachSimulationDto.cs`)
 * — one challenge/verification phase's timing. A `NotStarted` phase carries every field null except
 * `outcome`. `minTradingDaysMetFtmoDay` is a C# `DateOnly` (`yyyy-MM-dd`); the other instants are
 * `DateTime`. Shared by the multi-start row and, from PR2a, the single-start race.
 */
export interface FtmoChallengePhaseDto {
  outcome: FtmoPhaseOutcome;
  startSourceOpen: string | null;
  firstTargetTouchSourceClose: string | null;
  minTradingDaysMetFtmoDay: string | null;
  outcomeSourceClose: string | null;
  breachLimit: FtmoFirstBreachingLimit | null;
  breachPointClass: FtmoBreachPointClass | null;
  calendarDaysElapsed: number | null;
  ftmoTradingDaysElapsed: number | null;
  fxBandEnd: FtmoFxBandEnd | null;
}

/** Mirrors `Application.DTOs.Backtests.FtmoFundedPhaseDto`. */
export interface FtmoFundedPhaseDto {
  outcome: FtmoFundedOutcome;
  startSourceOpen: string | null;
  outcomeSourceClose: string | null;
  breachLimit: FtmoFirstBreachingLimit | null;
  breachPointClass: FtmoBreachPointClass | null;
  calendarDaysFromFundedStart: number | null;
  ftmoTradingDaysFromFundedStart: number | null;
  calendarDaysFromChainStart: number | null;
  ftmoTradingDaysFromChainStart: number | null;
}

/** Mirrors `Application.DTOs.Backtests.FtmoOutcomeCountDto`. Zeros included — never omitted. */
export interface FtmoOutcomeCountDto {
  outcome: FtmoChainOutcome;
  isCensored: boolean;
  count: number;
  share: number;
}

/** Mirrors `Application.DTOs.Backtests.FtmoMultiStartSummaryDto`. */
export interface FtmoMultiStartSummaryDto {
  startCount: number;
  outcomes: FtmoOutcomeCountDto[];
  fxRoundingSensitiveCount: number;
  daysToPhase1Target: FtmoOrderStatisticsDto;
  daysToPhase2Target: FtmoOrderStatisticsDto;
  daysToBothTargets: FtmoOrderStatisticsDto;
  fundedDaysToBreachFromFundedStart: FtmoOrderStatisticsDto;
  fundedDaysToBreachFromChainStart: FtmoOrderStatisticsDto;
  censoredRunway: FtmoOrderStatisticsDto;
}

/** Mirrors `Application.DTOs.Backtests.FtmoMultiStartRowDto`. Not consumed until PR1b's mappers. */
export interface FtmoMultiStartRowDto {
  index: number;
  startSourceOpen: string;
  startFtmoDay: string;
  ftmoMonth: string;
  outcome: FtmoChainOutcome;
  isCensored: boolean;
  runwayCalendarDays: number | null;
  calendarDaysToBothTargets: number | null;
  phase1: FtmoChallengePhaseDto;
  phase2: FtmoChallengePhaseDto;
  funded: FtmoFundedPhaseDto;
  fxBandEnd: FtmoFxBandEnd;
  fxRoundingSensitive: boolean;
}

/**
 * Mirrors `Application.DTOs.Backtests.FtmoMultiStartRunDto`. `kind`/`segment` reuse
 * `BacktestRunKind`/`BacktestSegment` from `backtest.service.ts` (design.md AD2) — redeclaring them
 * here would let the two drift.
 */
export interface FtmoMultiStartRunDto {
  runId: string;
  kind: BacktestRunKind;
  segment: BacktestSegment;
  status: FtmoSimulationStatus;
  refusal: FtmoSimulationRefusal | null;
  raceRefusal: FtmoChallengeRaceRefusal | null;
  storedProfitTargetPct: number | null;
  grain: FtmoStartGrain;
  rules: FtmoChallengeRulesDto;
  starts: FtmoMultiStartRowDto[];
  summary: FtmoMultiStartSummaryDto | null;
  monthsWithoutStart: string[];
  start1DiffersFromSingleStartAnchor: boolean;
  fxLow: number | null;
  fxHigh: number | null;
  unscalableCount: number;
  notModelled: string[];
  disclosure: string;
}

/** Mirrors `Application.DTOs.Backtests.FtmoMultiStartDto`, the root multi-start response. */
export interface FtmoMultiStartDto {
  strategyId: string;
  runs: FtmoMultiStartRunDto[];
}

/**
 * The query sent to `getMultiStart`/`getSingleStart` (design.md AD6). The 8 required fields mirror
 * `StrategyBacktestsController.TryValidateFtmoBreachQuery`'s required-parameter check exactly;
 * `fxLow`/`fxHigh` stay optional, per the same controller.
 */
export interface FtmoSimulationQuery {
  broker: string;
  sqxSymbol: string;
  initialCapital: number;
  targetRiskPerTrade: number;
  sizeDecimals: number;
  step: number;
  minLot: number;
  maxLots: number;
  fxLow?: number | null;
  fxHigh?: number | null;
}
