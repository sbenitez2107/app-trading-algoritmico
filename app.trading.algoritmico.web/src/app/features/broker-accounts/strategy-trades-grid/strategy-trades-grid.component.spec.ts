import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { StrategyTradesGridComponent } from './strategy-trades-grid.component';
import {
  StrategyService,
  StrategyTradeDto,
  PagedResult,
} from '../../../core/services/strategy.service';

const EXPECTED_COLUMNS = [
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

function makeTrade(overrides: Partial<StrategyTradeDto> = {}): StrategyTradeDto {
  return {
    id: 'trade-1',
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

function makePagedResult(trades: StrategyTradeDto[]): PagedResult<StrategyTradeDto> {
  return {
    items: trades,
    totalCount: trades.length,
    page: 1,
    pageSize: 50,
  };
}

describe('StrategyTradesGridComponent', () => {
  let strategyServiceMock: Partial<StrategyService>;

  beforeEach(() => {
    strategyServiceMock = {
      getTradesByStrategy: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [StrategyTradesGridComponent],
      providers: [{ provide: StrategyService, useValue: strategyServiceMock }],
    });
  });

  function create(strategyId = 'strat-1') {
    (strategyServiceMock.getTradesByStrategy as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    const fixture = TestBed.createComponent(StrategyTradesGridComponent);
    fixture.componentRef.setInput('strategyId', strategyId);
    fixture.detectChanges();
    return fixture;
  }

  // Test 1: renders all 16 columns
  // Extended timeout: first ag-grid TestBed.createComponent in the full parallel suite
  // regularly exceeds the default 5s in jsdom. Subsequent creates in the same file are fast.
  it('columnDefs_ContainsAll16ExpectedColumns', { timeout: 15000 }, () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    const headerNames = comp.columnDefs.map((c) => c.headerName);

    // Assert
    for (const expected of EXPECTED_COLUMNS) {
      expect(headerNames).toContain(expected);
    }
    expect(headerNames.length).toBe(16);
  });

  // Test 2: getRowStyle tints rows by open/profit/loss.
  it('getRowStyle_TintsByTradeState', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    const openTrade = makeTrade({ isOpen: true });
    const winTrade = makeTrade({
      isOpen: false,
      profit: 50,
      commission: -1,
      swap: 0,
      taxes: 0,
    });
    const lossTrade = makeTrade({
      isOpen: false,
      profit: -50,
      commission: -1,
      swap: 0,
      taxes: 0,
    });
    const breakeven = makeTrade({
      isOpen: false,
      profit: 1,
      commission: -1,
      swap: 0,
      taxes: 0,
    });

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
    // Net = 0 → no tint
    expect(comp.getRowStyle({ data: breakeven } as Params)).toBeUndefined();
  });

  // Test 3: status='closed' filter causes service call with 'closed'
  it('setStatus_Closed_CallsServiceWithClosedFilter', () => {
    // Arrange
    const fixture = create('strat-99');
    const comp = fixture.componentInstance;

    (strategyServiceMock.getTradesByStrategy as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    // Act
    comp.setStatus('closed');

    // Assert
    expect(strategyServiceMock.getTradesByStrategy).toHaveBeenCalledWith(
      'strat-99',
      'closed',
      1,
      50,
    );
  });

  // strategyId input change must refetch — without this, switching active row in the
  // parent leaves the grid stuck on the previous strategy's trades.
  it('strategyIdChange_RefetchesTradesForNewStrategy', () => {
    const fixture = create('strat-A');

    (strategyServiceMock.getTradesByStrategy as ReturnType<typeof vi.fn>).mockClear();
    (strategyServiceMock.getTradesByStrategy as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    fixture.componentRef.setInput('strategyId', 'strat-B');
    fixture.detectChanges();

    expect(strategyServiceMock.getTradesByStrategy).toHaveBeenCalledWith('strat-B', 'all', 1, 50);
  });

  // Test 4: loading overlay visible during fetch
  it('isLoading_TrueBeforeServiceResponds', () => {
    // Arrange
    TestBed.resetTestingModule();

    // Use a Subject that never resolves to hold loading state
    const pending = new Subject<PagedResult<StrategyTradeDto>>();

    const delayedMock: Partial<StrategyService> = {
      getTradesByStrategy: vi.fn().mockReturnValue(pending.asObservable()),
    };

    TestBed.configureTestingModule({
      imports: [StrategyTradesGridComponent],
      providers: [{ provide: StrategyService, useValue: delayedMock }],
    });

    const fixture = TestBed.createComponent(StrategyTradesGridComponent);
    fixture.componentRef.setInput('strategyId', 'strat-1');
    fixture.detectChanges();

    const comp = fixture.componentInstance;

    // Assert — still loading because subject hasn't emitted
    expect(comp.isLoading()).toBe(true);
  });
});
