import { describe, expect, it } from 'vitest';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';
import { refusedKind } from './ftmo-group-simulation.result.fixtures';
import { searchKind, searchRow } from './ftmo-group-search.fixtures';
import {
  SCATTER_LAYOUT,
  frontierFlags,
  niceAxis,
  toScatter,
} from './ftmo-group-search.scatter.mappers';

const D = BacktestRunKind.Deploy;
const E = BacktestRunKind.Evaluation;

/** A row whose two kinds have the given (breach per 1000, median days). */
function rowOf(rank: number, d: [number, number | null], e: [number, number | null]) {
  return searchRow({
    rank,
    memberIds: [`a${rank}`, `b${rank}`],
    memberNames: [`A${rank}`, `B${rank}`],
    kinds: [
      searchKind(D, { breached: d[0], medianDays: d[1] }),
      searchKind(E, { breached: e[0], medianDays: e[1] }),
    ],
  });
}

describe('frontierFlags', () => {
  it('keepsThePointsNoOtherPointBeatsOnBothAxes', () => {
    // (100d, 5%) (120d, 3%) (130d, 4%): the third is beaten by the second on both axes.
    expect(
      frontierFlags([
        { x: 100, y: 5 },
        { x: 120, y: 3 },
        { x: 130, y: 4 },
      ]),
    ).toEqual([true, true, false]);
  });

  it('isIndependentOfTheInputOrder', () => {
    expect(
      frontierFlags([
        { x: 130, y: 4 },
        { x: 120, y: 3 },
        { x: 100, y: 5 },
      ]),
    ).toEqual([false, true, true]);
  });

  it('aTieOnOneAxisIsDecidedByTheOther', () => {
    // Same days: the lower breach wins and the higher one is dominated.
    expect(
      frontierFlags([
        { x: 100, y: 5 },
        { x: 100, y: 3 },
      ]),
    ).toEqual([false, true]);
    // Same breach: the fewer days win and the slower one is dominated.
    expect(
      frontierFlags([
        { x: 100, y: 3 },
        { x: 120, y: 3 },
      ]),
    ).toEqual([true, false]);
  });

  it('exactDuplicatesShareTheStatus', () => {
    expect(
      frontierFlags([
        { x: 100, y: 3 },
        { x: 100, y: 3 },
        { x: 120, y: 5 },
        { x: 120, y: 5 },
      ]),
    ).toEqual([true, true, false, false]);
  });

  it('aZeroDaysPointIsAValidFrontierPoint', () => {
    expect(
      frontierFlags([
        { x: 0, y: 4 },
        { x: 10, y: 5 },
      ]),
    ).toEqual([true, false]);
  });

  it('aZeroBreachPointDominatesEverythingSlower', () => {
    expect(
      frontierFlags([
        { x: 50, y: 0 },
        { x: 60, y: 0 },
        { x: 40, y: 1 },
      ]),
    ).toEqual([true, false, true]);
  });

  it('isEmptyForNoPoints', () => {
    expect(frontierFlags([])).toEqual([]);
  });
});

describe('niceAxis', () => {
  it('roundsTheMaximumUpToANiceStepAndStartsAtZero', () => {
    const axis = niceAxis(87);
    expect(axis.ticks).toEqual([0, 20, 40, 60, 80, 100]);
    expect(axis.max).toBe(100);
  });

  it('usesOneTwoFiveSteps', () => {
    expect(niceAxis(4.5).ticks).toEqual([0, 1, 2, 3, 4, 5]);
    expect(niceAxis(12).ticks).toEqual([0, 2, 4, 6, 8, 10, 12]);
  });

  it('aMaximumOfZeroStillHasADrawableAxis', () => {
    const axis = niceAxis(0);
    expect(axis.max).toBeGreaterThan(0);
    expect(axis.ticks[0]).toBe(0);
  });

  it('theMaximumIsAlwaysCoveredByTheLastTick', () => {
    for (const max of [0.3, 1, 7, 99, 100, 101, 365, 4321]) {
      const axis = niceAxis(max);
      expect(axis.ticks[axis.ticks.length - 1]).toBe(axis.max);
      expect(axis.max).toBeGreaterThanOrEqual(max);
    }
  });
});

describe('toScatter', () => {
  it('plotsTheWorseKindOnBothAxes_BreachConvertedToPercentOnce', () => {
    const vm = toScatter([rowOf(1, [45, 120], [30, 80])], 0.05);
    expect(vm.points).toHaveLength(1);
    expect(vm.points[0].days).toBe(120);
    expect(vm.points[0].breachPercent).toBe(4.5);
    expect(vm.points[0].memberNames).toEqual(['A1', 'B1']);
  });

  it('theWorseKindCanBeEitherOne', () => {
    const vm = toScatter([rowOf(1, [10, 60], [90, 200])], 0.5);
    expect(vm.points[0].days).toBe(200);
    expect(vm.points[0].breachPercent).toBe(9);
  });

  it('aZeroMedianAndAZeroBreachAreRealValues_NotMissing', () => {
    const vm = toScatter([rowOf(1, [0, 0], [0, 0])], 0.05);
    expect(vm.points).toHaveLength(1);
    expect(vm.points[0].days).toBe(0);
    expect(vm.points[0].breachPercent).toBe(0);
    expect(vm.notPlotted).toBe(0);
  });

  it('rowsMissingAMedianOrAKindAreCountedNotPlacedAtZero', () => {
    const rows = [
      rowOf(1, [10, 100], [10, 100]),
      rowOf(2, [10, null], [10, 100]),
      rowOf(3, [10, 100], [10, null]),
      searchRow({
        rank: 4,
        kinds: [
          searchKind(D, { breached: 10, medianDays: 90 }),
          refusedKind(E, FtmoGroupRefusal.NoCommonWindow),
        ],
      }),
      searchRow({ rank: 5, kinds: [searchKind(D, { breached: 10, medianDays: 90 })] }),
    ];
    const vm = toScatter(rows, 0.05);
    expect(vm.points.map((p) => p.row.rank)).toEqual([1]);
    expect(vm.notPlotted).toBe(4);
  });

  it('marksTheFrontierAndTheCeiling', () => {
    const vm = toScatter(
      [
        rowOf(1, [50, 100], [50, 100]),
        rowOf(2, [30, 120], [30, 120]),
        rowOf(3, [40, 130], [40, 130]),
      ],
      0.04,
    );
    expect(vm.points.map((p) => p.frontier)).toEqual([true, true, false]);
    // 5% is above the 4% ceiling; 3% and 4% are within it (inclusive).
    expect(vm.points.map((p) => p.withinCeiling)).toEqual([false, true, true]);
  });

  it('aNullCeilingHighlightsNothing', () => {
    const vm = toScatter([rowOf(1, [1, 10], [1, 10])], null);
    expect(vm.points[0].withinCeiling).toBe(false);
  });

  it('positionsStayInsideThePlotArea_AndAreDeterministic', () => {
    const rows = [rowOf(1, [45, 120], [30, 80]), rowOf(2, [10, 40], [10, 40])];
    const a = toScatter(rows, 0.05);
    const b = toScatter(rows, 0.05);
    expect(a).toEqual(b);
    const { left, top, width, height } = SCATTER_LAYOUT;
    for (const p of a.points) {
      expect(p.cx).toBeGreaterThanOrEqual(left);
      expect(p.cx).toBeLessThanOrEqual(left + width);
      expect(p.cy).toBeGreaterThanOrEqual(top);
      expect(p.cy).toBeLessThanOrEqual(top + height);
    }
  });

  it('theLargestValuesSitOnTheFarEdgesOfTheirAxes_AndZeroOnTheNearOnes', () => {
    const { left, top, width, height } = SCATTER_LAYOUT;
    const vm = toScatter([rowOf(1, [0, 0], [0, 0]), rowOf(2, [100, 100], [100, 100])], 0.05);
    const [zero, big] = vm.points;
    expect(zero.cx).toBe(left);
    expect(zero.cy).toBe(top + height);
    expect(vm.xAxis.max).toBe(100);
    expect(vm.yAxis.max).toBe(10);
    expect(big.cx).toBe(left + width);
    expect(big.cy).toBe(top);
  });

  it('noRowsGivesAnEmptyChart', () => {
    const vm = toScatter([], 0.05);
    expect(vm.points).toEqual([]);
    expect(vm.notPlotted).toBe(0);
  });
});
