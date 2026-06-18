import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ColDef } from 'ag-grid-community';
import { PortfolioTradesGridComponent } from './portfolio-trades-grid.component';
import {
  PortfolioService,
  PortfolioTradeDto,
  PagedResult,
} from '../../../core/services/portfolio.service';

// Same trade columns as the strategy grid, PLUS the leading "Estrategia" column.
const EXPECTED_COLUMNS = [
  'Estrategia',
  'Ticket',
  'Open Time',
  'Close Time',
  'Type',
  'Size',
  'Item',
  'Open Price',
  'Close Price',
  'SL',
  'TP',
  'Commission',
  'Swap',
  'Profit',
  'Net Profit',
  'Close Reason',
  'Status',
];

function makeTrade(overrides: Partial<PortfolioTradeDto> = {}): PortfolioTradeDto {
  return {
    id: 'trade-1',
    strategyId: 'strat-1',
    strategyName: 'Alpha',
    ticket: 12345,
    openTime: '2026-01-15T10:00:00Z',
    closeTime: '2026-01-15T12:00:00Z',
    type: 'buy',
    size: 0.1,
    item: 'EURUSD',
    openPrice: 1.085,
    closePrice: 1.09,
    stopLoss: 1.08,
    takeProfit: 1.095,
    commission: -0.7,
    taxes: 0.0,
    swap: 0.0,
    profit: 50.0,
    closeReason: 'TP',
    isOpen: false,
    ...overrides,
  };
}

function makePagedResult(trades: PortfolioTradeDto[]): PagedResult<PortfolioTradeDto> {
  return {
    items: trades,
    totalCount: trades.length,
    page: 1,
    pageSize: 50,
  };
}

describe('PortfolioTradesGridComponent', () => {
  let portfolioServiceMock: Partial<PortfolioService>;

  beforeEach(() => {
    portfolioServiceMock = {
      getTradesByPortfolio: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortfolioTradesGridComponent],
      providers: [{ provide: PortfolioService, useValue: portfolioServiceMock }],
    });
  });

  function create(portfolioId = 'pf-1', initial: PortfolioTradeDto[] = []) {
    (portfolioServiceMock.getTradesByPortfolio as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult(initial)),
    );
    const fixture = TestBed.createComponent(PortfolioTradesGridComponent);
    fixture.componentRef.setInput('portfolioId', portfolioId);
    fixture.detectChanges();
    return fixture;
  }

  // Test 1: renders all 17 columns (16 trade columns + Estrategia)
  // Extended timeout: first ag-grid TestBed.createComponent in the full parallel suite
  // regularly exceeds the default 5s in jsdom.
  it('columnDefs_ContainsAll17ExpectedColumnsIncludingStrategy', { timeout: 15000 }, () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    const headerNames = comp.columnDefs.map((c) => c.headerName);

    // Assert
    for (const expected of EXPECTED_COLUMNS) {
      expect(headerNames).toContain(expected);
    }
    expect(headerNames.length).toBe(17);
    // Strategy column must be FIRST.
    expect((comp.columnDefs[0] as ColDef<PortfolioTradeDto>).field).toBe('strategyName');
  });

  // Test 2: loads trades from the service and exposes them.
  it('loadsTradesFromServiceOnInit', () => {
    const trades = [makeTrade({ id: 't1' }), makeTrade({ id: 't2', strategyName: 'Beta' })];
    const fixture = create('pf-1', trades);
    const comp = fixture.componentInstance;

    expect(portfolioServiceMock.getTradesByPortfolio).toHaveBeenCalledWith('pf-1', 'all', 1, 50);
    expect(comp.trades()).toEqual(trades);
    expect(comp.isLoading()).toBe(false);
  });

  // Test 2b: when the portfolio has more trades than the first server page,
  // the component refetches the FULL set so ag-grid can paginate over all of them.
  it('loadsAllTrades_WhenTotalCountExceedsFirstPage', () => {
    const firstPage = Array.from({ length: 50 }, (_, i) => makeTrade({ id: `t${i}` }));
    const fullSet = Array.from({ length: 148 }, (_, i) => makeTrade({ id: `t${i}` }));

    const mock = portfolioServiceMock.getTradesByPortfolio as ReturnType<typeof vi.fn>;
    mock.mockReturnValueOnce(of({ items: firstPage, totalCount: 148, page: 1, pageSize: 50 }));
    mock.mockReturnValueOnce(of({ items: fullSet, totalCount: 148, page: 1, pageSize: 148 }));

    const fixture = TestBed.createComponent(PortfolioTradesGridComponent);
    fixture.componentRef.setInput('portfolioId', 'pf-big');
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // First call = initial page (50); second call = full count (148).
    expect(mock).toHaveBeenNthCalledWith(1, 'pf-big', 'all', 1, 50);
    expect(mock).toHaveBeenNthCalledWith(2, 'pf-big', 'all', 1, 148);
    expect(comp.trades().length).toBe(148);
    expect(comp.isLoading()).toBe(false);
  });

  // Test 3: pinned TOTAL row sums money fields across all trades.
  it('pinnedBottomRowData_SumsMoneyFields', () => {
    const trades = [
      makeTrade({ commission: -1, swap: -2, taxes: -3, profit: 100 }),
      makeTrade({ commission: -4, swap: -5, taxes: -6, profit: 200 }),
    ];
    const fixture = create('pf-1', trades);
    const comp = fixture.componentInstance;

    const total = comp.pinnedBottomRowData();
    expect(total.length).toBe(1);
    expect(total[0].commission).toBe(-5);
    expect(total[0].swap).toBe(-7);
    expect(total[0].taxes).toBe(-9);
    expect(total[0].profit).toBe(300);
  });

  // Test 4: getRowStyle tints rows by open/profit/loss.
  it('getRowStyle_TintsByTradeState', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    const openTrade = makeTrade({ isOpen: true });
    const winTrade = makeTrade({ isOpen: false, profit: 50, commission: -1, swap: 0, taxes: 0 });
    const lossTrade = makeTrade({ isOpen: false, profit: -50, commission: -1, swap: 0, taxes: 0 });
    const breakeven = makeTrade({ isOpen: false, profit: 1, commission: -1, swap: 0, taxes: 0 });

    type Params = Parameters<typeof comp.getRowStyle>[0];

    expect(comp.getRowStyle({ data: openTrade } as Params)?.['backgroundColor']).toContain(
      'rgba(137, 180, 250',
    );
    expect(comp.getRowStyle({ data: winTrade } as Params)?.['backgroundColor']).toContain(
      'rgba(34, 197, 94',
    );
    expect(comp.getRowStyle({ data: lossTrade } as Params)?.['backgroundColor']).toContain(
      'rgba(239, 68, 68',
    );
    expect(comp.getRowStyle({ data: breakeven } as Params)).toBeUndefined();
  });

  // Test 5: status='closed' filter causes service call with 'closed'.
  it('setStatus_Closed_CallsServiceWithClosedFilter', () => {
    const fixture = create('pf-99');
    const comp = fixture.componentInstance;

    (portfolioServiceMock.getTradesByPortfolio as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    comp.setStatus('closed');

    expect(portfolioServiceMock.getTradesByPortfolio).toHaveBeenCalledWith(
      'pf-99',
      'closed',
      1,
      50,
    );
  });

  // portfolioId input change must refetch.
  it('portfolioIdChange_RefetchesTradesForNewPortfolio', () => {
    const fixture = create('pf-A');

    (portfolioServiceMock.getTradesByPortfolio as ReturnType<typeof vi.fn>).mockClear();
    (portfolioServiceMock.getTradesByPortfolio as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    fixture.componentRef.setInput('portfolioId', 'pf-B');
    fixture.detectChanges();

    expect(portfolioServiceMock.getTradesByPortfolio).toHaveBeenCalledWith('pf-B', 'all', 1, 50);
  });

  // Test 6: loading overlay visible during fetch.
  it('isLoading_TrueBeforeServiceResponds', () => {
    TestBed.resetTestingModule();

    const pending = new Subject<PagedResult<PortfolioTradeDto>>();
    const delayedMock: Partial<PortfolioService> = {
      getTradesByPortfolio: vi.fn().mockReturnValue(pending.asObservable()),
    };

    TestBed.configureTestingModule({
      imports: [PortfolioTradesGridComponent],
      providers: [{ provide: PortfolioService, useValue: delayedMock }],
    });

    const fixture = TestBed.createComponent(PortfolioTradesGridComponent);
    fixture.componentRef.setInput('portfolioId', 'pf-1');
    fixture.detectChanges();

    expect(fixture.componentInstance.isLoading()).toBe(true);
  });
});
