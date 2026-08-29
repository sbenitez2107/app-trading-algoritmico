import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { StrategyMonthlyReturnsComponent } from './strategy-monthly-returns.component';
import { AccountType } from '../../../core/services/portfolio.service';
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
    maxDrawdownPercent: 0.004,
    underwaterPercent: 0.004,
    winCount: 1,
    lossCount: 1,
    ...overrides,
  };
}

function makeRow(overrides: Partial<StrategyMonthlyReturnsDto> = {}): StrategyMonthlyReturnsDto {
  return {
    strategyId: 's-1',
    name: 'Alpha',
    symbol: 'EURUSD',
    timeframe: 'H4',
    returns: [makeReturn()],
    ...overrides,
  };
}

describe('StrategyMonthlyReturnsComponent', () => {
  let serviceMock: { getMonthlyReturnsByAccount: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    serviceMock = { getMonthlyReturnsByAccount: vi.fn() };

    localStorage.clear();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [StrategyMonthlyReturnsComponent],
      providers: [
        provideRouter([]),
        { provide: StrategyService, useValue: serviceMock },
        // The component sits inside the account-detail route, which carries the broker base path.
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { data: { basePath: '/darwinex', broker: 'Darwinex' } } },
        },
      ],
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

  it('metric_RestoresTheRememberedChoice_OnANewInstance', () => {
    // Written before creation on purpose: the signal reads storage at construction time.
    localStorage.setItem('monthly_metric_strategies', 'underwater');
    const fixture = create();

    expect(fixture.componentInstance.metric()).toBe('underwater');
  });

  it('setMetric_RemembersTheChoice', () => {
    const fixture = create();
    fixture.componentInstance.setMetric('winRate');

    expect(localStorage.getItem('monthly_metric_strategies')).toBe('winRate');
  });

  it('metric_IgnoresAStoredValue_ThatIsNoLongerAMetric', () => {
    localStorage.setItem('monthly_metric_strategies', 'sharpe');
    const fixture = create();

    expect(fixture.componentInstance.metric()).toBe('return');
  });

  it('metric_UsesItsOwnKey_SoThePortfoliosMatrixIsIndependent', () => {
    localStorage.setItem('monthly_metric_portfolios', 'maxDrawdown');
    const fixture = create();

    expect(fixture.componentInstance.metric()).toBe('return');
  });

  it('metric_DefaultsToReturn', () => {
    const fixture = create();
    expect(fixture.componentInstance.metric()).toBe('return');
  });

  it('setMetric_SwapsTheValueRenderedInEveryCell', () => {
    const fixture = create([
      makeRow({
        returns: [
          makeReturn({
            month: 1,
            returnPercent: 0.05,
            maxDrawdownPercent: 0.02,
            underwaterPercent: 0.09,
          }),
        ],
      }),
    ]);
    const cmp = fixture.componentInstance;

    expect(cmp.viewRows()[0].months[0]).toBe(0.05);

    cmp.setMetric('maxDrawdown');
    expect(cmp.viewRows()[0].months[0]).toBe(0.02);

    cmp.setMetric('underwater');
    expect(cmp.viewRows()[0].months[0]).toBe(0.09);
  });

  it('setMetric_WinRate_DerivesTheRateAndExposesTheRawCounts', () => {
    const fixture = create([
      makeRow({ returns: [makeReturn({ month: 1, winCount: 3, lossCount: 1 })] }),
    ]);
    const cmp = fixture.componentInstance;
    cmp.setMetric('winRate');

    expect(cmp.viewRows()[0].months[0]).toBe(0.75);
    expect(cmp.viewRows()[0].tooltips[0]).toBe('3 W / 1 L');
  });

  it('total_TakesTheWorstMonth_ForDrawdownMetrics_NotACompoundedSum', () => {
    const fixture = create([
      makeRow({
        returns: [
          makeReturn({ month: 1, underwaterPercent: 0.03 }),
          makeReturn({ month: 2, underwaterPercent: 0.08 }),
        ],
      }),
    ]);
    const cmp = fixture.componentInstance;
    cmp.setMetric('underwater');

    expect(cmp.viewRows()[0].total).toBe(0.08);
  });

  it('sortBy_DefaultsToAscending_WhenLowerIsBetter', () => {
    const fixture = create();
    const cmp = fixture.componentInstance;

    cmp.sortBy('total');
    expect(cmp.sortDir()).toBe('desc');

    cmp.setMetric('maxDrawdown');
    // The same column now ranks drawdowns, where the SMALLEST value is the best row.
    expect(cmp.sortDir()).toBe('asc');
  });

  it('setMetric_LeavesTextColumnSortUntouched', () => {
    const fixture = create();
    const cmp = fixture.componentInstance;

    cmp.sortBy('name');
    cmp.setMetric('maxDrawdown');

    expect(cmp.sortKey()).toBe('name');
    expect(cmp.sortDir()).toBe('asc');
  });

  it('serviceError_SetsErrorAndStopsLoading', () => {
    serviceMock.getMonthlyReturnsByAccount.mockReturnValue(throwError(() => new Error('boom')));
    const fixture = TestBed.createComponent(StrategyMonthlyReturnsComponent);
    fixture.componentRef.setInput('accountId', 'acc-1');
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBeTruthy();
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });

  // --- Filters -------------------------------------------------------------

  const FILTER_ROWS: StrategyMonthlyReturnsDto[] = [
    makeRow({
      strategyId: 's-1',
      name: 'ORIGINAL_NQ_H4_BB_V',
      symbol: 'USATECHIDXUSD_M1_UTC02',
      timeframe: 'H4',
      returns: [makeReturn({ month: 6, returnPercent: 0.02, winCount: 3, lossCount: 1 })],
    }),
    makeRow({
      strategyId: 's-2',
      name: 'ORIGINAL_XAUUSD_H1_BP',
      symbol: 'XAUUSD_M1_UTC02',
      timeframe: 'H1',
      returns: [makeReturn({ month: 6, returnPercent: -0.03, winCount: 1, lossCount: 3 })],
    }),
    makeRow({
      strategyId: 's-3',
      name: 'ORIGINAL_NQ_H4_SC_BP',
      symbol: 'USATECHIDXUSD_M1_UTC02',
      timeframe: null,
      returns: [],
    }),
  ];

  it('availableSymbols_ListsDistinctSymbolsAlphabetically', () => {
    const cmp = create(FILTER_ROWS).componentInstance;
    expect(cmp.availableSymbols()).toEqual(['USATECHIDXUSD_M1_UTC02', 'XAUUSD_M1_UTC02']);
  });

  it('nameFilter_MatchesCaseInsensitiveSubstring', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.nameFilter.set('xauusd_h1');

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-2']);
  });

  it('nameFilter_IgnoresSurroundingWhitespace', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.nameFilter.set('   ');

    expect(cmp.filteredRows()).toHaveLength(3);
  });

  it('symbolFilter_KeepsOnlyRowsOfTheSelectedSymbol', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.symbolFilter.set('USATECHIDXUSD_M1_UTC02');

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1', 's-3']);
  });

  it('totalFilter_Positive_KeepsOnlyRowsWhoseTotalReadsGreen', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.totalFilter.set('pos');

    // s-3 has no total at all, so it is neither positive nor negative.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1']);
  });

  it('totalFilter_Negative_KeepsOnlyRowsWhoseTotalReadsRed', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.totalFilter.set('neg');

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-2']);
  });

  it('totalFilter_FollowsTheMetricNeutralPointOnWinRate', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.setMetric('winRate');
    cmp.totalFilter.set('pos');

    // 3/4 sits above the 50% neutral point; 1/4 sits below it.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1']);
  });

  it('totalFilter_Positive_YieldsNothingOnDrawdownMetrics', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.setMetric('maxDrawdown');
    cmp.totalFilter.set('pos');

    // A drawdown depth is never good news, so it never renders green.
    expect(cmp.filteredRows()).toHaveLength(0);
  });

  it('filters_CombineWithAnd', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.nameFilter.set('original');
    cmp.symbolFilter.set('USATECHIDXUSD_M1_UTC02');
    cmp.totalFilter.set('pos');

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1']);
  });

  it('sortedRows_SortsTheFilteredSetOnly', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.symbolFilter.set('USATECHIDXUSD_M1_UTC02');
    cmp.sortBy('name');

    expect(cmp.sortedRows().map((r) => r.strategyId)).toEqual(['s-1', 's-3']);
  });

  it('hasActiveFilters_TracksWhetherAnyFilterNarrowsTheMatrix', () => {
    const cmp = create(FILTER_ROWS).componentInstance;
    expect(cmp.hasActiveFilters()).toBe(false);

    cmp.totalFilter.set('neg');
    expect(cmp.hasActiveFilters()).toBe(true);
  });

  it('clearFilters_RestoresTheFullMatrix', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.nameFilter.set('xauusd');
    cmp.symbolFilter.set('XAUUSD_M1_UTC02');
    cmp.totalFilter.set('neg');

    cmp.clearFilters();

    expect(cmp.nameFilter()).toBe('');
    expect(cmp.symbolFilter()).toBeNull();
    expect(cmp.totalFilter()).toBe('all');
    expect(cmp.filteredRows()).toHaveLength(3);
  });

  it('symbolFilter_IgnoresASymbolThatNoLongerExistsInTheData', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.symbolFilter.set('GONE');

    // A stale selection must not silently blank the matrix.
    expect(cmp.filteredRows()).toHaveLength(3);
  });

  it('viewRows_RenderTheWinRateWithItsTradeCounts', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.setMetric('winRate');
    const row = cmp.viewRows()[0];

    expect(row.monthTexts[5]).toBe('3/1 (75%)');
    expect(row.totalText).toBe('3/1 (75%)');
  });

  it('viewRows_KeepThePlainPercentageOnTheOtherMetrics', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    const row = cmp.viewRows()[0];

    expect(row.monthTexts[5]).toBe('2.00%');
  });

  // --- Timeframe, Max DD and portfolio hand-off ----------------------------

  it('viewRows_CarryTheStrategyTimeframe', () => {
    const cmp = create(FILTER_ROWS).componentInstance;
    expect(cmp.viewRows().map((r) => r.timeframe)).toEqual(['H4', 'H1', null]);
  });

  it('availableTimeframes_ListsDistinctValuesAndSkipsTheMissingOnes', () => {
    const cmp = create(FILTER_ROWS).componentInstance;
    expect(cmp.availableTimeframes()).toEqual(['H1', 'H4']);
  });

  it('timeframeFilter_KeepsOnlyRowsOfTheSelectedTimeframe', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.timeframeFilter.set('H4');

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1']);
  });

  it('sortBy_Timeframe_OrdersTheColumn', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.sortBy('timeframe');

    // The row without a timeframe sinks, like every other row without a value.
    expect(cmp.sortedRows().map((r) => r.strategyId)).toEqual(['s-2', 's-1', 's-3']);
  });

  it('maxDdFilter_ExcludesAStrategyWithASingleMonthOverTheThreshold', () => {
    const cmp = create([
      makeRow({
        strategyId: 'd-shallow',
        returns: [
          makeReturn({ month: 6, maxDrawdownPercent: 0.02 }),
          makeReturn({ month: 7, maxDrawdownPercent: 0.03 }),
        ],
      }),
      makeRow({
        strategyId: 'd-onedeepmonth',
        returns: [
          makeReturn({ month: 6, maxDrawdownPercent: 0.02 }),
          makeReturn({ month: 7, maxDrawdownPercent: 0.12 }),
        ],
      }),
    ]).componentInstance;

    // 10 means 10%, against fractions of 0.03 and 0.12.
    cmp.maxDdFilter.set(10);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['d-shallow']);
  });

  it('maxDdFilter_ExcludesStrategiesWithNoMonthToJudge', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.maxDdFilter.set(100);

    // s-3 has no months in the selected year, so nothing proves it clears the bar.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1', 's-2']);
  });

  it('maxDdFilter_IsIndependentOfTheSelectedMetric', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.setMetric('winRate');
    cmp.maxDdFilter.set(0.5);

    // The default fixture months carry a 0.4% drawdown, so the bar still bites on the W/L tab.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['s-1', 's-2']);
  });

  it('setMaxDdFilter_TreatsBlankAndNonPositiveInputAsNoFilter', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.setMaxDdFilter('');
    expect(cmp.maxDdFilter()).toBeNull();

    // A bar at 0% is one no month can clear, so it disables rather than blanking the matrix.
    cmp.setMaxDdFilter('0');
    expect(cmp.maxDdFilter()).toBeNull();

    cmp.setMaxDdFilter('abc');
    expect(cmp.maxDdFilter()).toBeNull();

    cmp.setMaxDdFilter('7.5');
    expect(cmp.maxDdFilter()).toBe(7.5);
  });

  it('clearFilters_AlsoResetsTimeframeAndMaxDd', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    cmp.timeframeFilter.set('H4');
    cmp.maxDdFilter.set(5);
    expect(cmp.hasActiveFilters()).toBe(true);

    cmp.clearFilters();

    expect(cmp.timeframeFilter()).toBeNull();
    expect(cmp.maxDdFilter()).toBeNull();
    expect(cmp.filteredRows()).toHaveLength(3);
  });

  it('openCreatePortfolio_FreezesTheFilteredStrategiesForTheDialog', () => {
    const fixture = create(FILTER_ROWS);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('broker', 'Darwinex');
    fixture.componentRef.setInput('accountType', AccountType.Demo);

    cmp.symbolFilter.set('USATECHIDXUSD_M1_UTC02');
    cmp.openCreatePortfolio();
    // Editing a filter behind the dialog must not change what gets created.
    cmp.symbolFilter.set(null);

    expect(cmp.isCreatingPortfolio()).toBe(true);
    expect(cmp.portfolioStrategyIds()).toEqual(['s-1', 's-3']);
  });

  it('canCreatePortfolio_StaysFalseUntilTheAccountTypeIsKnown', () => {
    const fixture = create(FILTER_ROWS);
    const cmp = fixture.componentInstance;

    // Broker and account type are not ours to guess, so the button stays hidden.
    expect(cmp.canCreatePortfolio()).toBe(false);

    fixture.componentRef.setInput('broker', 'Darwinex');
    expect(cmp.canCreatePortfolio()).toBe(false);

    fixture.componentRef.setInput('accountType', AccountType.Demo);
    expect(cmp.canCreatePortfolio()).toBe(true);
  });

  it('canCreatePortfolio_IsFalseWhenTheFilterMatchesNoStrategy', () => {
    const fixture = create(FILTER_ROWS);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('broker', 'Darwinex');
    fixture.componentRef.setInput('accountType', AccountType.Demo);

    cmp.nameFilter.set('nothing matches this');

    expect(cmp.canCreatePortfolio()).toBe(false);
  });

  it('openCreatePortfolio_DoesNothingWhenItCannotCreate', () => {
    const cmp = create(FILTER_ROWS).componentInstance;

    // No account type set: the dialog must not open.
    cmp.openCreatePortfolio();

    expect(cmp.isCreatingPortfolio()).toBe(false);
  });

  it('onPortfolioCreated_ClosesTheDialogAndOpensTheNewPortfolio', () => {
    const fixture = create(FILTER_ROWS);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('broker', 'Darwinex');
    fixture.componentRef.setInput('accountType', AccountType.Demo);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    cmp.openCreatePortfolio();
    cmp.onPortfolioCreated('p-1');

    expect(cmp.isCreatingPortfolio()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/darwinex/portfolios', 'p-1']);
  });

  // --- Per-month gates: EVERY month with data must clear the threshold -----

  const GATE_ROWS: StrategyMonthlyReturnsDto[] = [
    // Every month positive, worst is +0.30%.
    makeRow({
      strategyId: 'g-steady',
      name: 'Steady',
      returns: [
        makeReturn({ month: 6, returnPercent: 0.012, winCount: 6, lossCount: 2 }),
        makeReturn({ month: 7, returnPercent: 0.003, winCount: 3, lossCount: 2 }),
      ],
    }),
    // Strong year overall, but June bled: exactly the row a TOTAL filter would let through.
    makeRow({
      strategyId: 'g-onebadmonth',
      name: 'One bad month',
      returns: [
        makeReturn({ month: 6, returnPercent: -0.005, winCount: 1, lossCount: 2 }),
        makeReturn({ month: 7, returnPercent: 0.04, winCount: 8, lossCount: 1 }),
      ],
    }),
    // No months at all in the selected year.
    makeRow({ strategyId: 'g-nodata', name: 'No data', returns: [] }),
  ];

  it('minReturnFilter_ExcludesAStrategyWithASingleMonthBelowTheThreshold', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.minReturnFilter.set(0);

    // 'One bad month' has a great year total; one -0.50% month still disqualifies it.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['g-steady']);
  });

  it('minReturnFilter_IsStrictlyGreaterThan', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    // 'Steady' bottoms out at exactly 0.30%, so a 0.3 threshold must reject it.
    cmp.minReturnFilter.set(0.3);
    expect(cmp.filteredRows()).toHaveLength(0);

    cmp.minReturnFilter.set(0.29);
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['g-steady']);
  });

  it('minWinRateFilter_ExcludesAStrategyWithASingleMonthBelowTheThreshold', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    // 'One bad month' is 9/3 = 75% over the year but 1/2 = 33% in June.
    cmp.minWinRateFilter.set(50);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['g-steady']);
  });

  it('gateFilters_IgnoreMonthsWithNothingToJudge', () => {
    const cmp = create([
      makeRow({
        strategyId: 'g-breakeven',
        name: 'Breakeven month',
        returns: [
          makeReturn({ month: 6, returnPercent: 0.01, winCount: 4, lossCount: 1 }),
          // Nothing but breakeven trades: no win rate exists, so the gate must skip this month
          // instead of reading it as a zero.
          makeReturn({ month: 7, returnPercent: 0.01, winCount: 0, lossCount: 0 }),
        ],
      }),
    ]).componentInstance;

    cmp.minWinRateFilter.set(50);

    expect(cmp.filteredRows()).toHaveLength(1);
  });

  it('gateFilters_ExcludeStrategiesWithNoMonthsInTheSelectedYear', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.minReturnFilter.set(-100);

    // Nothing proves 'No data' clears the bar, and this list feeds a real portfolio.
    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['g-steady', 'g-onebadmonth']);
  });

  it('gateFilters_KeepBitingWhileAnotherMetricIsOnScreen', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.setMetric('maxDrawdown');
    cmp.minReturnFilter.set(0);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['g-steady']);
  });

  it('gateFilters_ComposeWithEachOther', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.minReturnFilter.set(0);
    cmp.minWinRateFilter.set(90);

    // 'Steady' clears the return gate but bottoms out at 60% win rate.
    expect(cmp.filteredRows()).toHaveLength(0);
  });

  it('setGateFilters_TreatBlankAndUnparseableInputAsNoFilter', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.setMinReturnFilter('');
    expect(cmp.minReturnFilter()).toBeNull();

    cmp.setMinReturnFilter('abc');
    expect(cmp.minReturnFilter()).toBeNull();

    // Zero and negatives are meaningful thresholds here, unlike Max DD.
    cmp.setMinReturnFilter('0');
    expect(cmp.minReturnFilter()).toBe(0);

    cmp.setMinReturnFilter('-1.5');
    expect(cmp.minReturnFilter()).toBe(-1.5);

    cmp.setMinWinRateFilter('55');
    expect(cmp.minWinRateFilter()).toBe(55);

    cmp.setMinWinRateFilter('');
    expect(cmp.minWinRateFilter()).toBeNull();
  });

  it('clearFilters_AlsoResetsTheGateFilters', () => {
    const cmp = create(GATE_ROWS).componentInstance;

    cmp.minReturnFilter.set(0);
    cmp.minWinRateFilter.set(50);
    expect(cmp.hasActiveFilters()).toBe(true);

    cmp.clearFilters();

    expect(cmp.minReturnFilter()).toBeNull();
    expect(cmp.minWinRateFilter()).toBeNull();
    expect(cmp.filteredRows()).toHaveLength(3);
  });

  // --- Trade counts: a year total, plus a per-month gate -------------------

  const TRADE_ROWS: StrategyMonthlyReturnsDto[] = [
    // 40 trades over the year, never fewer than 15 in a month.
    makeRow({
      strategyId: 't-busy',
      name: 'Busy',
      returns: [makeReturn({ month: 6, tradeCount: 25 }), makeReturn({ month: 7, tradeCount: 15 })],
    }),
    // Same 40 trades, but one month barely traded at all.
    makeRow({
      strategyId: 't-lumpy',
      name: 'Lumpy',
      returns: [makeReturn({ month: 6, tradeCount: 38 }), makeReturn({ month: 7, tradeCount: 2 })],
    }),
    makeRow({
      strategyId: 't-thin',
      name: 'Thin',
      returns: [makeReturn({ month: 6, tradeCount: 5 })],
    }),
  ];

  it('viewRows_CarryTheYearTradeTotalAndTheThinnestMonth', () => {
    const cmp = create(TRADE_ROWS).componentInstance;
    const [busy, lumpy, thin] = cmp.viewRows();

    expect(busy.totalTrades).toBe(40);
    expect(busy.worstMonthTrades).toBe(15);
    expect(lumpy.totalTrades).toBe(40);
    expect(lumpy.worstMonthTrades).toBe(2);
    expect(thin.totalTrades).toBe(5);
  });

  it('minTotalTradesFilter_KeepsStrategiesWithAtLeastThatManyTradesInTheYear', () => {
    const cmp = create(TRADE_ROWS).componentInstance;

    // At least, not more than: 40 must clear a threshold of 40.
    cmp.minTotalTradesFilter.set(40);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['t-busy', 't-lumpy']);
  });

  it('minMonthlyTradesFilter_ExcludesAStrategyWithASingleThinMonth', () => {
    const cmp = create(TRADE_ROWS).componentInstance;

    // 'Lumpy' has the same 40 trades as 'Busy', but July only saw 2.
    cmp.minMonthlyTradesFilter.set(10);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['t-busy']);
  });

  it('tradeFilters_ExcludeStrategiesWithNoMonthsInTheSelectedYear', () => {
    const cmp = create([
      ...TRADE_ROWS,
      makeRow({ strategyId: 't-none', returns: [] }),
    ]).componentInstance;

    cmp.minTotalTradesFilter.set(1);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['t-busy', 't-lumpy', 't-thin']);
  });

  it('tradeFilters_KeepBitingWhileAnotherMetricIsOnScreen', () => {
    const cmp = create(TRADE_ROWS).componentInstance;

    cmp.setMetric('winRate');
    cmp.minMonthlyTradesFilter.set(10);

    expect(cmp.filteredRows().map((r) => r.strategyId)).toEqual(['t-busy']);
  });

  it('setTradeFilters_TreatBlankUnparseableAndNonPositiveInputAsNoFilter', () => {
    const cmp = create(TRADE_ROWS).componentInstance;

    cmp.setMinTotalTradesFilter('');
    expect(cmp.minTotalTradesFilter()).toBeNull();

    cmp.setMinTotalTradesFilter('abc');
    expect(cmp.minTotalTradesFilter()).toBeNull();

    // "at least 0 trades" filters nothing, so it is not a filter.
    cmp.setMinTotalTradesFilter('0');
    expect(cmp.minTotalTradesFilter()).toBeNull();

    // A trade count is a whole number; a typed decimal rounds up to the next reachable one.
    cmp.setMinTotalTradesFilter('10.4');
    expect(cmp.minTotalTradesFilter()).toBe(11);

    cmp.setMinMonthlyTradesFilter('3');
    expect(cmp.minMonthlyTradesFilter()).toBe(3);
  });

  it('clearFilters_AlsoResetsTheTradeFilters', () => {
    const cmp = create(TRADE_ROWS).componentInstance;

    cmp.minTotalTradesFilter.set(20);
    cmp.minMonthlyTradesFilter.set(10);
    expect(cmp.hasActiveFilters()).toBe(true);

    cmp.clearFilters();

    expect(cmp.minTotalTradesFilter()).toBeNull();
    expect(cmp.minMonthlyTradesFilter()).toBeNull();
    expect(cmp.filteredRows()).toHaveLength(3);
  });

  // --- Summary row above the grid -----------------------------------------

  const SUMMARY_ROWS: StrategyMonthlyReturnsDto[] = [
    makeRow({
      strategyId: 'sum-a',
      name: 'Alpha',
      symbol: 'NQ',
      returns: [makeReturn({ month: 6, returnPercent: 0.01, winCount: 3, lossCount: 1 })],
    }),
    makeRow({
      strategyId: 'sum-b',
      name: 'Beta',
      symbol: 'XAU',
      returns: [makeReturn({ month: 6, returnPercent: 0.005, winCount: 1, lossCount: 3 })],
    }),
  ];

  it('summaryRow_SumsEachMonthColumnAcrossTheFilteredStrategies', () => {
    const cmp = create(SUMMARY_ROWS).componentInstance;
    const summary = cmp.summaryRow();

    expect(summary.count).toBe(2);
    expect(summary.months[5].text).toBe('1.50%');
    expect(summary.total.text).toBe('1.50%');
  });

  it('summaryRow_FollowsTheFilterSoItShowsTheBookBeingBuilt', () => {
    const cmp = create(SUMMARY_ROWS).componentInstance;

    cmp.symbolFilter.set('NQ');

    expect(cmp.summaryRow().count).toBe(1);
    expect(cmp.summaryRow().months[5].text).toBe('1.00%');
  });

  it('summaryRow_PoolsTheWinRateInsteadOfAddingPercentages', () => {
    const cmp = create(SUMMARY_ROWS).componentInstance;

    cmp.setMetric('winRate');

    // 75% and 25% pooled is 4/4 = 50%, not the 100% an addition would show.
    expect(cmp.summaryRow().months[5].text).toBe('4/4 (50%)');
    expect(cmp.summaryRow().total.text).toBe('4/4 (50%)');
  });

  it('summaryRow_RendersEmDashesForMonthsNobodyTraded', () => {
    const cmp = create(SUMMARY_ROWS).componentInstance;
    expect(cmp.summaryRow().months[0].text).toBe('—');
  });

  it('summaryRow_SurvivesAFilterThatMatchesNothing', () => {
    const cmp = create(SUMMARY_ROWS).componentInstance;

    cmp.nameFilter.set('nothing matches this');

    expect(cmp.summaryRow().count).toBe(0);
    expect(cmp.summaryRow().total.text).toBe('—');
  });

  it('summaryRow_ColoursEachCellBySign', () => {
    const cmp = create([
      makeRow({
        strategyId: 'tone-a',
        returns: [
          makeReturn({ month: 6, returnPercent: 0.02 }),
          makeReturn({ month: 7, returnPercent: -0.03 }),
        ],
      }),
    ]).componentInstance;

    const summary = cmp.summaryRow();
    expect(summary.months[5].tone).toBe('pos');
    expect(summary.months[6].tone).toBe('neg');
    // Months nobody traded stay uncoloured.
    expect(summary.months[0].tone).toBe('');
    // +2% then -3% compounds to a loss.
    expect(summary.total.tone).toBe('neg');
  });

  it('summaryRow_ColoursTheWinRateAroundFiftyPercentNotZero', () => {
    const cmp = create([
      makeRow({
        strategyId: 'tone-wl',
        returns: [
          makeReturn({ month: 6, winCount: 3, lossCount: 1 }),
          makeReturn({ month: 7, winCount: 1, lossCount: 3 }),
        ],
      }),
    ]).componentInstance;

    cmp.setMetric('winRate');
    const summary = cmp.summaryRow();

    expect(summary.months[5].tone).toBe('pos');
    expect(summary.months[6].tone).toBe('neg');
    // 4/8 is exactly a coin flip: neither.
    expect(summary.total.tone).toBe('');
  });

  it('summaryRow_NeverPaintsADrawdownGreen', () => {
    const cmp = create([
      makeRow({
        strategyId: 'tone-dd',
        returns: [makeReturn({ month: 6, maxDrawdownPercent: 0.04 })],
      }),
    ]).componentInstance;

    cmp.setMetric('maxDrawdown');

    expect(cmp.summaryRow().months[5].tone).toBe('neg');
    expect(cmp.summaryRow().total.tone).toBe('neg');
  });
});
