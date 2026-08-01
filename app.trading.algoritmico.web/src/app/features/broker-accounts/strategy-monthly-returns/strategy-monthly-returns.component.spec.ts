import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { StrategyMonthlyReturnsComponent } from './strategy-monthly-returns.component';
import {
  StrategyService,
  StrategyMonthlyReturnsDto,
  MonthlyReturnDto,
} from '../../../core/services/strategy.service';

const CURRENT_YEAR = new Date().getFullYear();

function makeReturn(overrides: Partial<MonthlyReturnDto> = {}): MonthlyReturnDto {
  return {
    year: CURRENT_YEAR,
    month: 1,
    equityStart: 10000,
    equityEnd: 10150,
    profit: 150,
    returnPercent: 0.015,
    tradeCount: 2,
    ...overrides,
  };
}

function makeRow(overrides: Partial<StrategyMonthlyReturnsDto> = {}): StrategyMonthlyReturnsDto {
  return {
    strategyId: 's-1',
    name: 'Alpha',
    symbol: 'EURUSD',
    returns: [makeReturn()],
    ...overrides,
  };
}

describe('StrategyMonthlyReturnsComponent', () => {
  let serviceMock: { getMonthlyReturnsByAccount: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    serviceMock = { getMonthlyReturnsByAccount: vi.fn() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [StrategyMonthlyReturnsComponent],
      providers: [{ provide: StrategyService, useValue: serviceMock }],
    });
  });

  function create(rows: StrategyMonthlyReturnsDto[] = [makeRow()]) {
    serviceMock.getMonthlyReturnsByAccount.mockReturnValue(of(rows));
    const fixture = TestBed.createComponent(StrategyMonthlyReturnsComponent);
    fixture.componentRef.setInput('accountId', 'acc-1');
    fixture.detectChanges();
    return fixture;
  }

  it('loadsRowsForTheGivenAccount', () => {
    const fixture = create();
    expect(serviceMock.getMonthlyReturnsByAccount).toHaveBeenCalledWith('acc-1');
    expect(fixture.componentInstance.rows()).toHaveLength(1);
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });

  it('defaultsSelectedYearToCurrentYear', () => {
    const fixture = create();
    expect(fixture.componentInstance.selectedYear()).toBe(CURRENT_YEAR);
  });

  it('viewRows_MapsMonthsOfSelectedYearIntoTwelveSlots', () => {
    const fixture = create([
      makeRow({
        returns: [
          makeReturn({ month: 4, returnPercent: -0.0004 }),
          makeReturn({ month: 5, returnPercent: 0.0103 }),
          makeReturn({ year: CURRENT_YEAR - 1, month: 1, returnPercent: 0.5 }),
        ],
      }),
    ]);

    const row = fixture.componentInstance.viewRows()[0];
    expect(row.months).toHaveLength(12);
    expect(row.months[3]).toBe(-0.0004);
    expect(row.months[4]).toBe(0.0103);
    // Previous-year data must not leak into the selected year.
    expect(row.months[0]).toBeNull();
  });

  it('viewRows_TotalIsCompoundedAcrossMonths', () => {
    const fixture = create([
      makeRow({
        returns: [
          makeReturn({ month: 1, returnPercent: 0.1 }),
          makeReturn({ month: 2, returnPercent: 0.2 }),
        ],
      }),
    ]);

    const row = fixture.componentInstance.viewRows()[0];
    // (1 + 0.1) * (1 + 0.2) - 1 = 0.32
    expect(row.total).toBeCloseTo(0.32, 10);
    expect(row.hasData).toBe(true);
  });

  it('viewRows_StrategyWithoutDataForYear_HasNullMonthsAndNoData', () => {
    const fixture = create([makeRow({ returns: [] })]);

    const row = fixture.componentInstance.viewRows()[0];
    expect(row.months.every((m) => m === null)).toBe(true);
    expect(row.hasData).toBe(false);
  });

  it('yearNavigation_RespectsDataBounds', () => {
    const fixture = create([
      makeRow({
        returns: [
          makeReturn({ year: CURRENT_YEAR - 2, month: 6 }),
          makeReturn({ year: CURRENT_YEAR, month: 1 }),
        ],
      }),
    ]);
    const comp = fixture.componentInstance;

    // Current year is the max bound — cannot go forward.
    expect(comp.canNext()).toBe(false);
    comp.nextYear();
    expect(comp.selectedYear()).toBe(CURRENT_YEAR);

    // Can go back to the earliest year with data, but not beyond.
    comp.prevYear();
    comp.prevYear();
    expect(comp.selectedYear()).toBe(CURRENT_YEAR - 2);
    expect(comp.canPrev()).toBe(false);
    comp.prevYear();
    expect(comp.selectedYear()).toBe(CURRENT_YEAR - 2);
  });

  it('sortBy_SameColumnTwice_TogglesDirection', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.sortBy('name');
    expect(comp.sortKey()).toBe('name');
    expect(comp.sortDir()).toBe('asc');

    comp.sortBy('name');
    expect(comp.sortDir()).toBe('desc');
  });

  it('sortBy_NumericColumn_DefaultsToDescending', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.sortBy('total');
    expect(comp.sortDir()).toBe('desc');

    comp.sortBy(5);
    expect(comp.sortKey()).toBe(5);
    expect(comp.sortDir()).toBe('desc');
  });

  it('sortedRows_ByName_SortsAlphabetically', () => {
    const fixture = create([
      makeRow({ strategyId: 's-1', name: 'Zeta' }),
      makeRow({ strategyId: 's-2', name: 'Alpha' }),
    ]);
    const comp = fixture.componentInstance;

    comp.sortBy('name');
    expect(comp.sortedRows().map((r) => r.name)).toEqual(['Alpha', 'Zeta']);

    comp.sortBy('name');
    expect(comp.sortedRows().map((r) => r.name)).toEqual(['Zeta', 'Alpha']);
  });

  it('sortedRows_ByMonth_SortsByValueWithNullsAlwaysLast', () => {
    const fixture = create([
      makeRow({ strategyId: 's-1', name: 'NoData', returns: [] }),
      makeRow({
        strategyId: 's-2',
        name: 'Low',
        returns: [makeReturn({ month: 6, returnPercent: 0.01 })],
      }),
      makeRow({
        strategyId: 's-3',
        name: 'High',
        returns: [makeReturn({ month: 6, returnPercent: 0.05 })],
      }),
    ]);
    const comp = fixture.componentInstance;

    // June is month index 5. Descending: highest first, no-data row last.
    comp.sortBy(5);
    expect(comp.sortedRows().map((r) => r.name)).toEqual(['High', 'Low', 'NoData']);

    // Ascending flips the values but keeps null rows at the bottom.
    comp.sortBy(5);
    expect(comp.sortedRows().map((r) => r.name)).toEqual(['Low', 'High', 'NoData']);
  });

  it('sortedRows_ByTotal_TreatsRowsWithoutDataAsNull', () => {
    const fixture = create([
      makeRow({ strategyId: 's-1', name: 'NoData', returns: [] }),
      makeRow({
        strategyId: 's-2',
        name: 'Winner',
        returns: [makeReturn({ month: 1, returnPercent: 0.1 })],
      }),
      makeRow({
        strategyId: 's-3',
        name: 'Loser',
        returns: [makeReturn({ month: 1, returnPercent: -0.05 })],
      }),
    ]);
    const comp = fixture.componentInstance;

    comp.sortBy('total');
    expect(comp.sortedRows().map((r) => r.name)).toEqual(['Winner', 'Loser', 'NoData']);
  });

  it('sortedRows_WithoutSortKey_KeepsServerOrder', () => {
    const fixture = create([
      makeRow({ strategyId: 's-1', name: 'Zeta' }),
      makeRow({ strategyId: 's-2', name: 'Alpha' }),
    ]);

    expect(fixture.componentInstance.sortedRows().map((r) => r.name)).toEqual(['Zeta', 'Alpha']);
  });

  it('serviceError_SetsErrorAndStopsLoading', () => {
    serviceMock.getMonthlyReturnsByAccount.mockReturnValue(throwError(() => new Error('boom')));
    const fixture = TestBed.createComponent(StrategyMonthlyReturnsComponent);
    fixture.componentRef.setInput('accountId', 'acc-1');
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBeTruthy();
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });
});
