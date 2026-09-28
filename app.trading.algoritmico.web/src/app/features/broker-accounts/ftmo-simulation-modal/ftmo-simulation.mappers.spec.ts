import { describe, expect, it } from 'vitest';
import {
  FtmoBreachPointClass,
  FtmoChainOutcome,
  FtmoChallengePhaseDto,
  FtmoChallengeRaceRefusal,
  FtmoChallengeRulesDto,
  FtmoFirstBreachingLimit,
  FtmoFundedOutcome,
  FtmoFundedPhaseDto,
  FtmoFxBandEnd,
  FtmoMultiStartRunDto,
  FtmoMultiStartSummaryDto,
  FtmoOrderStatisticsDto,
  FtmoPhaseOutcome,
  FtmoSimulationRefusal,
  FtmoSimulationStatus,
  FtmoStartGrain,
} from '../../../core/models/ftmo-simulation.model';
import { BacktestRunKind, BacktestSegment } from '../../../core/services/backtest.service';
import {
  CHAIN_OUTCOME_LABELS,
  CHAIN_OUTCOME_ORDER,
  FIRST_BREACHING_LIMIT_LABELS,
  FUNDED_OUTCOME_LABELS,
  BREACH_POINT_CLASS_LABELS,
  FX_BAND_END_LABELS,
  FtmoOrderStatRowVm,
  PHASE_OUTCOME_LABELS,
  RACE_REFUSAL_LABELS,
  REFUSAL_LABELS,
  START_GRAIN_LABELS,
  STATUS_LABELS,
  labelFor,
  toPanels,
  toRunPanelVm,
} from './ftmo-simulation.mappers';

const NOT_STARTED_PHASE: FtmoChallengePhaseDto = {
  outcome: FtmoPhaseOutcome.NotStarted,
  startSourceOpen: null,
  firstTargetTouchSourceClose: null,
  minTradingDaysMetFtmoDay: null,
  outcomeSourceClose: null,
  breachLimit: null,
  breachPointClass: null,
  calendarDaysElapsed: null,
  ftmoTradingDaysElapsed: null,
  fxBandEnd: null,
};

const NOT_STARTED_FUNDED: FtmoFundedPhaseDto = {
  outcome: FtmoFundedOutcome.NotStarted,
  startSourceOpen: null,
  outcomeSourceClose: null,
  breachLimit: null,
  breachPointClass: null,
  calendarDaysFromFundedStart: null,
  ftmoTradingDaysFromFundedStart: null,
  calendarDaysFromChainStart: null,
  ftmoTradingDaysFromChainStart: null,
};

const RULES: FtmoChallengeRulesDto = {
  phase1TargetPct: 0.1,
  phase2TargetPct: 0.05,
  minTradingDaysPerPhase: 4,
  timeLimitDays: null,
};

const EMPTY_STATS: FtmoOrderStatisticsDto = {
  n: 0,
  min: null,
  q1: null,
  median: null,
  q3: null,
  max: null,
};

function baseRun(overrides: Partial<FtmoMultiStartRunDto>): FtmoMultiStartRunDto {
  return {
    runId: 'run-1',
    kind: BacktestRunKind.Deploy,
    segment: BacktestSegment.OutOfSample,
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    raceRefusal: null,
    storedProfitTargetPct: null,
    grain: FtmoStartGrain.Monthly,
    rules: RULES,
    starts: [],
    summary: null,
    monthsWithoutStart: [],
    start1DiffersFromSingleStartAnchor: false,
    fxLow: null,
    fxHigh: null,
    unscalableCount: 0,
    notModelled: [],
    disclosure: 'disclosure text',
    ...overrides,
  };
}

describe('label accessors — exact-value switch, zero-safe (AD4, hard rule 2)', () => {
  it('Refused (0) renders its label, not UNKNOWN_VALUE', () => {
    expect(labelFor(STATUS_LABELS, FtmoSimulationStatus.Refused)).toEqual({
      key: 'FTMO_SIMULATION.STATUS.REFUSED',
    });
  });

  it('InvalidRequest (0) renders its label', () => {
    expect(labelFor(REFUSAL_LABELS, FtmoSimulationRefusal.InvalidRequest)).toEqual({
      key: 'FTMO_SIMULATION.REFUSAL.INVALID_REQUEST',
    });
  });

  it('ProfitTargetMismatch (0) renders its label', () => {
    expect(labelFor(RACE_REFUSAL_LABELS, FtmoChallengeRaceRefusal.ProfitTargetMismatch)).toEqual({
      key: 'FTMO_SIMULATION.RACE_REFUSAL.PROFIT_TARGET_MISMATCH',
    });
  });

  it('Phase1UndecidedAtEndOfData (0) renders its label', () => {
    expect(labelFor(CHAIN_OUTCOME_LABELS, FtmoChainOutcome.Phase1UndecidedAtEndOfData)).toEqual({
      key: 'FTMO_SIMULATION.CHAIN_OUTCOME.PHASE1_UNDECIDED',
    });
  });

  it('FtmoPhaseOutcome.NotStarted (0) renders its label', () => {
    expect(labelFor(PHASE_OUTCOME_LABELS, FtmoPhaseOutcome.NotStarted)).toEqual({
      key: 'FTMO_SIMULATION.PHASE_OUTCOME.NOT_STARTED',
    });
  });

  it('FtmoFundedOutcome.NotStarted (0) renders its label', () => {
    expect(labelFor(FUNDED_OUTCOME_LABELS, FtmoFundedOutcome.NotStarted)).toEqual({
      key: 'FTMO_SIMULATION.FUNDED_OUTCOME.NOT_STARTED',
    });
  });

  it('Daily (0) renders its label', () => {
    expect(labelFor(FIRST_BREACHING_LIMIT_LABELS, FtmoFirstBreachingLimit.Daily)).toEqual({
      key: 'FTMO_SIMULATION.FIRST_BREACHING_LIMIT.DAILY',
    });
  });

  it('Clean (0) renders its label', () => {
    expect(labelFor(BREACH_POINT_CLASS_LABELS, FtmoBreachPointClass.Clean)).toEqual({
      key: 'FTMO_SIMULATION.BREACH_POINT_CLASS.CLEAN',
    });
  });

  it('FxLow (0) renders its label', () => {
    expect(labelFor(FX_BAND_END_LABELS, FtmoFxBandEnd.FxLow)).toEqual({
      key: 'FTMO_SIMULATION.FX_BAND_END.FX_LOW',
    });
  });

  it('Monthly (0) renders its label', () => {
    expect(labelFor(START_GRAIN_LABELS, FtmoStartGrain.Monthly)).toEqual({
      key: 'FTMO_SIMULATION.START_GRAIN.MONTHLY',
    });
  });

  it('a value with no matching label renders UNKNOWN_VALUE with the raw number, never another member label', () => {
    const result = labelFor(STATUS_LABELS, 999 as FtmoSimulationStatus);
    expect(result).toEqual({ key: 'FTMO_SIMULATION.UNKNOWN_VALUE', value: 999 });
    expect(result?.key).not.toBe('FTMO_SIMULATION.STATUS.REFUSED');
    expect(result?.key).not.toBe('FTMO_SIMULATION.STATUS.EVALUATED');
  });

  it('a null or undefined enum value returns null, not a label', () => {
    expect(labelFor(STATUS_LABELS, null)).toBeNull();
    expect(labelFor(STATUS_LABELS, undefined)).toBeNull();
  });
});

describe('toRunPanelVm / toPanels', () => {
  it('a Refused run maps to state "refused" with no outcome shares or order stats', () => {
    const run = baseRun({
      status: FtmoSimulationStatus.Refused,
      refusal: FtmoSimulationRefusal.InvalidRequest,
    });
    const vm = toRunPanelVm(run);
    expect(vm.state).toBe('refused');
    expect('outcomeRows' in vm).toBe(false);
    expect('orderStatRows' in vm).toBe(false);
  });

  it('InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand map to three distinct message keys', () => {
    const keys = [
      FtmoSimulationRefusal.InstrumentSpecMissing,
      FtmoSimulationRefusal.FxRateNotDeclared,
      FtmoSimulationRefusal.InvalidFxBand,
    ].map((refusal) => {
      const vm = toRunPanelVm(baseRun({ status: FtmoSimulationStatus.Refused, refusal }));
      return vm.state === 'refused' ? vm.refusalKey : null;
    });
    expect(new Set(keys).size).toBe(3);
  });

  it('a race refusal of ProfitTargetMismatch carries the stored value through to the VM', () => {
    const run = baseRun({
      raceRefusal: FtmoChallengeRaceRefusal.ProfitTargetMismatch,
      storedProfitTargetPct: 0.08,
      summary: null,
    });
    const vm = toRunPanelVm(run);
    expect(vm.state).toBe('raceRefused');
    expect(vm.state === 'raceRefused' && vm.storedProfitTargetPct).toBe(0.08);
  });

  it('a summary with no starts maps to state "noStarts"', () => {
    const run = baseRun({ summary: null });
    const vm = toRunPanelVm(run);
    expect(vm.state).toBe('noStarts');
  });

  const summaryWithZeroCount: FtmoMultiStartSummaryDto = {
    startCount: 10,
    outcomes: [
      { outcome: FtmoChainOutcome.Phase1Breached, isCensored: false, count: 2, share: 0.2 },
      {
        outcome: FtmoChainOutcome.Phase1UndecidedAtEndOfData,
        isCensored: false,
        count: 1,
        share: 0.1,
      },
      { outcome: FtmoChainOutcome.Phase2Breached, isCensored: false, count: 3, share: 0.3 },
      {
        outcome: FtmoChainOutcome.Phase2UndecidedAtEndOfData,
        isCensored: false,
        count: 2,
        share: 0.2,
      },
      { outcome: FtmoChainOutcome.FundedBreached, isCensored: false, count: 0, share: 0 },
      {
        outcome: FtmoChainOutcome.FundedNoBreachAtEndOfData,
        isCensored: false,
        count: 2,
        share: 0.2,
      },
    ],
    fxRoundingSensitiveCount: 0,
    daysToPhase1Target: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    daysToPhase2Target: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    daysToBothTargets: { n: 5, min: 1, q1: 2, median: 3, q3: 4, max: 5 },
    fundedDaysToBreachFromFundedStart: { n: 3, min: 10, q1: 12, median: 15, q3: 18, max: 20 },
    fundedDaysToBreachFromChainStart: { n: 3, min: 40, q1: 42, median: 45, q3: 48, max: 50 },
    censoredRunway: EMPTY_STATS,
  };

  it('an evaluated run renders all six outcome rows including zero-count ones', () => {
    const run = baseRun({ summary: summaryWithZeroCount });
    const vm = toRunPanelVm(run);
    expect(vm.state).toBe('evaluated');
    if (vm.state !== 'evaluated') throw new Error('expected evaluated state');
    expect(vm.outcomeRows.length).toBe(6);
    const fundedBreachedRow = vm.outcomeRows.find(
      (r) => r.outcome === FtmoChainOutcome.FundedBreached,
    );
    expect(fundedBreachedRow).toEqual(
      expect.objectContaining({ outcome: FtmoChainOutcome.FundedBreached, count: 0, share: 0 }),
    );
  });

  it('an order-stat row with N=0 renders every quantile as absent, not zero', () => {
    const run = baseRun({ summary: summaryWithZeroCount });
    const vm = toRunPanelVm(run);
    if (vm.state !== 'evaluated') throw new Error('expected evaluated state');
    const censoredRow = vm.orderStatRows.find((r: FtmoOrderStatRowVm) =>
      r.labelKey.includes('CENSORED_RUNWAY'),
    );
    expect(censoredRow).toBeDefined();
    expect(censoredRow?.min).toBeNull();
    expect(censoredRow?.q1).toBeNull();
    expect(censoredRow?.median).toBeNull();
    expect(censoredRow?.q3).toBeNull();
    expect(censoredRow?.max).toBeNull();
  });

  it('an order-stat row with Min=0 and N>0 renders "0", not "—"', () => {
    const withZeroMin: FtmoMultiStartSummaryDto = {
      ...summaryWithZeroCount,
      daysToPhase1Target: { n: 4, min: 0, q1: 1, median: 2, q3: 3, max: 4 },
    };
    const run = baseRun({ summary: withZeroMin });
    const vm = toRunPanelVm(run);
    if (vm.state !== 'evaluated') throw new Error('expected evaluated state');
    const row = vm.orderStatRows.find((r: FtmoOrderStatRowVm) =>
      r.labelKey.includes('PHASE1_TARGET'),
    );
    expect(row?.min).toBe(0);
  });

  it('the funded-duration headline uses FundedDaysToBreachFromFundedStart, with FromChainStart shown as secondary', () => {
    const run = baseRun({ summary: summaryWithZeroCount });
    const vm = toRunPanelVm(run);
    if (vm.state !== 'evaluated') throw new Error('expected evaluated state');
    const headline = vm.orderStatRows.find((r: FtmoOrderStatRowVm) =>
      r.labelKey.includes('FUNDED_FROM_FUNDED_START'),
    );
    const secondary = vm.orderStatRows.find((r: FtmoOrderStatRowVm) =>
      r.labelKey.includes('FUNDED_FROM_CHAIN_START'),
    );
    expect(headline?.median).toBe(15);
    expect(headline?.secondary).toBeFalsy();
    expect(secondary?.median).toBe(45);
    expect(secondary?.secondary).toBe(true);
  });

  it('months without a start are listed with their count, never dropped', () => {
    const run = baseRun({ monthsWithoutStart: ['2024-01', '2024-02', '2024-03'] });
    const vm = toRunPanelVm(run);
    expect(vm.monthsWithoutStart).toEqual(['2024-01', '2024-02', '2024-03']);
    expect(vm.monthsWithoutStartCount).toBe(3);
  });

  it('a "yyyy-MM" month string is sliced, never parsed with new Date', () => {
    const run = baseRun({ monthsWithoutStart: ['2024-01'] });
    const vm = toRunPanelVm(run);
    // A raw slice keeps "2024-01" unmodified; new Date('2024-01-01') at UTC-3 would render December 2023.
    expect(vm.monthsWithoutStart[0]).toBe('2024-01');
    expect(vm.monthsWithoutStart[0]).not.toContain('2023');
  });

  it('Start1DiffersFromSingleStartAnchor=true surfaces an explicit disclosure flag in the VM', () => {
    const run = baseRun({ start1DiffersFromSingleStartAnchor: true });
    const vm = toRunPanelVm(run);
    expect(vm.start1Differs).toBe(true);
  });

  it('the server Disclosure and NotModelled text pass through verbatim, never looked up as an i18n key', () => {
    const run = baseRun({
      disclosure: 'Arbitrary server text — not a key',
      notModelled: ['spread widening'],
    });
    const vm = toRunPanelVm(run);
    expect(vm.disclosure).toBe('Arbitrary server text — not a key');
    expect(vm.notModelled).toEqual(['spread widening']);
  });

  it('toPanels maps every run in the DTO, and returns [] for a null DTO', () => {
    expect(toPanels(null)).toEqual([]);
    const dto = {
      strategyId: 'strat-1',
      runs: [baseRun({}), baseRun({ kind: BacktestRunKind.Evaluation })],
    };
    expect(toPanels(dto).length).toBe(2);
  });
});

// re-export used only to keep CHAIN_OUTCOME_ORDER's import path exercised (see fixed-order builder)
void CHAIN_OUTCOME_ORDER;
