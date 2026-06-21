import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { PortfolioDetailComponent } from './portfolio-detail.component';
import { PortfolioService } from '../../../core/services/portfolio.service';

/**
 * These tests exercise pure presentation logic (the Live Win% label and the
 * composition row-click guard). The component is created WITHOUT detectChanges()
 * so ngOnInit never runs — no data is fetched and the heavy template (ag-grid +
 * child charts) is never rendered.
 */
describe('PortfolioDetailComponent', () => {
  let serviceMock: Partial<PortfolioService>;

  beforeEach(() => {
    serviceMock = {
      getById: vi.fn().mockReturnValue(of(null)),
      getAnalytics: vi.fn().mockReturnValue(of(null)),
      getEquityCurve: vi.fn().mockReturnValue(of([])),
      getMonthlyReturns: vi.fn().mockReturnValue(of([])),
      getCandidates: vi.fn().mockReturnValue(of([])),
      getRisk: vi.fn().mockReturnValue(of(null)),
      getCorrelation: vi.fn().mockReturnValue(of(null)),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortfolioDetailComponent],
      providers: [
        { provide: PortfolioService, useValue: serviceMock },
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'pf-1' }, data: {} } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    });
  });

  function create(): PortfolioDetailComponent {
    return TestBed.createComponent(PortfolioDetailComponent).componentInstance;
  }

  type Row = Parameters<PortfolioDetailComponent['liveWinRateLabel']>[0];

  function makeRow(overrides: Partial<NonNullable<Row>> = {}): Row {
    return {
      strategyId: 's1',
      strategyName: 'Alpha',
      weight: 1,
      normalizedWeight: 0.1,
      contributionPercent: 0.1,
      weightedNetProfit: 100,
      liveTradeCount: 5,
      liveWinRate: 0.4,
      ...overrides,
    } as NonNullable<Row>;
  }

  type ClickEvent = Parameters<PortfolioDetailComponent['onCompositionCellClicked']>[0];

  function makeClick(colId: string, rowPinned: string | null, row: Row): ClickEvent {
    return {
      node: { rowPinned },
      data: row,
      column: { getColId: () => colId },
    } as unknown as ClickEvent;
  }

  // ---- Win% (Live) label: "(wins/losses) XX.XX%" ----

  it('liveWinRateLabel_PrependsReconstructedWinsLosses', () => {
    const comp = create();
    // 0.40 * 5 = 2 wins, 3 losses (the screenshot's first row).
    expect(comp.liveWinRateLabel(makeRow({ liveTradeCount: 5, liveWinRate: 0.4 }))).toBe(
      '(2/3) 40.00%',
    );
    // 0.7619 * 21 = 16 wins, 5 losses.
    expect(comp.liveWinRateLabel(makeRow({ liveTradeCount: 21, liveWinRate: 0.7619 }))).toBe(
      '(16/5) 76.19%',
    );
    // 0.4444 * 9 = 4 wins, 5 losses.
    expect(comp.liveWinRateLabel(makeRow({ liveTradeCount: 9, liveWinRate: 0.4444 }))).toBe(
      '(4/5) 44.44%',
    );
  });

  it('liveWinRateLabel_FallsBackWhenDataMissingOrNoTrades', () => {
    const comp = create();
    expect(
      comp.liveWinRateLabel(makeRow({ liveWinRate: undefined, liveTradeCount: undefined })),
    ).toBe('—');
    // No trades → no counts to show, just the (dashed) percentage.
    expect(comp.liveWinRateLabel(makeRow({ liveTradeCount: 0, liveWinRate: 0 }))).toBe('0.00%');
    expect(comp.liveWinRateLabel(undefined)).toBe('—');
  });

  // ---- Row click opens the reused strategy-analytics modal ----

  it('onCompositionCellClicked_OpensModalForStrategyRow', () => {
    const comp = create();
    comp.onCompositionCellClicked(makeClick('liveNetProfit', null, makeRow()));
    expect(comp.analyticsTargetStrategy()).toEqual({ id: 's1', name: 'Alpha' });
  });

  it('onCompositionCellClicked_IgnoresEditableWeightCell', () => {
    const comp = create();
    comp.onCompositionCellClicked(makeClick('weight', null, makeRow()));
    expect(comp.analyticsTargetStrategy()).toBeNull();
  });

  it('onCompositionCellClicked_IgnoresActionsCell', () => {
    const comp = create();
    comp.onCompositionCellClicked(makeClick('actions', null, makeRow()));
    expect(comp.analyticsTargetStrategy()).toBeNull();
  });

  it('onCompositionCellClicked_IgnoresPinnedTotalRow', () => {
    const comp = create();
    comp.onCompositionCellClicked(makeClick('liveNetProfit', 'bottom', makeRow({ isTotal: true })));
    expect(comp.analyticsTargetStrategy()).toBeNull();
  });

  it('closeAnalytics_ClearsTarget', () => {
    const comp = create();
    comp.onCompositionCellClicked(makeClick('symbol', null, makeRow()));
    expect(comp.analyticsTargetStrategy()).not.toBeNull();
    comp.closeAnalytics();
    expect(comp.analyticsTargetStrategy()).toBeNull();
  });
});
