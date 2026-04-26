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
    sl: 1.08,
    tp: 1.095,
    commission: -0.7,
    swap: 0.0,
    profit: 50.0,
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

  // Test 1: renders all 14 columns
  // Extended timeout: first ag-grid TestBed.createComponent in the full parallel suite
  // regularly exceeds the default 5s in jsdom. Subsequent creates in the same file are fast.
  it('columnDefs_ContainsAll14ExpectedColumns', { timeout: 15000 }, () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    const headerNames = comp.columnDefs.map((c) => c.headerName);

    // Assert
    for (const expected of EXPECTED_COLUMNS) {
      expect(headerNames).toContain(expected);
    }
    expect(headerNames.length).toBe(14);
  });

  // Test 2: open trade row gets CSS class 'trade--open'
  it('rowClassRules_OpenTrade_GetsTradeOpenClass', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    const openTrade = makeTrade({ isOpen: true });
    const closedTrade = makeTrade({ isOpen: false });

    // Act
    const openClass = comp.rowClassRules['trade--open'];
    const closedClass = comp.rowClassRules['trade--open'];

    // Assert — the rule should be true for open and false for closed
    expect(openClass({ data: openTrade } as unknown as Parameters<typeof openClass>[0])).toBe(true);
    expect(closedClass({ data: closedTrade } as unknown as Parameters<typeof closedClass>[0])).toBe(
      false,
    );
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
