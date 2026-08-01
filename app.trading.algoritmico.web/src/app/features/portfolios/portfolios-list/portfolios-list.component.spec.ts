import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { PortfoliosListComponent } from './portfolios-list.component';
import {
  PortfolioService,
  PortfolioSummaryDto,
  PortfolioMonthlyReturnsDto,
  MonthlyReturnDto,
  AccountType,
} from '../../../core/services/portfolio.service';

function makeSummary(overrides: Partial<PortfolioSummaryDto> = {}): PortfolioSummaryDto {
  return {
    id: 'pf-1',
    name: 'Alpha Portfolio',
    broker: 'FTMO',
    accountType: AccountType.Demo,
    initialCapital: 10000,
    baseCurrency: 'USD',
    memberCount: 5,
    createdAt: '2026-01-01T00:00:00Z',
    finalEquity: 10677,
    netProfit: 677,
    totalReturn: 0.0677,
    returnDrawdownRatio: 3.5,
    profitFactor: 1.42,
    sharpeRatio: 1.1,
    cagr: 0.12,
    maxDrawdownPercent: 0.0193,
    sqn: 2.1,
    exposure: 0.35,
    tradeCount: 120,
    winCount: 72,
    lossCount: 48,
    winRate: 0.6,
    monthlyAvgProfit: 56.4,
    dailyAvgProfit: 1.88,
    ...overrides,
  };
}

function makeMonth(overrides: Partial<MonthlyReturnDto> = {}): MonthlyReturnDto {
  return {
    year: 2026,
    month: 1,
    equityStart: 10000,
    equityEnd: 10100,
    profit: 100,
    returnPercent: 0.01,
    tradeCount: 4,
    ...overrides,
  };
}

function makeMonthly(
  overrides: Partial<PortfolioMonthlyReturnsDto> = {},
): PortfolioMonthlyReturnsDto {
  return {
    portfolioId: 'pf-1',
    name: 'Alpha Portfolio',
    memberCount: 5,
    returns: [makeMonth()],
    ...overrides,
  };
}

/** Minimal CellClickedEvent shape: the component only reads `data` and the column id. */
function cellEvent(data: PortfolioSummaryDto | undefined, colId: string) {
  return { data, column: { getColId: () => colId } } as Parameters<
    PortfoliosListComponent['onCellClicked']
  >[0];
}

describe('PortfoliosListComponent', () => {
  let portfolioServiceMock: Partial<PortfolioService>;
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let activatedRouteMock: { snapshot: { data: Record<string, string> } };

  beforeEach(() => {
    portfolioServiceMock = {
      getSummaries: vi.fn(),
      getMonthlyReturnsByBroker: vi.fn().mockReturnValue(of([])),
      delete: vi.fn().mockReturnValue(of(void 0)),
    };

    routerMock = { navigate: vi.fn() };

    activatedRouteMock = {
      snapshot: { data: { broker: 'FTMO', portfoliosBase: '/portfolios/ftmo' } },
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortfoliosListComponent],
      providers: [
        { provide: PortfolioService, useValue: portfolioServiceMock },
        { provide: Router, useValue: routerMock },
        { provide: ActivatedRoute, useValue: activatedRouteMock },
      ],
    });
  });

  function create(rows: PortfolioSummaryDto[] = [makeSummary()]) {
    (portfolioServiceMock.getSummaries as ReturnType<typeof vi.fn>).mockReturnValue(of(rows));
    const fixture = TestBed.createComponent(PortfoliosListComponent);
    fixture.detectChanges();
    return fixture;
  }

  // ── Column definitions ────────────────────────────────────────────────────

  it('columnDefs_ContainsExpectedColumns', { timeout: 15000 }, () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const headers = comp.columnDefs.map((c) => c.headerName ?? c.colId);

    const expected = [
      'Portfolio',
      'Estrategias',
      'Tipo',
      'Broker',
      'Capital',
      'Equity Final',
      'Net Profit',
      'Return',
      'CAGR',
      'R/DD',
      'PF',
      'Sharpe',
      'SQN',
      'Max DD',
      'Exposure',
      'Trades',
      'W / L',
      'Win Rate',
      'Avg/Mes',
      'Avg/Día',
      'Mensual',
      'Acciones',
    ];

    for (const col of expected) {
      expect(headers).toContain(col);
    }
    expect(comp.columnDefs.length).toBe(22);
  });

  it('columnDefs_FirstColumnIsPinnedLeft', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    expect(comp.columnDefs[0].pinned).toBe('left');
    expect(comp.columnDefs[0].field).toBe('name');
  });

  it('columnDefs_ActionColumnsArePinnedRight', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    const monthly = comp.columnDefs.find((c) => c.colId === 'monthly');
    const actions = comp.columnDefs.find((c) => c.colId === 'actions');

    expect(monthly?.pinned).toBe('right');
    expect(actions?.pinned).toBe('right');
  });

  // ── Data loading ──────────────────────────────────────────────────────────

  it('loadsDataFromServiceOnInit', () => {
    const rows = [makeSummary({ id: 'pf-1' }), makeSummary({ id: 'pf-2', name: 'Beta' })];
    const fixture = create(rows);
    const comp = fixture.componentInstance;

    expect(portfolioServiceMock.getSummaries).toHaveBeenCalledWith('FTMO');
    expect(comp.portfolios()).toEqual(rows);
    expect(comp.isLoading()).toBe(false);
    expect(comp.error()).toBeNull();
  });

  it('setsError_WhenServiceFails', () => {
    (portfolioServiceMock.getSummaries as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('network failure')),
    );

    const fixture = TestBed.createComponent(PortfoliosListComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    expect(comp.error()).toBe('Error al cargar los portfolios');
    expect(comp.isLoading()).toBe(false);
  });

  it('isLoading_TrueBeforeServiceResponds', () => {
    const pending = new Subject<PortfolioSummaryDto[]>();
    (portfolioServiceMock.getSummaries as ReturnType<typeof vi.fn>).mockReturnValue(
      pending.asObservable(),
    );

    const fixture = TestBed.createComponent(PortfoliosListComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.isLoading()).toBe(true);
  });

  // ── Monthly returns ───────────────────────────────────────────────────────

  it('loadsMonthlyReturns_ForTheBroker_OnInit', () => {
    (portfolioServiceMock.getMonthlyReturnsByBroker as ReturnType<typeof vi.fn>).mockReturnValue(
      of([makeMonthly()]),
    );

    const fixture = create();
    const comp = fixture.componentInstance;

    expect(portfolioServiceMock.getMonthlyReturnsByBroker).toHaveBeenCalledWith('FTMO');
    expect(comp.monthlyLoading()).toBe(false);
    expect(comp.monthlyError()).toBeNull();
    expect(comp.monthlyById().get('pf-1')).toEqual([makeMonth()]);
  });

  it('monthlyError_IsSetIndependently_WithoutBreakingTheGrid', () => {
    (portfolioServiceMock.getMonthlyReturnsByBroker as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('boom')),
    );

    const fixture = create();
    const comp = fixture.componentInstance;

    expect(comp.monthlyError()).toBe('Error al cargar el retorno mensual');
    expect(comp.monthlyLoading()).toBe(false);
    // The KPI grid still loaded fine — the two requests are independent.
    expect(comp.error()).toBeNull();
    expect(comp.portfolios()).toHaveLength(1);
  });

  it('toggleMonthlyView_SwitchesBetweenGridAndMatrix', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    expect(comp.viewMode()).toBe('grid');
    comp.toggleMonthlyView();
    expect(comp.viewMode()).toBe('monthly');
    comp.toggleMonthlyView();
    expect(comp.viewMode()).toBe('grid');
  });

  // ── Route data ────────────────────────────────────────────────────────────

  it('reads_BrokerAndPortfoliosBase_FromRouteData', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    expect(comp.broker).toBe('FTMO');
    // portfoliosBase is private — verify indirectly via openCreate navigation
    comp.openCreate();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/portfolios/ftmo', 'new']);
  });

  // ── Cell click navigation ─────────────────────────────────────────────────

  it('onCellClicked_NavigatesToDetailRoute', () => {
    const fixture = create([makeSummary({ id: 'pf-42' })]);
    const comp = fixture.componentInstance;

    comp.onCellClicked(cellEvent(makeSummary({ id: 'pf-42' }), 'name'));

    expect(routerMock.navigate).toHaveBeenCalledWith(['/portfolios/ftmo', 'pf-42']);
  });

  it('onCellClicked_DoesNotNavigate_WhenDataIsUndefined', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onCellClicked(cellEvent(undefined, 'name'));

    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it.each(['monthly', 'actions'])(
    'onCellClicked_DoesNotNavigate_FromThe%sActionColumn',
    (colId) => {
      const fixture = create();
      const comp = fixture.componentInstance;

      comp.onCellClicked(cellEvent(makeSummary({ id: 'pf-42' }), colId));

      expect(routerMock.navigate).not.toHaveBeenCalled();
    },
  );

  // ── openCreate ────────────────────────────────────────────────────────────

  it('openCreate_NavigatesToNewRoute', () => {
    const fixture = create();
    fixture.componentInstance.openCreate();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/portfolios/ftmo', 'new']);
  });

  // ── Delete ────────────────────────────────────────────────────────────────

  it('requestDelete_OpensConfirmation_WithoutCallingTheService', () => {
    const target = makeSummary({ id: 'pf-9', name: 'To Remove' });
    const fixture = create([target]);
    const comp = fixture.componentInstance;

    comp.requestDelete(target);

    expect(comp.pendingDelete()).toEqual(target);
    expect(portfolioServiceMock.delete).not.toHaveBeenCalled();
  });

  it('cancelDelete_ClosesConfirmation_WithoutDeleting', () => {
    const target = makeSummary({ id: 'pf-9' });
    const fixture = create([target]);
    const comp = fixture.componentInstance;

    comp.requestDelete(target);
    comp.cancelDelete();

    expect(comp.pendingDelete()).toBeNull();
    expect(portfolioServiceMock.delete).not.toHaveBeenCalled();
  });

  it('confirmDelete_RemovesRowFromGridAndMonthlyCache', () => {
    const keep = makeSummary({ id: 'pf-keep', name: 'Keep' });
    const drop = makeSummary({ id: 'pf-drop', name: 'Drop' });
    (portfolioServiceMock.getMonthlyReturnsByBroker as ReturnType<typeof vi.fn>).mockReturnValue(
      of([makeMonthly({ portfolioId: 'pf-keep' }), makeMonthly({ portfolioId: 'pf-drop' })]),
    );

    const fixture = create([keep, drop]);
    const comp = fixture.componentInstance;

    comp.requestDelete(drop);
    comp.confirmDelete();

    expect(portfolioServiceMock.delete).toHaveBeenCalledWith('pf-drop');
    expect(comp.portfolios().map((p) => p.id)).toEqual(['pf-keep']);
    expect(comp.monthlyById().has('pf-drop')).toBe(false);
    expect(comp.monthlyById().has('pf-keep')).toBe(true);
    expect(comp.pendingDelete()).toBeNull();
    expect(comp.isDeleting()).toBe(false);
  });

  it('confirmDelete_KeepsRow_AndSurfacesError_WhenServiceFails', () => {
    const target = makeSummary({ id: 'pf-9', name: 'Stubborn' });
    (portfolioServiceMock.delete as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('409')),
    );

    const fixture = create([target]);
    const comp = fixture.componentInstance;

    comp.requestDelete(target);
    comp.confirmDelete();

    expect(comp.portfolios().map((p) => p.id)).toEqual(['pf-9']);
    expect(comp.error()).toBe('No se pudo eliminar el portfolio "Stubborn"');
    expect(comp.pendingDelete()).toBeNull();
    expect(comp.isDeleting()).toBe(false);
  });

  it('confirmDelete_IsNoOp_WhenNothingIsPending', () => {
    const fixture = create();
    fixture.componentInstance.confirmDelete();
    expect(portfolioServiceMock.delete).not.toHaveBeenCalled();
  });

  // ── W/L valueGetter ──────────────────────────────────────────────────────

  it('wlColumn_RendersWinCountSlashLossCount', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    const wlCol = comp.columnDefs.find((c) => c.colId === 'wl');
    expect(wlCol).toBeDefined();

    const row = makeSummary({ winCount: 72, lossCount: 48 });
    // valueGetter receives a params-like object; we simulate the minimal shape
    const result = (wlCol!.valueGetter as (p: { data: PortfolioSummaryDto }) => string)({
      data: row,
    });
    expect(result).toBe('72 / 48');
  });
});
