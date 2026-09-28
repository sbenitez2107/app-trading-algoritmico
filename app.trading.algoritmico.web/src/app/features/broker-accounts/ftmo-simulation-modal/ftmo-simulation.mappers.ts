import { BacktestRunKind } from '../../../core/services/backtest.service';
import {
  FtmoBreachPointClass,
  FtmoChainOutcome,
  FtmoChallengeRaceRefusal,
  FtmoFirstBreachingLimit,
  FtmoFundedOutcome,
  FtmoFxBandEnd,
  FtmoMultiStartDto,
  FtmoMultiStartRunDto,
  FtmoOrderStatisticsDto,
  FtmoPhaseOutcome,
  FtmoSimulationRefusal,
  FtmoSimulationStatus,
  FtmoStartGrain,
} from '../../../core/models/ftmo-simulation.model';

/**
 * Per-run view-model state discriminant (design.md AD3). The template switches on this STRING, so
 * no template can ever test a `0` enum value for truthiness.
 */
export type FtmoRunPanelState = 'refused' | 'raceRefused' | 'noStarts' | 'evaluated';

/** An i18n label reference: `value` is present only for the UNKNOWN_VALUE fallback (design.md AD4). */
export interface EnumLabel {
  key: string;
  value?: number;
}

const UNKNOWN_VALUE_KEY = 'FTMO_SIMULATION.UNKNOWN_VALUE';

/**
 * The shared label accessor (design.md AD4): `x === null || x === undefined ? null : MAP[x] ??
 * UNKNOWN_KEY`. A zero-valued member is looked up by exact key presence, never by truthiness — this
 * is the seam hard rule 2 requires a dedicated test for, per zero-valued enum member.
 */
export function labelFor<T extends number>(
  map: Record<T, string>,
  value: T | null | undefined,
): EnumLabel | null {
  if (value === null || value === undefined) {
    return null;
  }
  const key = map[value];
  return key !== undefined ? { key } : { key: UNKNOWN_VALUE_KEY, value };
}

export const STATUS_LABELS: Record<FtmoSimulationStatus, string> = {
  [FtmoSimulationStatus.Refused]: 'FTMO_SIMULATION.STATUS.REFUSED',
  [FtmoSimulationStatus.Evaluated]: 'FTMO_SIMULATION.STATUS.EVALUATED',
};

/** Each refusal keeps its own message — InstrumentSpecMissing/FxRateNotDeclared/InvalidFxBand are
 * never collapsed into a shared generic refusal message (spec.md "A Whole-Run Refusal..."). */
export const REFUSAL_LABELS: Record<FtmoSimulationRefusal, string> = {
  [FtmoSimulationRefusal.InvalidRequest]: 'FTMO_SIMULATION.REFUSAL.INVALID_REQUEST',
  [FtmoSimulationRefusal.ProductNotTwoStep]: 'FTMO_SIMULATION.REFUSAL.PRODUCT_NOT_TWO_STEP',
  [FtmoSimulationRefusal.LimitsNotConfigured]: 'FTMO_SIMULATION.REFUSAL.LIMITS_NOT_CONFIGURED',
  [FtmoSimulationRefusal.DrawdownModelNotStatic]:
    'FTMO_SIMULATION.REFUSAL.DRAWDOWN_MODEL_NOT_STATIC',
  [FtmoSimulationRefusal.InstrumentSpecMissing]: 'FTMO_SIMULATION.REFUSAL.INSTRUMENT_SPEC_MISSING',
  [FtmoSimulationRefusal.PointValueNotCalibrated]:
    'FTMO_SIMULATION.REFUSAL.POINT_VALUE_NOT_CALIBRATED',
  [FtmoSimulationRefusal.FxRateNotDeclared]: 'FTMO_SIMULATION.REFUSAL.FX_RATE_NOT_DECLARED',
  [FtmoSimulationRefusal.InvalidFxBand]: 'FTMO_SIMULATION.REFUSAL.INVALID_FX_BAND',
  [FtmoSimulationRefusal.RiskNotEstimable]: 'FTMO_SIMULATION.REFUSAL.RISK_NOT_ESTIMABLE',
  [FtmoSimulationRefusal.RunSegmentsDisagree]: 'FTMO_SIMULATION.REFUSAL.RUN_SEGMENTS_DISAGREE',
  [FtmoSimulationRefusal.TimeZoneDataUnavailable]:
    'FTMO_SIMULATION.REFUSAL.TIME_ZONE_DATA_UNAVAILABLE',
};

export const RACE_REFUSAL_LABELS: Record<FtmoChallengeRaceRefusal, string> = {
  [FtmoChallengeRaceRefusal.ProfitTargetMismatch]:
    'FTMO_SIMULATION.RACE_REFUSAL.PROFIT_TARGET_MISMATCH',
};

/** Fixed rendering order for the six outcome-share rows (spec.md "All six outcomes are always present"). */
export const CHAIN_OUTCOME_ORDER: FtmoChainOutcome[] = [
  FtmoChainOutcome.Phase1Breached,
  FtmoChainOutcome.Phase1UndecidedAtEndOfData,
  FtmoChainOutcome.Phase2Breached,
  FtmoChainOutcome.Phase2UndecidedAtEndOfData,
  FtmoChainOutcome.FundedBreached,
  FtmoChainOutcome.FundedNoBreachAtEndOfData,
];

export const CHAIN_OUTCOME_LABELS: Record<FtmoChainOutcome, string> = {
  [FtmoChainOutcome.Phase1UndecidedAtEndOfData]: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_UNDECIDED',
  [FtmoChainOutcome.Phase1Breached]: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_BREACHED',
  [FtmoChainOutcome.Phase2Breached]: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE2_BREACHED',
  [FtmoChainOutcome.Phase2UndecidedAtEndOfData]: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE2_UNDECIDED',
  [FtmoChainOutcome.FundedBreached]: 'FTMO_SIMULATION.CHAIN_OUTCOME.FUNDED_BREACHED',
  [FtmoChainOutcome.FundedNoBreachAtEndOfData]: 'FTMO_SIMULATION.CHAIN_OUTCOME.FUNDED_NO_BREACH',
};

export const PHASE_OUTCOME_LABELS: Record<FtmoPhaseOutcome, string> = {
  [FtmoPhaseOutcome.NotStarted]: 'FTMO_SIMULATION.PHASE_OUTCOME.NOT_STARTED',
  [FtmoPhaseOutcome.TargetReachedFirst]: 'FTMO_SIMULATION.PHASE_OUTCOME.TARGET_REACHED_FIRST',
  [FtmoPhaseOutcome.BreachedFirst]: 'FTMO_SIMULATION.PHASE_OUTCOME.BREACHED_FIRST',
  [FtmoPhaseOutcome.NeitherByEndOfData]: 'FTMO_SIMULATION.PHASE_OUTCOME.NEITHER_BY_END',
};

export const FUNDED_OUTCOME_LABELS: Record<FtmoFundedOutcome, string> = {
  [FtmoFundedOutcome.NotStarted]: 'FTMO_SIMULATION.FUNDED_OUTCOME.NOT_STARTED',
  [FtmoFundedOutcome.BreachedFirst]: 'FTMO_SIMULATION.FUNDED_OUTCOME.BREACHED_FIRST',
  [FtmoFundedOutcome.NoBreachByEndOfData]: 'FTMO_SIMULATION.FUNDED_OUTCOME.NO_BREACH_BY_END',
};

export const FIRST_BREACHING_LIMIT_LABELS: Record<FtmoFirstBreachingLimit, string> = {
  [FtmoFirstBreachingLimit.Daily]: 'FTMO_SIMULATION.FIRST_BREACHING_LIMIT.DAILY',
  [FtmoFirstBreachingLimit.Max]: 'FTMO_SIMULATION.FIRST_BREACHING_LIMIT.MAX',
  [FtmoFirstBreachingLimit.BothSameClose]: 'FTMO_SIMULATION.FIRST_BREACHING_LIMIT.BOTH_SAME_CLOSE',
};

export const BREACH_POINT_CLASS_LABELS: Record<FtmoBreachPointClass, string> = {
  [FtmoBreachPointClass.Clean]: 'FTMO_SIMULATION.BREACH_POINT_CLASS.CLEAN',
  [FtmoBreachPointClass.Contingent]: 'FTMO_SIMULATION.BREACH_POINT_CLASS.CONTINGENT',
};

export const FX_BAND_END_LABELS: Record<FtmoFxBandEnd, string> = {
  [FtmoFxBandEnd.FxLow]: 'FTMO_SIMULATION.FX_BAND_END.FX_LOW',
  [FtmoFxBandEnd.FxHigh]: 'FTMO_SIMULATION.FX_BAND_END.FX_HIGH',
  [FtmoFxBandEnd.BothEnds]: 'FTMO_SIMULATION.FX_BAND_END.BOTH_ENDS',
};

export const START_GRAIN_LABELS: Record<FtmoStartGrain, string> = {
  [FtmoStartGrain.Monthly]: 'FTMO_SIMULATION.START_GRAIN.MONTHLY',
};

export interface FtmoOutcomeRowVm {
  outcome: FtmoChainOutcome;
  labelKey: string;
  count: number;
  share: number;
  isCensored: boolean;
}

/** `null` renders "—" (absent); `0` renders "0" (design.md Data Flow). Never conflated. */
export interface FtmoOrderStatRowVm {
  labelKey: string;
  n: number;
  min: number | null;
  q1: number | null;
  median: number | null;
  q3: number | null;
  max: number | null;
  secondary?: boolean;
}

interface FtmoRunPanelBaseVm {
  kind: BacktestRunKind;
  disclosure: string;
  notModelled: string[];
  monthsWithoutStart: string[];
  monthsWithoutStartCount: number;
  start1Differs: boolean;
  fxLow: number | null;
  fxHigh: number | null;
  unscalableCount: number;
}

export type FtmoRunPanelVm =
  | (FtmoRunPanelBaseVm & { state: 'refused'; refusalKey: string })
  | (FtmoRunPanelBaseVm & {
      state: 'raceRefused';
      raceRefusalKey: string;
      storedProfitTargetPct: number | null;
    })
  | (FtmoRunPanelBaseVm & { state: 'noStarts' })
  | (FtmoRunPanelBaseVm & {
      state: 'evaluated';
      outcomeRows: FtmoOutcomeRowVm[];
      orderStatRows: FtmoOrderStatRowVm[];
    });

/** A `null` stat renders "—" (absent); `0` renders "0" — the raw value passes through unchanged. */
function statRow(
  labelKey: string,
  stats: FtmoOrderStatisticsDto,
  secondary?: boolean,
): FtmoOrderStatRowVm {
  return {
    labelKey,
    n: stats.n,
    min: stats.n === 0 ? null : stats.min,
    q1: stats.n === 0 ? null : stats.q1,
    median: stats.n === 0 ? null : stats.median,
    q3: stats.n === 0 ? null : stats.q3,
    max: stats.n === 0 ? null : stats.max,
    ...(secondary ? { secondary: true } : {}),
  };
}

function baseFields(run: FtmoMultiStartRunDto): FtmoRunPanelBaseVm {
  return {
    kind: run.kind,
    // The server's Disclosure/NotModelled text is authoritative data, shown verbatim as-is — it is
    // never looked up as, or expected to resolve to, an i18n key (design.md AD11).
    disclosure: run.disclosure,
    notModelled: run.notModelled,
    monthsWithoutStart: run.monthsWithoutStart,
    monthsWithoutStartCount: run.monthsWithoutStart.length,
    start1Differs: run.start1DiffersFromSingleStartAnchor,
    fxLow: run.fxLow,
    fxHigh: run.fxHigh,
    unscalableCount: run.unscalableCount,
  };
}

/**
 * Maps one run DTO to its panel VM. State priority (design.md Data Flow): `status === Refused` →
 * `refused`; else `raceRefusal !== null` → `raceRefused`; else `summary === null` → `noStarts`; else
 * `evaluated`. Presence checks use `!==`, never truthiness (hard rule 2).
 */
export function toRunPanelVm(run: FtmoMultiStartRunDto): FtmoRunPanelVm {
  const base = baseFields(run);

  if (run.status === FtmoSimulationStatus.Refused) {
    const label = labelFor(REFUSAL_LABELS, run.refusal);
    return { ...base, state: 'refused', refusalKey: label?.key ?? UNKNOWN_VALUE_KEY };
  }

  if (run.raceRefusal !== null && run.raceRefusal !== undefined) {
    const label = labelFor(RACE_REFUSAL_LABELS, run.raceRefusal);
    return {
      ...base,
      state: 'raceRefused',
      raceRefusalKey: label?.key ?? UNKNOWN_VALUE_KEY,
      storedProfitTargetPct: run.storedProfitTargetPct,
    };
  }

  if (run.summary === null) {
    return { ...base, state: 'noStarts' };
  }

  const summary = run.summary;
  const countsByOutcome = new Map(summary.outcomes.map((o) => [o.outcome, o]));
  const outcomeRows: FtmoOutcomeRowVm[] = CHAIN_OUTCOME_ORDER.map((outcome) => {
    const found = countsByOutcome.get(outcome);
    const label = labelFor(CHAIN_OUTCOME_LABELS, outcome);
    return {
      outcome,
      labelKey: label?.key ?? UNKNOWN_VALUE_KEY,
      count: found?.count ?? 0,
      share: found?.share ?? 0,
      isCensored: found?.isCensored ?? false,
    };
  });

  const orderStatRows: FtmoOrderStatRowVm[] = [
    statRow('FTMO_SIMULATION.ORDER_STATS.ROW.PHASE1_TARGET', summary.daysToPhase1Target),
    statRow('FTMO_SIMULATION.ORDER_STATS.ROW.PHASE2_TARGET', summary.daysToPhase2Target),
    statRow('FTMO_SIMULATION.ORDER_STATS.ROW.BOTH_TARGETS', summary.daysToBothTargets),
    statRow(
      'FTMO_SIMULATION.ORDER_STATS.ROW.FUNDED_FROM_FUNDED_START',
      summary.fundedDaysToBreachFromFundedStart,
    ),
    statRow(
      'FTMO_SIMULATION.ORDER_STATS.ROW.FUNDED_FROM_CHAIN_START',
      summary.fundedDaysToBreachFromChainStart,
      true,
    ),
    statRow('FTMO_SIMULATION.ORDER_STATS.ROW.CENSORED_RUNWAY', summary.censoredRunway),
  ];

  return { ...base, state: 'evaluated', outcomeRows, orderStatRows };
}

/** Container entry point (design.md `panels = computed(() => toPanels(result()))`). */
export function toPanels(dto: FtmoMultiStartDto | null): FtmoRunPanelVm[] {
  if (dto === null) {
    return [];
  }
  return dto.runs.map(toRunPanelVm);
}
