import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  AccountDetailComponent,
  ALL_KPI_COLS,
  DEFAULT_VISIBLE_COLS,
} from './account-detail.component';
import { StrategyService, StrategyDto, PagedResult } from '../../../core/services/strategy.service';
import {
  TradingAccountService,
  TradingAccountDto,
} from '../../../core/services/trading-account.service';
import { GridPresetService, GridPresetDto } from '../../../core/services/grid-preset.service';
import { HttpErrorResponse } from '@angular/common/http';
import { StrategyCommentsModalComponent } from '../strategy-comments-modal/strategy-comments-modal.component';

function makeStrategy(id = '1'): StrategyDto {
  return {
    id,
    name: `Strategy ${id}`,
    pseudocode: null,
    entryIndicators: null,
    priceIndicators: null,
    indicatorParameters: null,
    symbol: null,
    timeframe: null,
    backtestFrom: null,
    backtestTo: null,
    totalProfit: 1000,
    profitInPips: null,
    yearlyAvgProfit: null,
    yearlyAvgReturn: null,
    cagr: null,
    numberOfTrades: 100,
    sharpeRatio: 1.5,
    profitFactor: 1.8,
    returnDrawdownRatio: null,
    winningPercentage: 60,
    drawdown: -500,
    drawdownPercent: null,
    dailyAvgProfit: null,
    monthlyAvgProfit: null,
    averageTrade: null,
    annualReturnMaxDdRatio: null,
    rExpectancy: null,
    rExpectancyScore: null,
    strQualityNumber: null,
    sqnScore: null,
    winsLossesRatio: null,
    payoutRatio: null,
    averageBarsInTrade: null,
    ahpr: null,
    zScore: null,
    zProbability: null,
    expectancy: null,
    deviation: null,
    exposure: null,
    stagnationInDays: null,
    stagnationPercent: null,
    numberOfWins: null,
    numberOfLosses: null,
    numberOfCancelled: null,
    grossProfit: null,
    grossLoss: null,
    averageWin: null,
    averageLoss: null,
    largestWin: null,
    largestLoss: null,
    maxConsecutiveWins: null,
    maxConsecutiveLosses: null,
    averageConsecutiveWins: null,
    averageConsecutiveLosses: null,
    averageBarsInWins: null,
    averageBarsInLosses: null,
    createdAt: new Date().toISOString(),
  };
}

function makePagedResult(items: StrategyDto[]): PagedResult<StrategyDto> {
  return { items, totalCount: items.length, page: 1, pageSize: 20 };
}

function makeTradingAccount(): TradingAccountDto {
  return {
    id: 'acc-1',
    name: 'Darwinex Demo',
    broker: 'Darwinex',
    accountType: 0,
    platform: 0,
    accountNumber: 12345,
    login: 12345,
    server: 'demo.darwinex.com',
    isEnabled: true,
    createdAt: new Date().toISOString(),
  };
}

describe('AccountDetailComponent', () => {
  let strategyServiceMock: Partial<StrategyService>;
  let tradingAccountServiceMock: Partial<TradingAccountService>;
  let gridPresetServiceMock: Partial<GridPresetService>;
  let routerMock: Partial<Router>;

  function makePreset(): GridPresetDto {
    return {
      id: 'preset-1',
      name: 'Performance',
      visibleColumns: ['totalProfit', 'sharpeRatio'],
      columnOrder: ['totalProfit', 'sharpeRatio'],
      createdAt: new Date().toISOString(),
    };
  }

  beforeEach(() => {
    strategyServiceMock = {
      getByAccount: vi.fn(),
      delete: vi.fn(),
      getComments: vi.fn().mockReturnValue(of([])),
      addComment: vi.fn(),
    };

    tradingAccountServiceMock = {
      getById: vi.fn().mockReturnValue(of(makeTradingAccount())),
    };

    gridPresetServiceMock = {
      getAll: vi.fn().mockReturnValue(of([])),
      create: vi.fn(),
      update: vi.fn(),
      delete: vi.fn(),
    };

    routerMock = {
      navigate: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AccountDetailComponent],
      providers: [
        { provide: StrategyService, useValue: strategyServiceMock },
        { provide: TradingAccountService, useValue: tradingAccountServiceMock },
        { provide: GridPresetService, useValue: gridPresetServiceMock },
        { provide: Router, useValue: routerMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { params: { accountId: 'acc-1' } },
          },
        },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(AccountDetailComponent);
    return fixture;
  }

  // --- Phase 8 spec R1: init loads strategies ---

  it('ngOnInit_LoadsStrategiesForAccount', () => {
    // Arrange
    const items = [makeStrategy('s1'), makeStrategy('s2')];
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult(items)),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    expect(strategyServiceMock.getByAccount).toHaveBeenCalledWith('acc-1', 1, 20);
    expect(fixture.componentInstance.strategies()).toEqual(items);
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });

  // --- Phase 8 spec R2: columnDefs from visibleColumns ---

  it('columnDefs_ContainsNameAndAllDefaultVisibleKpis', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    const fields = comp.columnDefs().map((c: { field?: string }) => c.field);

    // Assert — 'name' always present, plus all default visible columns
    expect(fields).toContain('name');
    for (const col of DEFAULT_VISIBLE_COLS) {
      expect(fields).toContain(col);
    }
  });

  // --- Phase 8 spec R3: toggleColumn ---

  it('toggleColumn_AddsColumnWhenNotVisible', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Find a col not in visibleColumns by default. All KPI cols are in columnDefs
    // at all times (with hide toggled), so assert on the hide flag not presence.
    const nonDefault = ALL_KPI_COLS.find((c) => !DEFAULT_VISIBLE_COLS.includes(c.field))!;
    const beforeDef = comp
      .columnDefs()
      .find((c: { field?: string }) => c.field === nonDefault.field) as { hide?: boolean };
    expect(beforeDef?.hide).toBe(true);

    // Act
    comp.toggleColumn(nonDefault.field);

    // Assert
    expect(comp.visibleColumns()).toContain(nonDefault.field);
    const afterDef = comp
      .columnDefs()
      .find((c: { field?: string }) => c.field === nonDefault.field) as { hide?: boolean };
    expect(afterDef?.hide).toBe(false);
  });

  it('toggleColumn_RemovesColumnWhenVisible', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // totalProfit is in DEFAULT_VISIBLE_COLS
    expect(comp.visibleColumns()).toContain('totalProfit');

    // Act
    comp.toggleColumn('totalProfit');

    // Assert
    expect(comp.visibleColumns()).not.toContain('totalProfit');
  });

  // --- Phase 8 spec R4: showModal ---

  it('openAddStrategyModal_SetsShowModalTrue', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    expect(comp.showModal()).toBe(false);

    // Act
    comp.openAddStrategyModal();

    // Assert
    expect(comp.showModal()).toBe(true);
  });

  // --- Phase 8 spec R5: onStrategyCreated refresh ---

  it('onStrategyCreated_PrependStrategyAndClosesModal', () => {
    // Arrange
    const initial = [makeStrategy('s1')];
    const newStrat = makeStrategy('s2');

    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult(initial)),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.openAddStrategyModal();
    expect(comp.showModal()).toBe(true);

    // Act
    comp.onStrategyCreated(newStrat);

    // Assert
    expect(comp.strategies()[0]).toEqual(newStrat);
    expect(comp.strategies()).toHaveLength(2);
    expect(comp.showModal()).toBe(false);
  });

  // --- Phase 8 spec R6: 404 error signal ---

  it('ngOnInit_On404Error_SetsErrorSignal', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Assert
    expect(comp.error()).not.toBeNull();
    expect(comp.isLoading()).toBe(false);
  });

  // --- #1 Account name in title ---

  it('ngOnInit_LoadsAccountAndSetsAccountSignal', () => {
    // Arrange
    const account = makeTradingAccount();
    (tradingAccountServiceMock.getById as ReturnType<typeof vi.fn>).mockReturnValue(of(account));
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Assert
    expect(tradingAccountServiceMock.getById).toHaveBeenCalledWith('acc-1');
    expect(comp.account()).toEqual(account);
  });

  it('ngOnInit_RendersAccountNameInTitle', () => {
    // Arrange
    const account = makeTradingAccount();
    (tradingAccountServiceMock.getById as ReturnType<typeof vi.fn>).mockReturnValue(of(account));
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert — title should contain account name
    const titleEl = fixture.nativeElement.querySelector('.account-detail__title');
    expect(titleEl?.textContent).toContain('Darwinex Demo');
  });

  // --- #2 Back button navigates to /darwinex/demo ---

  it('navigateBack_NavigatesToDarwinexDemo', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Act
    comp.navigateBack();

    // Assert
    expect(routerMock.navigate).toHaveBeenCalledWith(['/darwinex/demo']);
  });

  it('backButton_Click_CallsNavigateBack', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();

    // Act — click the back button in the DOM
    const backBtn = fixture.nativeElement.querySelector('.btn--ghost');
    backBtn?.click();

    // Assert
    expect(routerMock.navigate).toHaveBeenCalledWith(['/darwinex/demo']);
  });

  // --- #3 Delete confirm dialog ---

  it('requestDelete_SetsShowDeleteConfirmTrue', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Act
    comp.requestDelete('strategy-id-1');

    // Assert
    expect(comp.showDeleteConfirm()).toBe(true);
    expect(comp.pendingDeleteId()).toBe('strategy-id-1');
  });

  it('cancelDelete_ClosesDialogAndClearsPendingId', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.requestDelete('strategy-id-1');

    // Act
    comp.cancelDelete();

    // Assert
    expect(comp.showDeleteConfirm()).toBe(false);
    expect(comp.pendingDeleteId()).toBeNull();
  });

  it('confirmDelete_CallsDeleteAndRemovesRowFromStrategiesSignal', () => {
    // Arrange
    const strategy1 = makeStrategy('s1');
    const strategy2 = makeStrategy('s2');
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([strategy1, strategy2])),
    );
    (strategyServiceMock.delete as ReturnType<typeof vi.fn>).mockReturnValue(of(void 0));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.requestDelete('s1');

    // Act
    comp.confirmDelete();

    // Assert
    expect(strategyServiceMock.delete).toHaveBeenCalledWith('s1');
    expect(comp.strategies()).toHaveLength(1);
    expect(comp.strategies()[0].id).toBe('s2');
    expect(comp.showDeleteConfirm()).toBe(false);
  });

  // --- #4 Column presets ---

  it('ngOnInit_LoadsPresetsAndSetsPresetsSignal', () => {
    // Arrange
    const preset = makePreset();
    (gridPresetServiceMock.getAll as ReturnType<typeof vi.fn>).mockReturnValue(of([preset]));
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Assert
    expect(comp.presets()).toEqual([preset]);
  });

  it('applyPreset_UpdatesVisibleColumnsSignal', () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    const preset = makePreset(); // visibleColumns: ['totalProfit', 'sharpeRatio']

    // Act
    comp.applyPreset(preset);

    // Assert
    expect(comp.visibleColumns()).toEqual(['totalProfit', 'sharpeRatio']);
    expect(comp.showPresetsDropdown()).toBe(false);
  });

  it('savePreset_CallsGridPresetServiceCreate', () => {
    // Arrange
    const newPreset = makePreset();
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    (gridPresetServiceMock.create as ReturnType<typeof vi.fn>).mockReturnValue(of(newPreset));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Act
    comp.savePreset('Performance');

    // Assert
    expect(gridPresetServiceMock.create).toHaveBeenCalled();
    expect(comp.presets()).toContain(newPreset);
    expect(comp.showSavePresetModal()).toBe(false);
  });

  it('savePreset_CapturesOrderFromGridApi', () => {
    // Arrange
    const newPreset = makePreset();
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    (gridPresetServiceMock.create as ReturnType<typeof vi.fn>).mockReturnValue(of(newPreset));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Simulate the grid emitting ready with a fake API. Order reflects a drag
    // reorder: sharpeRatio moved before totalProfit. Fixed cols and one hidden col included.
    const fakeApi = {
      getColumnState: () => [
        { colId: 'name', hide: false }, // fixed — must be filtered
        { colId: 'symbol', hide: false }, // fixed
        { colId: 'timeframe', hide: false }, // fixed
        { colId: 'sharpeRatio', hide: false },
        { colId: 'totalProfit', hide: false },
        { colId: 'profitFactor', hide: true }, // hidden — excluded from visible but kept in order
        { colId: 'id', hide: false }, // Actions — fixed
      ],
      applyColumnState: vi.fn(),
    };
    comp.onGridReady({ api: fakeApi } as never);

    // Act
    comp.savePreset('My View');

    // Assert
    const createCall = (gridPresetServiceMock.create as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(createCall.name).toBe('My View');
    expect(createCall.columnOrder).toEqual(['sharpeRatio', 'totalProfit', 'profitFactor']);
    expect(createCall.visibleColumns).toEqual(['sharpeRatio', 'totalProfit']);
  });

  it('applyPreset_CallsApplyColumnStateWithFullOrderedState', async () => {
    // Arrange
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    const applyColumnState = vi.fn();
    // Grid has fixed cols + 2 user cols currently visible + 1 hidden KPI.
    const fakeApi = {
      getColumnState: () => [
        { colId: 'name', hide: false },
        { colId: 'symbol', hide: false },
        { colId: 'timeframe', hide: false },
        { colId: 'totalProfit', hide: false },
        { colId: 'drawdown', hide: true },
        { colId: 'id', hide: false },
      ],
      applyColumnState,
    };
    comp.onGridReady({ api: fakeApi } as never);

    const preset: GridPresetDto = {
      ...makePreset(),
      visibleColumns: ['sharpeRatio', 'totalProfit'],
      columnOrder: ['sharpeRatio', 'totalProfit', 'profitFactor'],
    };

    // Act
    comp.applyPreset(preset);
    // Flush the setTimeout(0) queued by applyPreset
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Assert: state must contain fixed cols (unchanged), then preset cols in preset order,
    // then any remaining non-preset KPI cols hidden at the end.
    expect(applyColumnState).toHaveBeenCalledTimes(1);
    const arg = applyColumnState.mock.calls[0][0];
    expect(arg.applyOrder).toBe(true);
    expect(arg.state).toEqual([
      // fixed cols preserved
      { colId: 'name', hide: false },
      { colId: 'symbol', hide: false },
      { colId: 'timeframe', hide: false },
      { colId: 'id', hide: false },
      // preset cols in preset order
      { colId: 'sharpeRatio', hide: false },
      { colId: 'totalProfit', hide: false },
      { colId: 'profitFactor', hide: true },
      // remaining KPI cols, hidden
      { colId: 'drawdown', hide: true },
    ]);
  });

  it('updatePreset_SendsCapturedStateToService_ReplacesInList', async () => {
    // Arrange
    const existingPreset = makePreset();
    const updated: GridPresetDto = {
      ...existingPreset,
      visibleColumns: ['sharpeRatio', 'drawdown'],
      columnOrder: ['drawdown', 'sharpeRatio'],
    };
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    (gridPresetServiceMock.getAll as ReturnType<typeof vi.fn>).mockReturnValue(
      of([existingPreset]),
    );
    (gridPresetServiceMock.update as ReturnType<typeof vi.fn>).mockReturnValue(of(updated));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Wire a fake grid api so updatePreset captures the reordered state
    const fakeApi = {
      getColumnState: () => [
        { colId: 'name', hide: false },
        { colId: 'drawdown', hide: false },
        { colId: 'sharpeRatio', hide: false },
        { colId: 'profitFactor', hide: true },
        { colId: 'id', hide: false },
      ],
      applyColumnState: vi.fn(),
    };
    comp.onGridReady({ api: fakeApi } as never);

    // Act
    comp.updatePreset(existingPreset);

    // Assert
    const updateCall = (gridPresetServiceMock.update as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(updateCall[0]).toBe(existingPreset.id);
    expect(updateCall[1]).toEqual({
      visibleColumns: ['drawdown', 'sharpeRatio'],
      columnOrder: ['drawdown', 'sharpeRatio', 'profitFactor'],
    });
    expect(comp.presets()).toEqual([updated]);
    expect(comp.showPresetsDropdown()).toBe(false);
  });

  it('updatePreset_SetsErrorOnFailure', () => {
    // Arrange
    const existingPreset = makePreset();
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    (gridPresetServiceMock.getAll as ReturnType<typeof vi.fn>).mockReturnValue(
      of([existingPreset]),
    );
    (gridPresetServiceMock.update as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Act
    comp.updatePreset(existingPreset);

    // Assert
    expect(comp.error()).toBeTruthy();
  });

  it('deletePreset_CallsGridPresetServiceDelete', () => {
    // Arrange
    const preset = makePreset();
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([])),
    );
    (gridPresetServiceMock.getAll as ReturnType<typeof vi.fn>).mockReturnValue(of([preset]));
    (gridPresetServiceMock.delete as ReturnType<typeof vi.fn>).mockReturnValue(of(void 0));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Act
    comp.deletePreset('preset-1');

    // Assert
    expect(gridPresetServiceMock.delete).toHaveBeenCalledWith('preset-1');
    expect(comp.presets()).toHaveLength(0);
  });

  // --- Comments modal ---

  it('openComments_SetsShowCommentsModalAndSelectedStrategy', () => {
    // Arrange
    const strategy = makeStrategy('s1');
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([strategy])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    expect(comp.showCommentsModal()).toBe(false);
    expect(comp.selectedStrategyForComments()).toBeNull();

    // Act
    comp.openComments(strategy);

    // Assert
    expect(comp.showCommentsModal()).toBe(true);
    expect(comp.selectedStrategyForComments()).toEqual(strategy);
  });

  it('closeCommentsModal_ResetsModalState', () => {
    // Arrange
    const strategy = makeStrategy('s1');
    (strategyServiceMock.getByAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makePagedResult([strategy])),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.openComments(strategy);

    // Act
    comp.closeCommentsModal();

    // Assert
    expect(comp.showCommentsModal()).toBe(false);
    expect(comp.selectedStrategyForComments()).toBeNull();
  });
});
