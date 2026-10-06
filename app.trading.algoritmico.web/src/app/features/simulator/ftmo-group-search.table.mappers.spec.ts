import { describe, expect, it } from 'vitest';
import {
  FtmoGroupSearchIneligibleReason,
  FtmoGroupSearchRowDto,
} from '../../core/models/ftmo-group-search.model';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { searchKind, searchRow } from './ftmo-group-search.fixtures';
import { refusedKind } from './ftmo-group-simulation.result.fixtures';
import { ineligibleCounts, toTableRows } from './ftmo-group-search.table.mappers';

const D = BacktestRunKind.Deploy;
const E = BacktestRunKind.Evaluation;

function withShares(deploy: number, evaluation: number): FtmoGroupSearchRowDto {
  return searchRow({
    kinds: [
      searchKind(D, { breached: deploy * 1000 }),
      searchKind(E, { breached: evaluation * 1000 }),
    ],
  });
}

describe('toTableRows', () => {
  it('keepsTheBackendOrder_NeverReSorts', () => {
    const rows = [searchRow({ rank: 3 }), searchRow({ rank: 1 }), searchRow({ rank: 2 })];
    expect(toTableRows(rows, 0.05, false).map((r) => r.rank)).toEqual([3, 1, 2]);
  });

  it('showsEachKindOnItsOwn_ConvertedToPercentOnce', () => {
    const [row] = toTableRows([searchRow()], 0.05, false);
    const [deploy, evaluation] = row.kinds;
    expect(deploy.breach).toBe(4.5);
    expect(deploy.worstDaily).toBe(45);
    expect(deploy.worstDrawdown).toBe(80);
    expect(deploy.medianDrawdown).toBe(30);
    expect(deploy.fundedNoBreach).toBe(70);
    expect(deploy.medianDays).toEqual({ kind: 'value', value: 120 });
    expect(evaluation.breach).toBe(3);
    expect(evaluation.worstDrawdown).toBe(60);
  });

  it('aRealZeroMedianStaysZero_AndANullMedianIsAbsent', () => {
    const [row] = toTableRows([searchRow()], 0.05, false);
    expect(row.kinds[1].medianDays).toEqual({ kind: 'value', value: 0 });
    const [absent] = toTableRows(
      [searchRow({ kinds: [searchKind(D, { medianDays: null }), searchKind(E)] })],
      0.05,
      false,
    );
    expect(absent.kinds[0].medianDays).toEqual({ kind: 'absent' });
  });

  it('aRealZeroShareStaysZero', () => {
    const [row] = toTableRows(
      [searchRow({ kinds: [searchKind(D, { breached: 0, fundedNoBreach: 0 }), searchKind(E)] })],
      0.05,
      false,
    );
    expect(row.kinds[0].breach).toBe(0);
    expect(row.kinds[0].fundedNoBreach).toBe(0);
  });

  it('aMissingHeadroomIsAbsent_NotZero', () => {
    const [row] = toTableRows([searchRow({ kindHeadrooms: [] })], 0.05, false);
    expect(row.kinds[0].worstDaily).toBeNull();
    expect(row.kinds[0].worstDrawdown).toBeNull();
    expect(row.kinds[0].medianDrawdown).toBeNull();
  });

  it('aRefusedKindCarriesItsRefusal_AndNoNumbers', () => {
    const [row] = toTableRows(
      [searchRow({ kinds: [searchKind(D), refusedKind(E, FtmoGroupRefusal.NoCommonWindow)] })],
      0.05,
      false,
    );
    const evaluation = row.kinds[1];
    expect(evaluation.state).toBe('refused');
    expect(evaluation.refusal?.key).toBe('SIMULATOR.FTMO_GROUP.RESULT.REFUSAL.NO_COMMON_WINDOW');
    expect(evaluation.breach).toBeNull();
    expect(row.kinds[0].state).toBe('evaluated');
  });

  it('aRefusalWithValueZeroIsNotReadAsAbsent', () => {
    const [row] = toTableRows(
      [searchRow({ kinds: [searchKind(D), refusedKind(E, FtmoGroupRefusal.InvalidRequest)] })],
      0.05,
      false,
    );
    expect(row.kinds[1].refusal?.key).toBe('SIMULATOR.FTMO_GROUP.RESULT.REFUSAL.INVALID_REQUEST');
  });

  it('aKindMissingFromTheRowIsNoRun', () => {
    const [row] = toTableRows([searchRow({ kinds: [searchKind(D)] })], 0.05, false);
    expect(row.kinds[1].state).toBe('noRun');
    expect(row.kinds[1].kind).toBe(E);
  });

  it('nullSymbolsBecomeAnEmptyList', () => {
    expect(toTableRows([searchRow({ symbols: null })], 0.05, false)[0].symbols).toEqual([]);
  });
});

describe('ceiling', () => {
  it('highlightsByTheWorseKind_AtFivePercent', () => {
    const rows = [withShares(0.03, 0.06), withShares(0.03, 0.04)];
    expect(toTableRows(rows, 0.05, false).map((r) => r.withinCeiling)).toEqual([false, true]);
  });

  it('ignoresTheServerFlag_AndRecomputesAtTenPercent', () => {
    const rows = [searchRow({ ...withShares(0.03, 0.06), withinCeiling: false })];
    expect(toTableRows(rows, 0.05, false)[0].withinCeiling).toBe(false);
    expect(toTableRows(rows, 0.1, false)[0].withinCeiling).toBe(true);
  });

  it('theFilterListsOnlyHighlightedRows_AndOffKeepsEveryRowInOrder', () => {
    const rows = [
      searchRow({ ...withShares(0.03, 0.06), rank: 1 }),
      searchRow({ ...withShares(0.03, 0.04), rank: 2 }),
    ];
    expect(toTableRows(rows, 0.05, true).map((r) => r.rank)).toEqual([2]);
    expect(toTableRows(rows, 0.05, false).map((r) => r.rank)).toEqual([1, 2]);
    expect(rows).toHaveLength(2);
  });

  it('aNullCeilingHighlightsNothing', () => {
    expect(toTableRows([withShares(0, 0)], null, false)[0].withinCeiling).toBe(false);
  });
});

describe('ineligibleCounts', () => {
  it('groupsByReason_WithCounts_UnknownKeepsTheRawValue', () => {
    const mk = (reason: number) => ({ strategyId: 'x', name: 'x', reason, refusal: null });
    const out = ineligibleCounts([
      mk(FtmoGroupSearchIneligibleReason.MissingKind),
      mk(FtmoGroupSearchIneligibleReason.MissingKind),
      mk(FtmoGroupSearchIneligibleReason.Unknown),
    ]);
    expect(out).toEqual([
      {
        reason: { key: 'SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON.MISSING_KIND', raw: null },
        count: 2,
      },
      { reason: { key: 'SIMULATOR.FTMO_SEARCH.INELIGIBLE_REASON.UNKNOWN', raw: 0 }, count: 1 },
    ]);
  });
});
