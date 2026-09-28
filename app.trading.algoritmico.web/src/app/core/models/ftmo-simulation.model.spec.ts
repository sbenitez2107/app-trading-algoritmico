import {
  FtmoSimulationStatus,
  FtmoSimulationRefusal,
  FtmoChallengeRaceRefusal,
  FtmoChainOutcome,
  FtmoPhaseOutcome,
  FtmoFundedOutcome,
  FtmoFirstBreachingLimit,
  FtmoBreachPointClass,
  FtmoFxBandEnd,
  FtmoStartGrain,
  IMOX_RETESTER_LOT_GRID,
} from './ftmo-simulation.model';
import type {
  FtmoChallengePhaseDto,
  FtmoChallengeRulesDto,
  FtmoFundedPhaseDto,
  FtmoMultiStartDto,
  FtmoMultiStartRowDto,
  FtmoMultiStartRunDto,
  FtmoMultiStartSummaryDto,
  FtmoOrderStatisticsDto,
  FtmoOutcomeCountDto,
} from './ftmo-simulation.model';
import { BacktestRunKind, BacktestSegment } from '../services/backtest.service';

describe('FTMO_SIMULATION enums pin their exact numeric values', () => {
  it('FtmoSimulationStatus_PinsRefusedAsZero', () => {
    expect(FtmoSimulationStatus.Refused).toBe(0);
    expect(FtmoSimulationStatus.Evaluated).toBe(1);
  });

  it('FtmoSimulationRefusal_PinsInvalidRequestAsZero', () => {
    expect(FtmoSimulationRefusal.InvalidRequest).toBe(0);
    expect(FtmoSimulationRefusal.ProductNotTwoStep).toBe(1);
    expect(FtmoSimulationRefusal.LimitsNotConfigured).toBe(2);
    expect(FtmoSimulationRefusal.DrawdownModelNotStatic).toBe(3);
    expect(FtmoSimulationRefusal.InstrumentSpecMissing).toBe(4);
    expect(FtmoSimulationRefusal.PointValueNotCalibrated).toBe(5);
    expect(FtmoSimulationRefusal.FxRateNotDeclared).toBe(6);
    expect(FtmoSimulationRefusal.InvalidFxBand).toBe(7);
    expect(FtmoSimulationRefusal.RiskNotEstimable).toBe(8);
    expect(FtmoSimulationRefusal.RunSegmentsDisagree).toBe(9);
    expect(FtmoSimulationRefusal.TimeZoneDataUnavailable).toBe(10);
  });

  it('FtmoChallengeRaceRefusal_PinsProfitTargetMismatchAsZero', () => {
    expect(FtmoChallengeRaceRefusal.ProfitTargetMismatch).toBe(0);
  });

  it('FtmoChainOutcome_PinsPhase1UndecidedAtEndOfDataAsZero', () => {
    expect(FtmoChainOutcome.Phase1UndecidedAtEndOfData).toBe(0);
    expect(FtmoChainOutcome.Phase1Breached).toBe(1);
    expect(FtmoChainOutcome.Phase2Breached).toBe(2);
    expect(FtmoChainOutcome.Phase2UndecidedAtEndOfData).toBe(3);
    expect(FtmoChainOutcome.FundedBreached).toBe(4);
    expect(FtmoChainOutcome.FundedNoBreachAtEndOfData).toBe(5);
  });

  it('FtmoPhaseOutcome_PinsNotStartedAsZero', () => {
    expect(FtmoPhaseOutcome.NotStarted).toBe(0);
    expect(FtmoPhaseOutcome.TargetReachedFirst).toBe(1);
    expect(FtmoPhaseOutcome.BreachedFirst).toBe(2);
    expect(FtmoPhaseOutcome.NeitherByEndOfData).toBe(3);
  });

  it('FtmoFundedOutcome_PinsNotStartedAsZero', () => {
    expect(FtmoFundedOutcome.NotStarted).toBe(0);
    expect(FtmoFundedOutcome.BreachedFirst).toBe(1);
    expect(FtmoFundedOutcome.NoBreachByEndOfData).toBe(2);
  });

  it('FtmoFirstBreachingLimit_PinsDailyAsZero', () => {
    expect(FtmoFirstBreachingLimit.Daily).toBe(0);
    expect(FtmoFirstBreachingLimit.Max).toBe(1);
    expect(FtmoFirstBreachingLimit.BothSameClose).toBe(2);
  });

  it('FtmoBreachPointClass_PinsCleanAsZero', () => {
    expect(FtmoBreachPointClass.Clean).toBe(0);
    expect(FtmoBreachPointClass.Contingent).toBe(1);
  });

  it('FtmoFxBandEnd_PinsFxLowAsZero', () => {
    expect(FtmoFxBandEnd.FxLow).toBe(0);
    expect(FtmoFxBandEnd.FxHigh).toBe(1);
    expect(FtmoFxBandEnd.BothEnds).toBe(2);
  });

  it('FtmoStartGrain_PinsMonthlyAsZero', () => {
    expect(FtmoStartGrain.Monthly).toBe(0);
  });
});

describe('IMOX_RETESTER_LOT_GRID pins the backtest lot grid, never the FTMO grid', () => {
  it('IMOX_RETESTER_LOT_GRID_MatchesLotGridImoxRetester_NeverTheFtmoGrid', () => {
    // FTMO's own grid uses maxLots=1000 for XAUUSD (migration 20260925122543). This constant must
    // mirror LotGrid.ImoxRetester (LotGrid.cs:71) instead — sending the FTMO grid as the SOURCE
    // grid would declare the wrong grid (design.md "Resolved: the lot grid hazard").
    expect(IMOX_RETESTER_LOT_GRID).toEqual({
      sizeDecimals: 2,
      step: 0.01,
      minLot: 0.01,
      maxLots: 10,
    });
  });
});

// Wire-shape pin (design.md AD3). Every object literal below carries EVERY field of its C# record in
// `Application/DTOs/Backtests/FtmoMultiStartDto.cs` / `FtmoBreachSimulationDto.cs`, and is typed with
// no `as` cast. `tsc --build` (which type-checks `*.spec.ts` via `tsconfig.spec.json`) therefore fails
// both ways: a field missing from an interface is an excess-property error on the literal, and a field
// the interface requires but C# does not declare is a missing-property error.
const RULES: FtmoChallengeRulesDto = {
  phase1TargetPct: 0.1,
  phase2TargetPct: 0.05,
  minTradingDaysPerPhase: 4,
  timeLimitDays: null,
};

const PHASE1: FtmoChallengePhaseDto = {
  outcome: FtmoPhaseOutcome.TargetReachedFirst,
  startSourceOpen: '2024-01-02T09:00:00',
  firstTargetTouchSourceClose: '2024-01-20T15:30:00',
  minTradingDaysMetFtmoDay: '2024-01-05',
  outcomeSourceClose: '2024-01-20T15:30:00',
  breachLimit: null,
  breachPointClass: null,
  calendarDaysElapsed: 18,
  ftmoTradingDaysElapsed: 14,
  fxBandEnd: FtmoFxBandEnd.FxLow,
};

const PHASE2: FtmoChallengePhaseDto = {
  outcome: FtmoPhaseOutcome.TargetReachedFirst,
  startSourceOpen: '2024-01-22T09:00:00',
  firstTargetTouchSourceClose: '2024-02-10T11:00:00',
  minTradingDaysMetFtmoDay: '2024-01-25',
  outcomeSourceClose: '2024-02-10T11:00:00',
  breachLimit: null,
  breachPointClass: null,
  calendarDaysElapsed: 19,
  ftmoTradingDaysElapsed: 15,
  fxBandEnd: FtmoFxBandEnd.BothEnds,
};

const FUNDED: FtmoFundedPhaseDto = {
  outcome: FtmoFundedOutcome.BreachedFirst,
  startSourceOpen: '2024-02-12T09:00:00',
  outcomeSourceClose: '2024-03-01T16:00:00',
  breachLimit: FtmoFirstBreachingLimit.Daily,
  breachPointClass: FtmoBreachPointClass.Clean,
  calendarDaysFromFundedStart: 18,
  ftmoTradingDaysFromFundedStart: 14,
  calendarDaysFromChainStart: 59,
  ftmoTradingDaysFromChainStart: 43,
};

const ROW: FtmoMultiStartRowDto = {
  index: 1,
  startSourceOpen: '2024-01-02T09:00:00',
  startFtmoDay: '2024-01-02',
  ftmoMonth: '2024-01-01',
  outcome: FtmoChainOutcome.FundedBreached,
  isCensored: false,
  runwayCalendarDays: null,
  calendarDaysToBothTargets: 39,
  phase1: PHASE1,
  phase2: PHASE2,
  funded: FUNDED,
  fxBandEnd: FtmoFxBandEnd.FxLow,
  fxRoundingSensitive: false,
};

const STATS: FtmoOrderStatisticsDto = { n: 1, min: 18, q1: 18, median: 18, q3: 18, max: 18 };

const OUTCOME_COUNT: FtmoOutcomeCountDto = {
  outcome: FtmoChainOutcome.FundedBreached,
  isCensored: false,
  count: 1,
  share: 1,
};

const SUMMARY: FtmoMultiStartSummaryDto = {
  startCount: 1,
  outcomes: [OUTCOME_COUNT],
  fxRoundingSensitiveCount: 0,
  daysToPhase1Target: STATS,
  daysToPhase2Target: STATS,
  daysToBothTargets: STATS,
  fundedDaysToBreachFromFundedStart: STATS,
  fundedDaysToBreachFromChainStart: STATS,
  censoredRunway: { n: 0, min: null, q1: null, median: null, q3: null, max: null },
};

const RUN: FtmoMultiStartRunDto = {
  runId: '0b9f7a52-3c1e-4d7a-9a55-2f1c0e6b8d01',
  kind: BacktestRunKind.Deploy,
  segment: BacktestSegment.InSample,
  status: FtmoSimulationStatus.Evaluated,
  refusal: null,
  raceRefusal: null,
  storedProfitTargetPct: 0.1,
  grain: FtmoStartGrain.Monthly,
  rules: RULES,
  starts: [ROW],
  summary: SUMMARY,
  monthsWithoutStart: ['2024-02-01'],
  start1DiffersFromSingleStartAnchor: false,
  fxLow: null,
  fxHigh: null,
  unscalableCount: 0,
  notModelled: ['Swap', 'FtmoCommission', 'IntradayEquity'],
  disclosure: 'disclosure',
};

const MULTI_START: FtmoMultiStartDto = {
  strategyId: '655ef82d-20cc-4108-a1f5-a782587fca36',
  runs: [RUN],
};

describe('FTMO_SIMULATION wire DTOs mirror the C# records field for field', () => {
  // Runtime half of the pin: the fixture's key sets are the camelCased C# positional parameters, in
  // declaration order. The compile-time half is the typed literals above.
  it('FtmoMultiStartRunDto_CarriesEveryCSharpField_IncludingRules', () => {
    expect(Object.keys(MULTI_START)).toEqual(['strategyId', 'runs']);
    expect(Object.keys(MULTI_START.runs[0])).toEqual([
      'runId',
      'kind',
      'segment',
      'status',
      'refusal',
      'raceRefusal',
      'storedProfitTargetPct',
      'grain',
      'rules',
      'starts',
      'summary',
      'monthsWithoutStart',
      'start1DiffersFromSingleStartAnchor',
      'fxLow',
      'fxHigh',
      'unscalableCount',
      'notModelled',
      'disclosure',
    ]);
    expect(Object.keys(MULTI_START.runs[0].rules)).toEqual([
      'phase1TargetPct',
      'phase2TargetPct',
      'minTradingDaysPerPhase',
      'timeLimitDays',
    ]);
  });

  it('FtmoMultiStartRowDto_CarriesEveryCSharpField_IncludingPhase1Phase2AndFunded', () => {
    const row = MULTI_START.runs[0].starts[0];
    expect(Object.keys(row)).toEqual([
      'index',
      'startSourceOpen',
      'startFtmoDay',
      'ftmoMonth',
      'outcome',
      'isCensored',
      'runwayCalendarDays',
      'calendarDaysToBothTargets',
      'phase1',
      'phase2',
      'funded',
      'fxBandEnd',
      'fxRoundingSensitive',
    ]);
    const phaseKeys = [
      'outcome',
      'startSourceOpen',
      'firstTargetTouchSourceClose',
      'minTradingDaysMetFtmoDay',
      'outcomeSourceClose',
      'breachLimit',
      'breachPointClass',
      'calendarDaysElapsed',
      'ftmoTradingDaysElapsed',
      'fxBandEnd',
    ];
    expect(Object.keys(row.phase1)).toEqual(phaseKeys);
    expect(Object.keys(row.phase2)).toEqual(phaseKeys);
    expect(Object.keys(row.funded)).toEqual([
      'outcome',
      'startSourceOpen',
      'outcomeSourceClose',
      'breachLimit',
      'breachPointClass',
      'calendarDaysFromFundedStart',
      'ftmoTradingDaysFromFundedStart',
      'calendarDaysFromChainStart',
      'ftmoTradingDaysFromChainStart',
    ]);
  });

  it('FtmoMultiStartSummaryDto_CarriesEveryCSharpField', () => {
    const summary = MULTI_START.runs[0].summary;
    expect(summary).not.toBeNull();
    expect(Object.keys(summary ?? {})).toEqual([
      'startCount',
      'outcomes',
      'fxRoundingSensitiveCount',
      'daysToPhase1Target',
      'daysToPhase2Target',
      'daysToBothTargets',
      'fundedDaysToBreachFromFundedStart',
      'fundedDaysToBreachFromChainStart',
      'censoredRunway',
    ]);
    expect(Object.keys(summary?.outcomes[0] ?? {})).toEqual([
      'outcome',
      'isCensored',
      'count',
      'share',
    ]);
    expect(Object.keys(summary?.daysToPhase1Target ?? {})).toEqual([
      'n',
      'min',
      'q1',
      'median',
      'q3',
      'max',
    ]);
  });
});
