import { FtmoGroupCandidateDto } from '../../core/models/ftmo-group-simulation.model';
import { IMOX_RETESTER_LOT_GRID } from '../../core/models/ftmo-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import {
  DEFAULT_GROUP_FORM,
  GroupFormValue,
  canRunGroup,
  needsFxInputs,
  toGroupRequest,
  toWorstCaseReadout,
} from './ftmo-group-simulation.mappers';

const FILLED: GroupFormValue = { ...DEFAULT_GROUP_FORM, targetRiskPerTrade: 25 };

function candidate(id: string, needsFx: boolean, onDeploy = true): FtmoGroupCandidateDto {
  const run = {
    runId: `r-${id}`,
    symbol: 'EURUSD',
    tradeCount: 1,
    firstOpen: null,
    lastClose: null,
    hasInstrumentSpec: true,
    isCalibrated: true,
    profitCurrency: needsFx ? 'EUR' : 'USD',
    needsFxBand: needsFx,
    sourceTimeZoneId: null,
  };
  return {
    strategyId: id,
    name: id,
    symbol: 'EURUSD',
    deploy: onDeploy ? run : null,
    evaluation: onDeploy ? null : run,
    nameExistsOnOtherAccount: false,
  };
}

describe('group form defaults', () => {
  it('prefillsBrokerCapitalAndTheBacktestGridButNotTheRisk', () => {
    expect(DEFAULT_GROUP_FORM.broker).toBe('FTMO');
    expect(DEFAULT_GROUP_FORM.initialCapital).toBe(10000);
    expect(DEFAULT_GROUP_FORM.targetRiskPerTrade).toBeNull();
    expect(DEFAULT_GROUP_FORM.sizeDecimals).toBe(IMOX_RETESTER_LOT_GRID.sizeDecimals);
    expect(DEFAULT_GROUP_FORM.step).toBe(IMOX_RETESTER_LOT_GRID.step);
    expect(DEFAULT_GROUP_FORM.minLot).toBe(IMOX_RETESTER_LOT_GRID.minLot);
    expect(DEFAULT_GROUP_FORM.maxLots).toBe(IMOX_RETESTER_LOT_GRID.maxLots);
    expect(DEFAULT_GROUP_FORM.fxLow).toBeNull();
    expect(DEFAULT_GROUP_FORM.fxHigh).toBeNull();
  });
});

describe('canRunGroup', () => {
  it('isFalseWithNoMembers', () => {
    expect(canRunGroup(FILLED, 0, false)).toBe(false);
  });

  it('isFalseWithAnEmptyRisk', () => {
    expect(canRunGroup(DEFAULT_GROUP_FORM, 2, false)).toBe(false);
  });

  it('isTrueWhenMembersAndEveryRequiredFieldArePresent', () => {
    expect(canRunGroup(FILLED, 1, false)).toBe(true);
  });

  it('isFalseWhileARunIsInFlight', () => {
    expect(canRunGroup(FILLED, 2, true)).toBe(false);
  });

  it('acceptsSizeDecimalsZero_AWholeLotGrid', () => {
    expect(canRunGroup({ ...FILLED, sizeDecimals: 0 }, 2, false)).toBe(true);
  });

  it('isFalseForABlankBrokerOrNonPositiveCapitalRiskOrGridValues', () => {
    expect(canRunGroup({ ...FILLED, broker: '  ' }, 2, false)).toBe(false);
    expect(canRunGroup({ ...FILLED, initialCapital: 0 }, 2, false)).toBe(false);
    expect(canRunGroup({ ...FILLED, targetRiskPerTrade: 0 }, 2, false)).toBe(false);
    expect(canRunGroup({ ...FILLED, step: 0 }, 2, false)).toBe(false);
    expect(canRunGroup({ ...FILLED, sizeDecimals: -1 }, 2, false)).toBe(false);
    expect(canRunGroup({ ...FILLED, maxLots: Number.NaN }, 2, false)).toBe(false);
  });

  it('doesNotRequireTheFxBand', () => {
    expect(canRunGroup({ ...FILLED, fxLow: null, fxHigh: null }, 2, false)).toBe(true);
  });
});

describe('needsFxInputs', () => {
  const all = [candidate('usd', false), candidate('eur', true), candidate('eur-eval', true, false)];

  it('isFalseForAnAllUsdSelection', () => {
    expect(needsFxInputs(all, new Set(['usd']))).toBe(false);
  });

  it('isTrueWhenASelectedMemberNeedsABand_OnEitherKind', () => {
    expect(needsFxInputs(all, new Set(['usd', 'eur']))).toBe(true);
    expect(needsFxInputs(all, new Set(['eur-eval']))).toBe(true);
  });

  it('ignoresAMemberThatIsNotSelected', () => {
    expect(needsFxInputs(all, new Set())).toBe(false);
  });
});

describe('toGroupRequest', () => {
  it('buildsTheBodyWithDeduplicatedIdsAndNullFxWhenHidden', () => {
    const request = toGroupRequest(
      { ...FILLED, sizeDecimals: 0, fxLow: 1.05, fxHigh: 1.15 },
      ['a', 'b', 'a'],
      false,
    );
    expect(request).toEqual({
      memberStrategyIds: ['a', 'b'],
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 25,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 0,
      step: 0.01,
      minLot: 0.01,
      maxLots: 10,
    });
  });

  it('sendsTheBandOnlyWhenShown', () => {
    const request = toGroupRequest({ ...FILLED, fxLow: 1.05, fxHigh: 1.15 }, ['a'], true);
    expect(request?.fxLow).toBe(1.05);
    expect(request?.fxHigh).toBe(1.15);
  });

  it('isNullWhenTheFormCannotRun', () => {
    expect(toGroupRequest(DEFAULT_GROUP_FORM, ['a'], false)).toBeNull();
    expect(toGroupRequest(FILLED, [], false)).toBeNull();
  });
});

describe('toWorstCaseReadout', () => {
  it('showsKTimesRiskAsAmountAndPercentAgainstBothReferences', () => {
    const r = toWorstCaseReadout(4, 25, 10000, null, [])!;
    expect(r.worst.amount).toBe(100);
    expect(r.worst.pct).toBe(1);
    expect(r.worst.pctText).toBe('1.00');
    expect(r.academyPct).toBe(1);
    expect(r.dailyLimitPct).toBe(5);
    expect(r.worst.exceedsAcademy).toBe(false);
    expect(r.worst.exceedsDaily).toBe(false);
  });

  it('marksTheAcademyCriterionAsExceededButNotTheDailyLimit', () => {
    const r = toWorstCaseReadout(4, 50, 10000, null, [])!;
    expect(r.worst.pct).toBe(2);
    expect(r.worst.exceedsAcademy).toBe(true);
    expect(r.worst.exceedsDaily).toBe(false);
  });

  it('followsTheEchoedDailyLimit_WhichTheBackendSendsAsAFraction', () => {
    // 4 x 112.5 = 450 = 4.5% of 10000: above an echoed 4% limit, below the 5% default.
    const r = toWorstCaseReadout(4, 112.5, 10000, 0.04, [])!;
    expect(r.dailyLimitPct).toBe(4);
    expect(r.worst.exceedsDaily).toBe(true);
  });

  it('convertsTheEchoedFraction0_05To5Percent_AndDoesNotFlagARiskBelowIt (F3a RELIABILITY-001)', () => {
    // 4 x 100 = 400 = 4% of 10000: above the 1% academy criterion, below the 5% daily limit.
    const r = toWorstCaseReadout(4, 100, 10000, 0.05, [{ kind: BacktestRunKind.Deploy, peak: 4 }])!;
    expect(r.dailyLimitPct).toBe(5);
    expect(r.worst.pct).toBe(4);
    expect(r.worst.exceedsAcademy).toBe(true);
    expect(r.worst.exceedsDaily).toBe(false);
    expect(r.observed[0].line.exceedsDaily).toBe(false);
  });

  it('convertsAFractionWithoutFloatingPointNoise', () => {
    // 0.07 * 100 is 7.000000000000001 in IEEE 754; the readout must show 7.
    expect(toWorstCaseReadout(1, 25, 10000, 0.07, [])!.dailyLimitPct).toBe(7);
  });

  it('showsTheObservedPeakTimesRiskPerKind_AndKeepsAPeakOfZero', () => {
    const r = toWorstCaseReadout(4, 25, 10000, null, [
      { kind: BacktestRunKind.Deploy, peak: 3 },
      { kind: BacktestRunKind.Evaluation, peak: 0 },
    ])!;
    expect(r.observed).toHaveLength(2);
    expect(r.observed[0]).toMatchObject({ kind: BacktestRunKind.Deploy, peak: 3 });
    expect(r.observed[0].line.amount).toBe(75);
    expect(r.observed[1].peak).toBe(0);
    expect(r.observed[1].line.amount).toBe(0);
  });

  it('isNullWithoutMembersOrAUsableRiskOrCapital', () => {
    expect(toWorstCaseReadout(0, 25, 10000, null, [])).toBeNull();
    expect(toWorstCaseReadout(2, null, 10000, null, [])).toBeNull();
    expect(toWorstCaseReadout(2, 25, null, null, [])).toBeNull();
    expect(toWorstCaseReadout(2, 25, 0, null, [])).toBeNull();
  });
});
