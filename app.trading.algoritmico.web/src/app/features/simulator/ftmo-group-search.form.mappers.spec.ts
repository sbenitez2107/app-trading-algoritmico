import { describe, expect, it } from 'vitest';
import { FtmoGroupSearchStatus } from '../../core/models/ftmo-group-search.model';
import { IMOX_RETESTER_LOT_GRID } from '../../core/models/ftmo-simulation.model';
import {
  DEFAULT_SEARCH_FORM,
  SearchFormValue,
  canStartSearch,
  fxBandValid,
  formatElapsed,
  isRunningStatus,
  isTerminalStatus,
  progressPercent,
  sizeBoundsValid,
  toSearchRequest,
} from './ftmo-group-search.mappers';

const READY: SearchFormValue = { ...DEFAULT_SEARCH_FORM, targetRiskPerTrade: 25 };

describe('search form defaults', () => {
  it('matchTheSpec', () => {
    expect(DEFAULT_SEARCH_FORM).toMatchObject({
      minMembers: 2,
      maxMembers: 4,
      maxPerInstrument: 2,
      excludeIdentical: true,
      onePercentRule: false,
      ceilingPercent: 5,
      initialCapital: 10000,
      targetRiskPerTrade: null,
      fxLow: null,
      fxHigh: null,
      maxFullSimulations: null,
      maxWallClockSeconds: null,
    });
  });
});

describe('canStartSearch', () => {
  it('isFalseWithAnEmptyRisk', () => {
    expect(canStartSearch(DEFAULT_SEARCH_FORM, 4, false)).toBe(false);
    expect(canStartSearch(READY, 4, false)).toBe(true);
  });
  it.each([0, -5, Number.NaN])('isFalseForAnInvalidCapital_%s', (capital) => {
    expect(canStartSearch({ ...READY, initialCapital: capital }, 4, false)).toBe(false);
  });
  it('isFalseWhenARunningJobExists', () => {
    expect(canStartSearch(READY, 4, true)).toBe(false);
  });
  it('isFalseWhenMinIsGreaterThanMax', () => {
    expect(canStartSearch({ ...READY, minMembers: 4, maxMembers: 3 }, 4, false)).toBe(false);
  });
  it('rejectsAMaxAboveTheBackendCap_ButAcceptsTheCap', () => {
    expect(sizeBoundsValid({ ...READY, maxMembers: 5 }, 4)).toBe(false);
    expect(sizeBoundsValid({ ...READY, maxMembers: 4 }, 4)).toBe(true);
    expect(sizeBoundsValid({ ...READY, maxMembers: 5 }, 5)).toBe(true);
  });
  it('rejectsAMinBelowTwo_AndNonIntegers', () => {
    expect(sizeBoundsValid({ ...READY, minMembers: 1 }, 4)).toBe(false);
    expect(sizeBoundsValid({ ...READY, minMembers: 2.5 }, 4)).toBe(false);
    expect(sizeBoundsValid({ ...READY, minMembers: null }, 4)).toBe(false);
  });
  it('requiresMaxPerInstrumentOfAtLeastOne_AndJudgesZeroByValue', () => {
    expect(canStartSearch({ ...READY, maxPerInstrument: 0 }, 4, false)).toBe(false);
    expect(canStartSearch({ ...READY, maxPerInstrument: 1 }, 4, false)).toBe(true);
  });
  it('keepsTheBudgetsWithinTheCeilings_AndAcceptsAbsent', () => {
    expect(canStartSearch({ ...READY, maxFullSimulations: 500 }, 4, false)).toBe(true);
    expect(canStartSearch({ ...READY, maxFullSimulations: 501 }, 4, false)).toBe(false);
    expect(canStartSearch({ ...READY, maxFullSimulations: 0 }, 4, false)).toBe(false);
    expect(canStartSearch({ ...READY, maxWallClockSeconds: 3600 }, 4, false)).toBe(true);
    expect(canStartSearch({ ...READY, maxWallClockSeconds: 3601 }, 4, false)).toBe(false);
  });
  it('rejectsACeilingOutsideZeroToOneHundred', () => {
    expect(canStartSearch({ ...READY, ceilingPercent: 0 }, 4, false)).toBe(false);
    expect(canStartSearch({ ...READY, ceilingPercent: 101 }, 4, false)).toBe(false);
    expect(canStartSearch({ ...READY, ceilingPercent: 100 }, 4, false)).toBe(true);
  });
});

describe('toSearchRequest', () => {
  it('convertsTheCeilingFromPercentToFractionExactlyOnce', () => {
    expect(toSearchRequest('acc', READY, 4, false)?.eliminationCeiling).toBe(0.05);
    expect(
      toSearchRequest('acc', { ...READY, ceilingPercent: 4.5 }, 4, false)?.eliminationCeiling,
    ).toBe(0.045);
  });
  it('sendsEveryKey_WithSharedBrokerAndGridConstants', () => {
    expect(toSearchRequest('acc', READY, 4, false)).toEqual({
      tradingAccountId: 'acc',
      minMembers: 2,
      maxMembers: 4,
      maxPerInstrument: 2,
      includeIdenticalDeployEval: false,
      onePercentRule: false,
      eliminationCeiling: 0.05,
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 25,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: IMOX_RETESTER_LOT_GRID.sizeDecimals,
      step: IMOX_RETESTER_LOT_GRID.step,
      minLot: IMOX_RETESTER_LOT_GRID.minLot,
      maxLots: IMOX_RETESTER_LOT_GRID.maxLots,
      maxFullSimulations: null,
      maxWallClockSeconds: null,
    });
  });
  it('sendsTheFxBandOnlyWhenShown', () => {
    const fx = { ...READY, fxLow: 1.05, fxHigh: 1.2 };
    expect(toSearchRequest('acc', fx, 4, false)).toMatchObject({ fxLow: null, fxHigh: null });
    expect(toSearchRequest('acc', fx, 4, true)).toMatchObject({ fxLow: 1.05, fxHigh: 1.2 });
  });
  it('excludeIdenticalMapsToTheInvertedWireFlag_AndBudgetsPassThrough', () => {
    const request = toSearchRequest(
      'acc',
      { ...READY, excludeIdentical: false, maxFullSimulations: 200, maxWallClockSeconds: 60 },
      4,
      false,
    )!;
    expect(request.includeIdenticalDeployEval).toBe(true);
    expect(request.maxFullSimulations).toBe(200);
    expect(request.maxWallClockSeconds).toBe(60);
  });
  it('isNullWhenTheFormCannotStart', () => {
    expect(toSearchRequest('acc', DEFAULT_SEARCH_FORM, 4, false)).toBeNull();
    expect(toSearchRequest(null, READY, 4, false)).toBeNull();
  });
});

describe('status and progress helpers', () => {
  it('onlyRunningIsRunning_AndZeroIsTerminal', () => {
    expect(isRunningStatus(FtmoGroupSearchStatus.Running)).toBe(true);
    expect(isRunningStatus(FtmoGroupSearchStatus.Unknown)).toBe(false);
    expect(isTerminalStatus(FtmoGroupSearchStatus.Unknown)).toBe(true);
    for (const s of [
      FtmoGroupSearchStatus.Completed,
      FtmoGroupSearchStatus.StoppedAtBudget,
      FtmoGroupSearchStatus.Cancelled,
      FtmoGroupSearchStatus.Failed,
    ]) {
      expect(isTerminalStatus(s)).toBe(true);
    }
    expect(isTerminalStatus(FtmoGroupSearchStatus.Running)).toBe(false);
  });
  it('progressPercentIsZeroWithoutTotal_AndClamped', () => {
    expect(progressPercent(0, 0)).toBe(0);
    expect(progressPercent(0, 10)).toBe(0);
    expect(progressPercent(50, 200)).toBe(25);
    expect(progressPercent(300, 200)).toBe(100);
  });
  it('formatsElapsedAsMinutesAndSeconds', () => {
    expect(formatElapsed(0)).toBe('0:00');
    expect(formatElapsed(65000)).toBe('1:05');
    expect(formatElapsed(900000)).toBe('15:00');
  });
});

describe('FX band validation (mirrors FtmoGroupSearchController)', () => {
  const fx = (fxLow: number | null, fxHigh: number | null): SearchFormValue => ({
    ...READY,
    fxLow,
    fxHigh,
  });

  it('acceptsBothEmptyOrAValidPair', () => {
    expect(fxBandValid(fx(null, null))).toBe(true);
    expect(fxBandValid(fx(1.05, 1.1))).toBe(true);
    expect(fxBandValid(fx(1.1, 1.1))).toBe(true);
  });

  it('rejectsAHalfPair_ANonPositiveLow_AndHighBelowLow', () => {
    expect(fxBandValid(fx(1.1, null))).toBe(false);
    expect(fxBandValid(fx(null, 1.1))).toBe(false);
    expect(fxBandValid(fx(0, 1.1))).toBe(false);
    expect(fxBandValid(fx(-1, 1.1))).toBe(false);
    expect(fxBandValid(fx(1.2, 1.1))).toBe(false);
  });

  it('blocksStartOnlyWhenTheFxInputsAreShown', () => {
    expect(canStartSearch(fx(1.1, null), 4, false, true)).toBe(false);
    expect(canStartSearch(fx(0, 1.1), 4, false, true)).toBe(false);
    expect(canStartSearch(fx(1.05, 1.1), 4, false, true)).toBe(true);
    expect(canStartSearch(fx(null, null), 4, false, true)).toBe(true);
    expect(canStartSearch(fx(1.1, null), 4, false, false)).toBe(true);
  });

  it('toSearchRequestReturnsNullForAnInvalidShownBand', () => {
    expect(toSearchRequest('a2', fx(1.2, 1.1), 4, true)).toBeNull();
    expect(toSearchRequest('a2', fx(1.2, 1.1), 4, false)!.fxLow).toBeNull();
  });
});
