import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { BacktestsListComponent } from './backtests-list.component';
import {
  BacktestService,
  BacktestRunDto,
  BacktestRunKind,
  SymbolCalibrationDto,
  CalibrationStatus,
  PagedResult,
} from '../../../../core/services/backtest.service';

function makeRun(overrides: Partial<BacktestRunDto> = {}): BacktestRunDto {
  return {
    id: 'run-1',
    sourceFileName: 'ListOfTrades_XAUUSD_H1_IST.csv',
    symbol: 'XAUUSD_M1_UTC02',
    strategyId: 'strategy-1',
    strategyName: 'BTC_H1_Fractal_MACD',
    kind: BacktestRunKind.Deploy,
    tradeCount: 329,
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

function makeCalibration(overrides: Partial<SymbolCalibrationDto> = {}): SymbolCalibrationDto {
  return {
    symbol: 'XAUUSD_M1_UTC02',
    pointValue: 100,
    sampleCount: 90,
    minObserved: 100,
    maxObserved: 100,
    status: CalibrationStatus.Calibrated,
    calibratedAt: new Date().toISOString(),
    ...overrides,
  };
}

describe('BacktestsListComponent', () => {
  let backtestServiceMock: Partial<BacktestService>;

  beforeEach(() => {
    backtestServiceMock = {
      getRuns: vi
        .fn()
        .mockReturnValue(
          of({ items: [], totalCount: 0, page: 1, pageSize: 20 } as PagedResult<BacktestRunDto>),
        ),
      getCalibrations: vi.fn().mockReturnValue(of([])),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [BacktestsListComponent, TranslateModule.forRoot()],
      providers: [{ provide: BacktestService, useValue: backtestServiceMock }],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(BacktestsListComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('ngOnInit_LoadsRunsAndCalibrations', () => {
    create();

    expect(backtestServiceMock.getRuns).toHaveBeenCalledWith(1, 20);
    expect(backtestServiceMock.getCalibrations).toHaveBeenCalled();
  });

  it('runs_AreRenderedWithTheOwningStrategyAndTheRunKind', () => {
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      of({
        items: [
          makeRun(),
          makeRun({
            id: 'run-2',
            kind: BacktestRunKind.Evaluation,
            strategyName: 'Other Strategy',
          }),
        ],
        totalCount: 2,
        page: 1,
        pageSize: 20,
      }),
    );

    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(fixture.componentInstance.runs()).toHaveLength(2);
    expect(text).toContain('BTC_H1_Fractal_MACD');
    expect(text).toContain('Other Strategy');
    expect(text).toContain('329');
  });

  it('page_HasNoImportControl_ImportBelongsToTheStrategyRow', () => {
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      of({ items: [makeRun()], totalCount: 1, page: 1, pageSize: 20 }),
    );

    const fixture = create();
    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ) as HTMLButtonElement[];

    // Only the two pagination controls remain. Import moved to the account grid's row action, where
    // the strategy is known — an import button here would have nothing to attribute a file to.
    expect(buttons).toHaveLength(2);
    expect(buttons.map((b) => b.textContent?.trim())).toEqual([
      'SQX.BACKTESTS.PAGE_PREV',
      'SQX.BACKTESTS.PAGE_NEXT',
    ]);
  });

  it('calibrations_CalibratedSymbol_SurfacedInSignal', () => {
    const calibration = makeCalibration();
    (backtestServiceMock.getCalibrations as ReturnType<typeof vi.fn>).mockReturnValue(
      of([calibration]),
    );

    const fixture = create();

    expect(fixture.componentInstance.calibrations()).toEqual([calibration]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('90');
  });

  // --- A failed load must not read as "nothing imported yet" ---

  it('loadRuns_WhenTheRequestFails_ShowsAnErrorAndNotTheEmptyState', () => {
    // A 500, a dropped connection and an unapplied migration all leave `runs` empty. Rendering the
    // reassuring "No backtest runs imported yet." for them tells the user their data is gone.
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('500 Internal Server Error')),
    );

    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(fixture.componentInstance.runsError()).toBe('SQX.BACKTESTS.RUNS_ERROR');
    expect(text).toContain('SQX.BACKTESTS.RUNS_ERROR');
    expect(text).not.toContain('SQX.BACKTESTS.EMPTY_RUNS');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).not.toBeNull();
  });

  it('loadCalibrations_WhenTheRequestFails_ShowsAnErrorAndNotTheEmptyState', () => {
    // The two panels load independently, so they fail independently: a calibration outage must not
    // claim the runs list failed, and vice versa.
    (backtestServiceMock.getCalibrations as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('500 Internal Server Error')),
    );

    const fixture = create();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(fixture.componentInstance.calibrationsError()).toBe('SQX.BACKTESTS.CALIBRATIONS_ERROR');
    expect(text).toContain('SQX.BACKTESTS.CALIBRATIONS_ERROR');
    expect(text).not.toContain('SQX.BACKTESTS.EMPTY_CALIBRATIONS');
    expect(fixture.componentInstance.runsError()).toBeNull();
  });

  it('loadRuns_AfterAFailure_ClearsTheErrorOnceItSucceeds', () => {
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('boom')),
    );
    const fixture = create();
    expect(fixture.componentInstance.runsError()).toBe('SQX.BACKTESTS.RUNS_ERROR');

    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      of({ items: [makeRun()], totalCount: 1, page: 1, pageSize: 20 }),
    );
    fixture.componentInstance.loadRuns();
    fixture.detectChanges();

    expect(fixture.componentInstance.runsError()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('BTC_H1_Fractal_MACD');
  });

  it('goToPage_OutOfRange_DoesNotReload', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockClear();

    comp.goToPage(0);

    expect(backtestServiceMock.getRuns).not.toHaveBeenCalled();
  });

  it('goToPage_InRange_ReloadsThatPage', () => {
    (backtestServiceMock.getRuns as ReturnType<typeof vi.fn>).mockReturnValue(
      of({ items: [makeRun()], totalCount: 45, page: 1, pageSize: 20 }),
    );
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.goToPage(2);

    expect(comp.page()).toBe(2);
    expect(backtestServiceMock.getRuns).toHaveBeenLastCalledWith(2, 20);
  });
});
