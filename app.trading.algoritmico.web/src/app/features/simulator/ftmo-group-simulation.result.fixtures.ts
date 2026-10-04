import {
  FtmoGroupDiagnosticsDto,
  FtmoGroupKindResultDto,
  FtmoGroupMemberAttributionDto,
  FtmoGroupMemberContributionDto,
  FtmoGroupMemberCoverageDto,
  FtmoGroupMemberDto,
  FtmoGroupMemberRefusalDto,
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
} from '../../core/models/ftmo-group-simulation.model';
import {
  FtmoChainOutcome,
  FtmoMultiStartRunDto,
  FtmoOrderStatisticsDto,
  FtmoSimulationRefusal,
  FtmoSimulationStatus,
  FtmoStartGrain,
} from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind, BacktestSegment } from '../../core/services/backtest.service';

/**
 * Typed builders for the F3b result specs (mappers, result component, page sweeps). Every nested record is
 * fully populated, so a render exercises every placeholder of every key (PR1c/1d lessons).
 */

const STATS: FtmoOrderStatisticsDto = { n: 3, min: 1, q1: 2, median: 3, q3: 4, max: 5 };

const OUTCOMES: FtmoChainOutcome[] = [
  FtmoChainOutcome.Phase1Breached,
  FtmoChainOutcome.Phase1UndecidedAtEndOfData,
  FtmoChainOutcome.Phase2Breached,
  FtmoChainOutcome.Phase2UndecidedAtEndOfData,
  FtmoChainOutcome.FundedBreached,
  FtmoChainOutcome.FundedNoBreachAtEndOfData,
];

/** A successful, fully populated run. `start1Differs` is `true` on purpose: a group must never show it. */
export function evaluatedRun(kind: BacktestRunKind): FtmoMultiStartRunDto {
  return {
    runId: '00000000-0000-0000-0000-000000000000',
    kind,
    segment: BacktestSegment.OutOfSample,
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    raceRefusal: null,
    storedProfitTargetPct: null,
    grain: FtmoStartGrain.Monthly,
    rules: {
      phase1TargetPct: 0.1,
      phase2TargetPct: 0.05,
      minTradingDaysPerPhase: 4,
      timeLimitDays: null,
    },
    starts: [],
    summary: {
      startCount: 6,
      outcomes: OUTCOMES.map((outcome, i) => ({
        outcome,
        isCensored: false,
        count: i === 0 ? 0 : 1,
        share: i === 0 ? 0 : 0.2,
      })),
      fxRoundingSensitiveCount: 0,
      daysToPhase1Target: STATS,
      daysToPhase2Target: STATS,
      daysToBothTargets: STATS,
      fundedDaysToBreachFromFundedStart: STATS,
      fundedDaysToBreachFromChainStart: STATS,
      censoredRunway: STATS,
    },
    monthsWithoutStart: ['2020-03'],
    start1DiffersFromSingleStartAnchor: true,
    fxLow: 1,
    fxHigh: 1,
    unscalableCount: 0,
    notModelled: ['Swap', 'FtmoCommission', 'IntradayEquity'],
    disclosure: 'server text, never rendered',
  };
}

export function coverage(
  id: string,
  name: string,
  overrides: Partial<FtmoGroupMemberCoverageDto> = {},
): FtmoGroupMemberCoverageDto {
  return {
    strategyId: id,
    name,
    firstOpen: '2020-02-01T00:00:00',
    lastClose: '2021-06-30T00:00:00',
    inWindowTrades: 7,
    ...overrides,
  };
}

export function memberRefusal(
  id: string,
  name: string,
  reason: FtmoGroupRefusal,
  runReason: FtmoSimulationRefusal | null = null,
): FtmoGroupMemberRefusalDto {
  return { strategyId: id, name, reason, runReason };
}

/** A successful kind whose members all fit the window exactly. */
export function successKind(
  kind: BacktestRunKind,
  diagnostics: FtmoGroupDiagnosticsDto | null = null,
): FtmoGroupKindResultDto {
  return {
    kind,
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    memberRefusals: [],
    window: { start: '2020-02-01T00:00:00', end: '2021-06-30T00:00:00' },
    coverage: [coverage('a', 'Alpha'), coverage('b', 'Beta')],
    run: evaluatedRun(kind),
    diagnostics,
  };
}

/** A refused kind; `refusal` is the kind-level reason, `members` the failing members. */
export function refusedKind(
  kind: BacktestRunKind,
  refusal: FtmoGroupRefusal,
  members: FtmoGroupMemberRefusalDto[] = [],
  cover: FtmoGroupMemberCoverageDto[] = [],
): FtmoGroupKindResultDto {
  return {
    kind,
    status: FtmoSimulationStatus.Refused,
    refusal,
    memberRefusals: members,
    window: null,
    coverage: cover,
    run: null,
    diagnostics: null,
  };
}

export const MEMBERS: FtmoGroupMemberDto[] = [
  {
    strategyId: 'a',
    name: 'Alpha',
    memberOrder: 0,
    profitCurrency: 'USD',
    sourceTimeZoneId: 'Europe/Berlin',
    fxLow: 1,
    fxHigh: 1,
  },
  {
    strategyId: 'b',
    name: 'Beta',
    memberOrder: 1,
    profitCurrency: 'USD',
    sourceTimeZoneId: 'America/New_York',
    fxLow: 1,
    fxHigh: 1,
  },
];

export function groupResult(
  kinds: FtmoGroupKindResultDto[],
  overrides: Partial<FtmoGroupSimulationDto> = {},
): FtmoGroupSimulationDto {
  return {
    status: FtmoSimulationStatus.Evaluated,
    refusal: null,
    sharedRefusal: null,
    dailyLossLimitPct: 0.05,
    maxLossLimitPct: 0.1,
    members: MEMBERS,
    duplicateIdsRemoved: [],
    duplicateNameWarnings: [],
    unknownStrategyIds: [],
    kinds,
    disclosures: ['server disclosure one', 'server disclosure two', 'server disclosure three'],
    ...overrides,
  };
}

/** A group-wide refusal: `Status` is Refused and `Kinds` is empty (the envelope contract). */
export function groupWideRefusal(
  refusal: FtmoGroupRefusal,
  overrides: Partial<FtmoGroupSimulationDto> = {},
): FtmoGroupSimulationDto {
  return groupResult([], {
    status: FtmoSimulationStatus.Refused,
    refusal,
    ...overrides,
  });
}

/** Three members (Alpha, Beta, Gamma) for the diagnostics specs. */
export const THREE_MEMBERS: FtmoGroupMemberDto[] = [
  ...MEMBERS,
  { ...MEMBERS[0], strategyId: 'c', name: 'Gamma', memberOrder: 2 },
];

export function contribution(
  id: string,
  name: string,
  overrides: Partial<FtmoGroupMemberContributionDto> = {},
): FtmoGroupMemberContributionDto {
  return {
    strategyId: id,
    name,
    inWindowTrades: 20,
    scalableTrades: 18,
    netLow: 150.5,
    netHigh: 210.25,
    raisedToMinimum: 2,
    cappedAtMaximum: 1,
    unscalable: 2,
    ...overrides,
  };
}

export function memberAttribution(
  id: string,
  name: string,
  overrides: Partial<FtmoGroupMemberAttributionDto> = {},
): FtmoGroupMemberAttributionDto {
  return {
    strategyId: id,
    name,
    phase1Starts: 4,
    phase2Starts: 1,
    fundedStarts: 1,
    soleContributorStarts: 4,
    sharedCloseStarts: 2,
    ...overrides,
  };
}

/**
 * Three members; 10 deciding-breach starts = 8 sole (4 + 3 + 1) + 2 tied + 0 unattributed. Alpha and Beta tie
 * on both shared closes, so each member's phase counts add up to its sole + tied starts (6, 5, 1).
 */
export function diagnosticsFixture(
  overrides: Partial<FtmoGroupDiagnosticsDto> = {},
): FtmoGroupDiagnosticsDto {
  return {
    contributions: [
      contribution('a', 'Alpha'),
      contribution('b', 'Beta', { netLow: -42.5, netHigh: 0 }),
      contribution('c', 'Gamma', { raisedToMinimum: 0, cappedAtMaximum: 0, unscalable: 0 }),
    ],
    attribution: {
      decidingBreachStarts: 10,
      sharedCloseStarts: 2,
      unattributedStarts: 0,
      members: [
        memberAttribution('a', 'Alpha'),
        memberAttribution('b', 'Beta', {
          phase1Starts: 3,
          phase2Starts: 2,
          fundedStarts: 0,
          soleContributorStarts: 3,
        }),
        memberAttribution('c', 'Gamma', {
          phase1Starts: 0,
          phase2Starts: 0,
          fundedStarts: 1,
          soleContributorStarts: 1,
          sharedCloseStarts: 0,
        }),
      ],
    },
    peak: {
      peakConcurrentOpen: 3,
      firstReachedSource: '2020-05-04T10:30:00',
      memberIdsAtPeak: ['a', 'b', 'c'],
    },
    ...overrides,
  };
}
