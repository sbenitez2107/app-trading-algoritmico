import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { PortfolioDetailComponent } from './portfolio-detail.component';
import {
  PortfolioService,
  PortfolioDto,
  PortfolioRiskDto,
  FundingService,
  GuardrailKind,
  AccountType,
} from '../../../core/services/portfolio.service';

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

  // ---- varBandLabel: band position vs [floor, target] (portfolio-monthly-var spec) ----

  type VarTarget = Parameters<PortfolioDetailComponent['varBandLabel']>[0];

  function makeVarTarget(overrides: Partial<VarTarget> = {}): VarTarget {
    return {
      targetVarPct: 0.065,
      varFloorPct: 0.0325,
      horizonDays: 30,
      insufficientHistory: false,
      observationDays: 120,
      overlappingWindows: 91,
      independentWindows: 4,
      monthlyVar95: 1500,
      monthlyVar95Percent: 0.05,
      impliedMultiplier: 1.3,
      ...overrides,
    };
  }

  it('varBandLabel_BelowFloor_WarnsEngineMayScaleUp', () => {
    const comp = create();
    // 0.02 < floor 0.0325 — strictly below.
    expect(comp.varBandLabel(makeVarTarget({ monthlyVar95Percent: 0.02 }))).toBe(
      'Por debajo del floor — el motor puede escalar la exposición al alza (no es más seguro)',
    );
  });

  it('varBandLabel_ExactlyAtFloor_IsWithinBand', () => {
    const comp = create();
    // Boundary: spec text does not say which side owns the floor value itself; the
    // implementation treats the floor endpoint as within-band (only a strict "<" is "below").
    expect(comp.varBandLabel(makeVarTarget({ monthlyVar95Percent: 0.0325 }))).toBe(
      'Dentro de la banda',
    );
  });

  it('varBandLabel_WithinBand_ReportsWithinBand', () => {
    const comp = create();
    expect(comp.varBandLabel(makeVarTarget({ monthlyVar95Percent: 0.05 }))).toBe(
      'Dentro de la banda',
    );
  });

  it('varBandLabel_ExactlyAtTarget_IsWithinBand', () => {
    const comp = create();
    // Boundary: same ambiguity as the floor — implementation treats the target endpoint as
    // within-band (only a strict ">" is "above").
    expect(comp.varBandLabel(makeVarTarget({ monthlyVar95Percent: 0.065 }))).toBe(
      'Dentro de la banda',
    );
  });

  it('varBandLabel_AboveTarget_ReportsAboveTarget', () => {
    const comp = create();
    // 0.09 > target 0.065 — strictly above.
    expect(comp.varBandLabel(makeVarTarget({ monthlyVar95Percent: 0.09 }))).toBe(
      'Por encima del target',
    );
  });

  it('varBandLabel_NoEstimate_ReturnsDash', () => {
    const comp = create();
    expect(
      comp.varBandLabel(
        makeVarTarget({ monthlyVar95Percent: undefined, insufficientHistory: true }),
      ),
    ).toBe('—');
    // Also dashes out if either bound is missing even when an estimate exists.
    expect(comp.varBandLabel(makeVarTarget({ varFloorPct: undefined }))).toBe('—');
    expect(comp.varBandLabel(makeVarTarget({ targetVarPct: undefined }))).toBe('—');
  });
});

/**
 * Risk tab rendering by GuardrailKind. Unlike the block above, THESE tests call
 * detectChanges() (so ngOnInit + the template actually render) because the assertions are about
 * rendered DOM. To keep it light, `risk`/`activeTab` are set directly on the component's signals
 * AFTER the initial detectChanges() (which only touches the lightweight 'overview' tab via mocked
 * synchronous observables) — this never triggers ag-grid or the correlation-matrix child.
 */
describe('PortfolioDetailComponent — Risk tab by GuardrailKind', () => {
  let serviceMock: Partial<PortfolioService>;

  const portfolioDto: PortfolioDto = {
    id: 'pf-1',
    name: 'Test Portfolio',
    broker: 'Darwinex',
    accountType: AccountType.Live,
    initialCapital: 50_000,
    baseCurrency: 'USD',
    memberCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    members: [{ strategyId: 's1', strategyName: 'A', weight: 1 }],
  };

  beforeEach(() => {
    serviceMock = {
      getById: vi.fn().mockReturnValue(of(portfolioDto)),
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

  function baseRisk(overrides: Partial<PortfolioRiskDto> = {}): PortfolioRiskDto {
    return {
      initialCapital: 50_000,
      method: 'Historical',
      windowDays: 250,
      observationDays: 120,
      var95: 1000,
      var95Percent: 0.02,
      var99: 1500,
      var99Percent: 0.03,
      worstDay: 800,
      bestDay: 600,
      maxDrawdownPercent: 0.05,
      byService: [],
      guardrails: [],
      ...overrides,
    };
  }

  function renderWithRisk(risk: PortfolioRiskDto) {
    const fixture = TestBed.createComponent(PortfolioDetailComponent);
    fixture.detectChanges(); // ngOnInit — lightweight 'overview' tab, all mocks synchronous.
    fixture.componentInstance.risk.set(risk);
    fixture.componentInstance.activeTab.set('risk');
    fixture.detectChanges();
    return fixture;
  }

  it('VarTargetGuardrail_SufficientHistory_ShowsNoBreachOrHeadroom', () => {
    const fixture = renderWithRisk(
      baseRisk({
        guardrails: [
          {
            service: 'Darwinex',
            fundingService: FundingService.DarwinexZero,
            kind: GuardrailKind.VarTarget,
            configured: true,
            verified: true,
            dailyLossLimitPct: null,
            maxLossLimitPct: null,
            profitTargetPct: null,
            drawdownModel: null,
            serviceVar95Percent: 0.02,
            dailyHeadroomPct: null,
            dailyBreached: false,
            varTarget: {
              targetVarPct: 0.065,
              varFloorPct: 0.0325,
              horizonDays: 30,
              insufficientHistory: false,
              observationDays: 120,
              overlappingWindows: 91,
              independentWindows: 4,
              monthlyVar95: 300,
              monthlyVar95Percent: 0.006,
              impliedMultiplier: 10.83,
            },
          },
        ],
      }),
    );

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('breach');
    expect(text).not.toContain('Headroom');
    expect(fixture.nativeElement.querySelector('.guard__bar')).toBeNull();
  });

  it('VarTargetGuardrail_SufficientHistory_ShowsCapitalBaseLabelAndDisclaimer', () => {
    const fixture = renderWithRisk(
      baseRisk({
        guardrails: [
          {
            service: 'Darwinex',
            fundingService: FundingService.DarwinexZero,
            kind: GuardrailKind.VarTarget,
            configured: true,
            verified: true,
            dailyLossLimitPct: null,
            maxLossLimitPct: null,
            profitTargetPct: null,
            drawdownModel: null,
            serviceVar95Percent: 0.02,
            dailyHeadroomPct: null,
            dailyBreached: false,
            varTarget: {
              targetVarPct: 0.065,
              varFloorPct: 0.0325,
              horizonDays: 30,
              insufficientHistory: false,
              observationDays: 120,
              overlappingWindows: 91,
              independentWindows: 4,
              monthlyVar95: 300,
              monthlyVar95Percent: 0.006,
              impliedMultiplier: 10.83,
            },
          },
        ],
      }),
    );

    const text = fixture.nativeElement.textContent as string;
    // Portfolio initial capital, explicit denominator label (es-AR currency formatting).
    expect(text).toContain('50.000,00');
    expect(text.toLowerCase()).toContain('proxy'); // approximation disclaimer, adjacent to the estimate
  });

  it('VarTargetGuardrail_BelowMinHistory_ShowsInsufficientHistoryStateNoNumbers', () => {
    const fixture = renderWithRisk(
      baseRisk({
        guardrails: [
          {
            service: 'Darwinex',
            fundingService: FundingService.DarwinexZero,
            kind: GuardrailKind.VarTarget,
            configured: true,
            verified: true,
            dailyLossLimitPct: null,
            maxLossLimitPct: null,
            profitTargetPct: null,
            drawdownModel: null,
            serviceVar95Percent: 0.02,
            dailyHeadroomPct: null,
            dailyBreached: false,
            varTarget: {
              targetVarPct: 0.065,
              varFloorPct: 0.0325,
              horizonDays: 30,
              insufficientHistory: true,
              observationDays: 30,
              overlappingWindows: 0,
              independentWindows: 0,
              monthlyVar95: undefined,
              monthlyVar95Percent: undefined,
              impliedMultiplier: undefined,
            },
          },
        ],
      }),
    );

    const text = fixture.nativeElement.textContent as string;
    expect(text.toLowerCase()).toContain('insuficiente');
    expect(fixture.nativeElement.querySelector('.guard__verdict')).toBeNull();
  });

  it('VarTargetGuardrail_SufficientHistory_ShowsImpliedMultiplierWithDLeverageContext', () => {
    const fixture = renderWithRisk(
      baseRisk({
        guardrails: [
          {
            service: 'Darwinex',
            fundingService: FundingService.DarwinexZero,
            kind: GuardrailKind.VarTarget,
            configured: true,
            verified: true,
            dailyLossLimitPct: null,
            maxLossLimitPct: null,
            profitTargetPct: null,
            drawdownModel: null,
            serviceVar95Percent: 0.02,
            dailyHeadroomPct: null,
            dailyBreached: false,
            varTarget: {
              targetVarPct: 0.065,
              varFloorPct: 0.0325,
              horizonDays: 30,
              insufficientHistory: false,
              observationDays: 120,
              overlappingWindows: 91,
              independentWindows: 4,
              monthlyVar95: 300,
              monthlyVar95Percent: 0.006,
              impliedMultiplier: 10.83,
            },
          },
        ],
      }),
    );

    const text = fixture.nativeElement.textContent as string;
    // Multiplier value (num() -> toFixed(2), locale-independent) rendered with an 'x' suffix.
    expect(text).toContain('10.83x');
    // All three D-Leverage caps by position duration (KB-sourced, frontend display constants only).
    expect(text).toContain('16.25');
    expect(text).toContain('13x');
    expect(text).toContain('9.75x');
    // Explicit note: the app cannot resolve which cap applies (no position-duration tracking).
    expect(text.toLowerCase()).toContain('no puede resolver');
    expect(text.toLowerCase()).toContain('duración de posición');
  });
});
