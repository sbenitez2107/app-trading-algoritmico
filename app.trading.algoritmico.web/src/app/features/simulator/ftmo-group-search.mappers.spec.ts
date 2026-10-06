import { FtmoChainOutcome } from '../../core/models/ftmo-simulation.model';
import {
  FtmoGroupSearchIneligibleReason,
  FtmoGroupSearchRowDto,
  FtmoGroupSearchStage,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from '../../core/models/ftmo-group-search.model';
import { FtmoGroupKindResultDto } from '../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { refusedKind, successKind } from './ftmo-group-simulation.result.fixtures';
import {
  breachShare,
  fractionToPercent,
  ineligibleReasonKey,
  isWithinCeiling,
  medianVm,
  percentToFraction,
  stageKey,
  statusKey,
  stopReasonKey,
} from './ftmo-group-search.mappers';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';

/** A kind whose run breached `breached` of `startCount` starts (spread over the three breach outcomes). */
function kindWithBreaches(
  kind: BacktestRunKind,
  breached: number,
  startCount: number,
): FtmoGroupKindResultDto {
  const base = successKind(kind);
  const run = base.run!;
  const summary = run.summary!;
  const breachOutcomes = [
    FtmoChainOutcome.Phase1Breached,
    FtmoChainOutcome.Phase2Breached,
    FtmoChainOutcome.FundedBreached,
  ];
  const outcomes = summary.outcomes.map((o) => {
    const idx = breachOutcomes.indexOf(o.outcome);
    if (idx === 0) return { ...o, count: breached };
    if (idx > 0) return { ...o, count: 0 };
    return { ...o, count: o.outcome === FtmoChainOutcome.FundedNoBreachAtEndOfData ? 3 : 0 };
  });
  return { ...base, run: { ...run, summary: { ...summary, startCount, outcomes } } };
}

function row(kinds: FtmoGroupKindResultDto[]): FtmoGroupSearchRowDto {
  return {
    rank: 1,
    memberIds: ['a', 'b'],
    memberNames: ['A', 'B'],
    peakConcurrentOpen: 2,
    identicalDeployEval: false,
    withinCeiling: false,
    headroom: 0.5,
    kindHeadrooms: [],
    kinds,
    symbols: [],
  };
}

describe('fractionToPercent / percentToFraction (fractions converted ONCE)', () => {
  it.each([
    [0.045, 4.5],
    [0.8, 80],
    [0, 0],
    [1, 100],
    [0.07, 7],
  ])('fractionToPercent(%s)Is%s_NeverTimes10000', (fraction, expected) => {
    expect(fractionToPercent(fraction)).toBe(expected);
  });

  it('aFractionIsNeverScaledTwice', () => {
    expect(fractionToPercent(0.8)).not.toBe(8000);
    expect(fractionToPercent(0.8)).toBeLessThanOrEqual(100);
  });

  it('theCeilingInputIsAPercentAndBecomesAFraction', () => {
    expect(percentToFraction(5)).toBe(0.05);
    expect(percentToFraction(10)).toBe(0.1);
    expect(percentToFraction(0)).toBe(0);
  });
});

describe('breachShare', () => {
  it('sumsPhase1Phase2AndFundedBreachesOverTheStartCount', () => {
    const kind = kindWithBreaches(BacktestRunKind.Deploy, 1, 20);
    // fixture: Phase1Breached = 1, Phase2Breached = 0, FundedBreached = 0
    expect(breachShare(kind)).toBe(0.05);
  });

  it('includesAllThreeBreachOutcomes', () => {
    const kind = successKind(BacktestRunKind.Deploy);
    // evaluatedRun: Phase1Breached 0, Phase2Breached 1, FundedBreached 1 over startCount 6
    expect(breachShare(kind)).toBeCloseTo(2 / 6, 12);
  });

  it('aRefusedKindHasNoShare_NotZero', () => {
    expect(
      breachShare(refusedKind(BacktestRunKind.Evaluation, FtmoGroupRefusal.NoCommonWindow)),
    ).toBeNull();
  });

  it('aZeroBreachRunKeepsItsRealZero', () => {
    expect(breachShare(kindWithBreaches(BacktestRunKind.Deploy, 0, 10))).toBe(0);
  });
});

describe('isWithinCeiling (raw fractions, the worse kind decides)', () => {
  const ceiling = percentToFraction(5);

  it('breach0049IsHighlightedAt5Percent', () => {
    const r = row([
      kindWithBreaches(BacktestRunKind.Deploy, 0, 1000),
      kindWithBreaches(BacktestRunKind.Evaluation, 49, 1000),
    ]);
    expect(isWithinCeiling(r, ceiling)).toBe(true);
  });

  it('03And06IsNotHighlighted_TheWorseKindDecides', () => {
    const r = row([
      kindWithBreaches(BacktestRunKind.Deploy, 3, 100),
      kindWithBreaches(BacktestRunKind.Evaluation, 6, 100),
    ]);
    expect(isWithinCeiling(r, ceiling)).toBe(false);
  });

  it('03And04IsHighlighted', () => {
    const r = row([
      kindWithBreaches(BacktestRunKind.Deploy, 3, 100),
      kindWithBreaches(BacktestRunKind.Evaluation, 4, 100),
    ]);
    expect(isWithinCeiling(r, ceiling)).toBe(true);
  });

  it('exactlyAtTheCeilingIsWithin', () => {
    const r = row([kindWithBreaches(BacktestRunKind.Deploy, 5, 100)]);
    expect(isWithinCeiling(r, ceiling)).toBe(true);
  });

  it('theCeilingEditedToTenPercentReevaluates', () => {
    const r = row([kindWithBreaches(BacktestRunKind.Deploy, 8, 100)]);
    expect(isWithinCeiling(r, percentToFraction(5))).toBe(false);
    expect(isWithinCeiling(r, percentToFraction(10))).toBe(true);
  });

  it('aPercentPassedAsTheCeilingWouldHighlightEverything_SoItIsAFraction', () => {
    const r = row([kindWithBreaches(BacktestRunKind.Deploy, 60, 100)]);
    expect(isWithinCeiling(r, percentToFraction(5))).toBe(false);
  });

  it('aRefusedKindIsNeverWithin', () => {
    const r = row([
      kindWithBreaches(BacktestRunKind.Deploy, 0, 100),
      refusedKind(BacktestRunKind.Evaluation, FtmoGroupRefusal.NoCommonWindow),
    ]);
    expect(isWithinCeiling(r, ceiling)).toBe(false);
  });

  it('aRowWithNoKindsIsNeverWithin', () => {
    expect(isWithinCeiling(row([]), ceiling)).toBe(false);
  });
});

describe('medianVm', () => {
  it('nullIsExplicitlyAbsent', () => {
    expect(medianVm(null)).toEqual({ kind: 'absent' });
  });

  it('aRealZeroStaysZero', () => {
    expect(medianVm(0)).toEqual({ kind: 'value', value: 0 });
  });

  it('aValueIsKept', () => {
    expect(medianVm(123.5)).toEqual({ kind: 'value', value: 123.5 });
  });
});

describe('enum to i18n key mapping', () => {
  it.each([
    [FtmoGroupSearchStatus.Running, 'SIMULATOR.FTMO_SEARCH.STATUS.RUNNING'],
    [FtmoGroupSearchStatus.Completed, 'SIMULATOR.FTMO_SEARCH.STATUS.COMPLETED'],
    [FtmoGroupSearchStatus.StoppedAtBudget, 'SIMULATOR.FTMO_SEARCH.STATUS.STOPPED_AT_BUDGET'],
    [FtmoGroupSearchStatus.Cancelled, 'SIMULATOR.FTMO_SEARCH.STATUS.CANCELLED'],
    [FtmoGroupSearchStatus.Failed, 'SIMULATOR.FTMO_SEARCH.STATUS.FAILED'],
  ])('status%sMapsToItsOwnKey', (status, key) => {
    expect(statusKey(status)).toEqual({ key, raw: null });
  });

  it('statusZeroIsUnknown_WithItsRawValue_NotCompleted', () => {
    expect(statusKey(FtmoGroupSearchStatus.Unknown)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.STATUS.UNKNOWN',
      raw: 0,
    });
  });

  it('anUnmappedStatusIsUnknownWithTheRawValue', () => {
    expect(statusKey(99)).toEqual({ key: 'SIMULATOR.FTMO_SEARCH.STATUS.UNKNOWN', raw: 99 });
  });

  it.each([
    [FtmoGroupSearchStage.Loading, 'SIMULATOR.FTMO_SEARCH.STAGE.LOADING'],
    [FtmoGroupSearchStage.Eligibility, 'SIMULATOR.FTMO_SEARCH.STAGE.ELIGIBILITY'],
    [FtmoGroupSearchStage.Proxy, 'SIMULATOR.FTMO_SEARCH.STAGE.PROXY'],
    [FtmoGroupSearchStage.Simulating, 'SIMULATOR.FTMO_SEARCH.STAGE.SIMULATING'],
    [FtmoGroupSearchStage.Ranking, 'SIMULATOR.FTMO_SEARCH.STAGE.RANKING'],
  ])('stage%sMapsToItsOwnKey', (stage, key) => {
    expect(stageKey(stage)).toEqual({ key, raw: null });
  });

  it('stageZeroAndUnmappedAreUnknownWithRaw', () => {
    expect(stageKey(FtmoGroupSearchStage.Unknown)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.STAGE.UNKNOWN',
      raw: 0,
    });
    expect(stageKey(42)).toEqual({ key: 'SIMULATOR.FTMO_SEARCH.STAGE.UNKNOWN', raw: 42 });
  });

  it.each([
    [FtmoGroupSearchStopReason.None, 'SIMULATOR.FTMO_SEARCH.STOP_REASON.NONE'],
    [
      FtmoGroupSearchStopReason.MaxFullSimulations,
      'SIMULATOR.FTMO_SEARCH.STOP_REASON.MAX_FULL_SIMULATIONS',
    ],
    [FtmoGroupSearchStopReason.WallClock, 'SIMULATOR.FTMO_SEARCH.STOP_REASON.WALL_CLOCK'],
  ])('stopReason%sMapsToItsOwnKey', (reason, key) => {
    expect(stopReasonKey(reason)).toEqual({ key, raw: null });
  });

  it('stopReasonZeroAndUnmappedAreUnknownWithRaw', () => {
    expect(stopReasonKey(FtmoGroupSearchStopReason.Unknown)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.STOP_REASON.UNKNOWN',
      raw: 0,
    });
    expect(stopReasonKey(-1)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.STOP_REASON.UNKNOWN',
      raw: -1,
    });
  });

  it.each([
    [FtmoGroupSearchIneligibleReason.MissingKind, 'MISSING_KIND'],
    [FtmoGroupSearchIneligibleReason.SymbolRefused, 'SYMBOL_REFUSED'],
    [FtmoGroupSearchIneligibleReason.ZoneUnresolved, 'ZONE_UNRESOLVED'],
    [FtmoGroupSearchIneligibleReason.ProjectionRefused, 'PROJECTION_REFUSED'],
    [FtmoGroupSearchIneligibleReason.ProjectionRowless, 'PROJECTION_ROWLESS'],
    [FtmoGroupSearchIneligibleReason.IdenticalDeployEval, 'IDENTICAL_DEPLOY_EVAL'],
  ])('ineligibleReason%sMapsToItsOwnKey', (reason, suffix) => {
    expect(ineligibleReasonKey(reason)).toEqual({
      key: `SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON.${suffix}`,
      raw: null,
    });
  });

  it('ineligibleReasonZeroAndUnmappedAreUnknownWithRaw', () => {
    expect(ineligibleReasonKey(FtmoGroupSearchIneligibleReason.Unknown)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON.UNKNOWN',
      raw: 0,
    });
    expect(ineligibleReasonKey(77)).toEqual({
      key: 'SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON.UNKNOWN',
      raw: 77,
    });
  });
});
