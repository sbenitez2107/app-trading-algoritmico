import { BacktestRunKind } from '../services/backtest.service';
import { FtmoSimulationRefusal } from './ftmo-simulation.model';
import {
  FtmoGroupSearchIneligibleReason,
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRequest,
  FtmoGroupSearchStage,
  FtmoGroupSearchStartResponseDto,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from './ftmo-group-search.model';

function numericMembers(e: Record<string, string | number>): number[] {
  return Object.values(e).filter((v): v is number => typeof v === 'number');
}

/** Numbers copied from `FtmoGroupSearchDto.cs` (explicit values). */
describe('FtmoGroupSearchStatus mirror', () => {
  it('unknownIsZero_TheClrDefault_AndIsNotCompleted', () => {
    expect(FtmoGroupSearchStatus.Unknown).toBe(0);
    expect(FtmoGroupSearchStatus.Unknown).not.toBe(FtmoGroupSearchStatus.Completed);
  });

  it.each([
    ['Running', FtmoGroupSearchStatus.Running, 1],
    ['Completed', FtmoGroupSearchStatus.Completed, 2],
    ['StoppedAtBudget', FtmoGroupSearchStatus.StoppedAtBudget, 3],
    ['Cancelled', FtmoGroupSearchStatus.Cancelled, 4],
    ['Failed', FtmoGroupSearchStatus.Failed, 5],
  ])('%sMatchesTheBackendNumber', (_n, actual, expected) => {
    expect(actual).toBe(expected);
  });

  it('hasExactlySixMembers', () => {
    expect(numericMembers(FtmoGroupSearchStatus)).toHaveLength(6);
  });
});

describe('FtmoGroupSearchStage mirror', () => {
  it('unknownIsZero', () => {
    expect(FtmoGroupSearchStage.Unknown).toBe(0);
  });

  it.each([
    ['Loading', FtmoGroupSearchStage.Loading, 1],
    ['Eligibility', FtmoGroupSearchStage.Eligibility, 2],
    ['Proxy', FtmoGroupSearchStage.Proxy, 3],
    ['Simulating', FtmoGroupSearchStage.Simulating, 4],
    ['Ranking', FtmoGroupSearchStage.Ranking, 5],
  ])('%sMatchesTheBackendNumber', (_n, actual, expected) => {
    expect(actual).toBe(expected);
  });

  it('hasExactlySixMembers', () => {
    expect(numericMembers(FtmoGroupSearchStage)).toHaveLength(6);
  });
});

describe('FtmoGroupSearchStopReason mirror', () => {
  it('unknownIsZero', () => {
    expect(FtmoGroupSearchStopReason.Unknown).toBe(0);
  });

  it.each([
    ['None', FtmoGroupSearchStopReason.None, 1],
    ['MaxFullSimulations', FtmoGroupSearchStopReason.MaxFullSimulations, 2],
    ['WallClock', FtmoGroupSearchStopReason.WallClock, 3],
  ])('%sMatchesTheBackendNumber', (_n, actual, expected) => {
    expect(actual).toBe(expected);
  });

  it('hasExactlyFourMembers', () => {
    expect(numericMembers(FtmoGroupSearchStopReason)).toHaveLength(4);
  });
});

describe('FtmoGroupSearchIneligibleReason mirror', () => {
  it('unknownIsZero', () => {
    expect(FtmoGroupSearchIneligibleReason.Unknown).toBe(0);
  });

  it.each([
    ['MissingKind', FtmoGroupSearchIneligibleReason.MissingKind, 1],
    ['SymbolRefused', FtmoGroupSearchIneligibleReason.SymbolRefused, 2],
    ['ZoneUnresolved', FtmoGroupSearchIneligibleReason.ZoneUnresolved, 3],
    ['ProjectionRefused', FtmoGroupSearchIneligibleReason.ProjectionRefused, 4],
    ['ProjectionRowless', FtmoGroupSearchIneligibleReason.ProjectionRowless, 5],
    ['IdenticalDeployEval', FtmoGroupSearchIneligibleReason.IdenticalDeployEval, 6],
  ])('%sMatchesTheBackendNumber', (_n, actual, expected) => {
    expect(actual).toBe(expected);
  });

  it('hasExactlySevenMembers', () => {
    expect(numericMembers(FtmoGroupSearchIneligibleReason)).toHaveLength(7);
  });
});

/** Typed fixtures with NO casts (PR1a lesson): `tsc` fails when a mirror lacks a field. */
describe('ftmo group search wire mirror', () => {
  it('requestCarriesEveryBodyField_AndKeepsZeroAndNull', () => {
    const request: FtmoGroupSearchRequest = {
      tradingAccountId: 'a',
      minMembers: 2,
      maxMembers: 4,
      maxPerInstrument: 1,
      includeIdenticalDeployEval: false,
      onePercentRule: false,
      eliminationCeiling: 0.05,
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 25,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 0,
      step: 0.01,
      minLot: 0.01,
      maxLots: 10,
      maxFullSimulations: null,
      maxWallClockSeconds: null,
    };
    expect(request.sizeDecimals).toBe(0);
    expect(Object.keys(request)).toHaveLength(18);
  });

  const job: FtmoGroupSearchJobDto = {
    jobId: 'j',
    status: FtmoGroupSearchStatus.StoppedAtBudget,
    progress: {
      stage: FtmoGroupSearchStage.Simulating,
      processed: 1,
      total: 2,
      elapsedMs: 3,
      fullSimulationsDone: 4,
      maxFullSimulations: 5,
      funnel: {
        examined: 12926,
        removedByCap: 1,
        removedByPairConflict: 2,
        removedByOnePercentRule: 3,
        removedNoCommonWindow: 4,
        removedMemberHasNoTrades: 5,
        shortlisted: 6,
      },
    },
    stopReason: FtmoGroupSearchStopReason.WallClock,
    notComputed: 7,
    ineligible: [
      {
        strategyId: 's',
        name: 'S',
        reason: FtmoGroupSearchIneligibleReason.ProjectionRefused,
        refusal: FtmoSimulationRefusal.RiskNotEstimable,
      },
      {
        strategyId: 't',
        name: 'T',
        reason: FtmoGroupSearchIneligibleReason.MissingKind,
        refusal: null,
      },
    ],
    rows: [
      {
        rank: 1,
        memberIds: ['a', 'b'],
        memberNames: ['A', 'B'],
        peakConcurrentOpen: 2,
        identicalDeployEval: false,
        withinCeiling: true,
        headroom: 0.8,
        kindHeadrooms: [
          {
            kind: BacktestRunKind.Deploy,
            worstDailyUsed: 0.1,
            worstMaxUsed: 0.2,
            medianMaxUsed: 0.15,
            headroom: 0.8,
          },
        ],
        kinds: [],
        symbols: ['EURUSD'],
      },
      {
        rank: 2,
        memberIds: [],
        memberNames: [],
        peakConcurrentOpen: 0,
        identicalDeployEval: true,
        withinCeiling: false,
        headroom: 0,
        kindHeadrooms: [],
        kinds: [],
        symbols: null,
      },
    ],
    disclosures: ['x'],
    errorMessage: null,
    request: {
      tradingAccountId: 'a',
      minMembers: 2,
      maxMembers: 4,
      maxPerInstrument: 2,
      includeIdenticalDeployEval: true,
      onePercentRule: false,
      eliminationCeiling: 0.05,
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 100,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 2,
      step: 0.01,
      minLot: 0.01,
      maxLots: 100,
      maxFullSimulations: null,
      maxWallClockSeconds: null,
    },
  };

  it('jobCarriesEveryField', () => {
    expect(Object.keys(job)).toEqual([
      'jobId',
      'status',
      'progress',
      'stopReason',
      'notComputed',
      'ineligible',
      'rows',
      'disclosures',
      'errorMessage',
      'request',
    ]);
    expect(Object.keys(job.progress)).toHaveLength(7);
    expect(Object.keys(job.progress.funnel)).toHaveLength(7);
    expect(Object.keys(job.rows[0])).toHaveLength(10);
    expect(Object.keys(job.rows[0].kindHeadrooms[0])).toHaveLength(5);
  });

  it('startResponseIsJobIdPlusJob', () => {
    const response: FtmoGroupSearchStartResponseDto = { jobId: 'j', job };
    expect(response.job.jobId).toBe(response.jobId);
  });

  it('noMemberOrKeyUsesBannedSurvivalWording', () => {
    const names = [
      ...Object.keys(FtmoGroupSearchStatus),
      ...Object.keys(FtmoGroupSearchStage),
      ...Object.keys(FtmoGroupSearchStopReason),
      ...Object.keys(FtmoGroupSearchIneligibleReason),
      ...Object.keys(job),
      ...Object.keys(job.progress),
      ...Object.keys(job.rows[0]),
    ];
    expect(names.filter((n) => /surviv|supervivencia/i.test(n))).toEqual([]);
  });
});
