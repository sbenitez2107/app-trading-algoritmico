import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRequest,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStage,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from '../../core/models/ftmo-group-search.model';
import { FtmoGroupKindResultDto } from '../../core/models/ftmo-group-simulation.model';
import { FtmoChainOutcome } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { successKind } from './ftmo-group-simulation.result.fixtures';

/** A submitted request with every field present; override what a test cares about. Shares are fractions. */
export function searchRequest(
  overrides: Partial<FtmoGroupSearchRequest> = {},
): FtmoGroupSearchRequest {
  return {
    tradingAccountId: 'a2',
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
    ...overrides,
  };
}

/** A job snapshot with every field present; override what a test cares about. */
export function searchJob(overrides: Partial<FtmoGroupSearchJobDto> = {}): FtmoGroupSearchJobDto {
  return {
    jobId: 'job-1',
    status: FtmoGroupSearchStatus.Running,
    progress: {
      stage: FtmoGroupSearchStage.Proxy,
      processed: 40,
      total: 200,
      elapsedMs: 65000,
      fullSimulationsDone: 3,
      maxFullSimulations: 75,
      funnel: {
        examined: 12926,
        removedByCap: 11,
        removedByPairConflict: 22,
        removedByOnePercentRule: 33,
        removedNoCommonWindow: 44,
        removedMemberHasNoTrades: 55,
        shortlisted: 75,
      },
    },
    stopReason: FtmoGroupSearchStopReason.None,
    notComputed: 0,
    ineligible: [],
    rows: [],
    disclosures: ['server text that must never render'],
    errorMessage: null,
    request: searchRequest(),
    ...overrides,
  };
}

export interface SearchKindOptions {
  startCount?: number;
  breached?: number;
  fundedNoBreach?: number;
  medianDays?: number | null;
}

/** An evaluated kind with explicit breach / funded counts and median days to both targets. */
export function searchKind(
  kind: BacktestRunKind,
  o: SearchKindOptions = {},
): FtmoGroupKindResultDto {
  const base = successKind(kind);
  const summary = base.run!.summary!;
  const startCount = o.startCount ?? 1000;
  const counts = new Map<FtmoChainOutcome, number>([
    [FtmoChainOutcome.Phase1Breached, o.breached ?? 0],
    [FtmoChainOutcome.FundedNoBreachAtEndOfData, o.fundedNoBreach ?? 0],
  ]);
  const median = o.medianDays === undefined ? 90 : o.medianDays;
  return {
    ...base,
    run: {
      ...base.run!,
      summary: {
        ...summary,
        startCount,
        outcomes: summary.outcomes.map((x) => ({
          ...x,
          count: counts.get(x.outcome) ?? 0,
          share: (counts.get(x.outcome) ?? 0) / startCount,
        })),
        daysToBothTargets: { ...summary.daysToBothTargets, median },
      },
    },
  };
}

/** One ranked row with a Deploy and an Evaluation kind; override what a test cares about. */
export function searchRow(overrides: Partial<FtmoGroupSearchRowDto> = {}): FtmoGroupSearchRowDto {
  return {
    rank: 1,
    memberIds: ['s1', 's2'],
    memberNames: ['Alpha', 'Beta'],
    peakConcurrentOpen: 3,
    identicalDeployEval: false,
    withinCeiling: true,
    headroom: 0.2,
    kindHeadrooms: [
      {
        kind: BacktestRunKind.Deploy,
        worstDailyUsed: 0.45,
        worstMaxUsed: 0.8,
        medianMaxUsed: 0.3,
        headroom: 0.2,
      },
      {
        kind: BacktestRunKind.Evaluation,
        worstDailyUsed: 0.1,
        worstMaxUsed: 0.6,
        medianMaxUsed: 0.25,
        headroom: 0.4,
      },
    ],
    kinds: [
      searchKind(BacktestRunKind.Deploy, { breached: 45, fundedNoBreach: 700, medianDays: 120 }),
      searchKind(BacktestRunKind.Evaluation, { breached: 30, fundedNoBreach: 650, medianDays: 0 }),
    ],
    symbols: ['EURUSD', 'GBPJPY'],
    ...overrides,
  };
}
