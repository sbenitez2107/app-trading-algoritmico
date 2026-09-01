import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import {
  BacktestService,
  BacktestImportOutcome,
  BacktestImportResultDto,
  BacktestRunKind,
  StrategyBacktestsDto,
  WalkForwardImportResultDto,
} from './backtest.service';

function makeFile(name: string, content = 'x'): File {
  return new File([content], name, { type: 'text/csv' });
}

const STRATEGY_ID = '655ef82d-20cc-4108-a1f5-a782587fca36';

describe('BacktestService', () => {
  let service: BacktestService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        BacktestService,
      ],
    });

    service = TestBed.inject(BacktestService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('importDeploy_PostsOneFileToTheStrategysDeploySlot', () => {
    service.importDeploy(STRATEGY_ID, makeFile('deploy.csv')).subscribe();

    const req = httpTesting.expectOne(
      `http://localhost:5001/api/strategies/${STRATEGY_ID}/backtests/deploy`,
    );
    expect(req.request.method).toBe('POST');
    const carried = (req.request.body as FormData).getAll('file') as File[];
    expect(carried).toHaveLength(1);
    expect(carried[0].name).toBe('deploy.csv');

    req.flush({
      fileName: 'deploy.csv',
      outcome: BacktestImportOutcome.Imported,
      tradeCount: 329,
      rejectedRowCount: 0,
      reason: null,
    } as BacktestImportResultDto);
  });

  it('importEvaluation_PostsToTheEvaluationSlotNotTheDeploySlot', () => {
    service.importEvaluation(STRATEGY_ID, makeFile('evaluation.csv')).subscribe();

    const req = httpTesting.expectOne(
      `http://localhost:5001/api/strategies/${STRATEGY_ID}/backtests/evaluation`,
    );
    expect(req.request.method).toBe('POST');

    req.flush({
      fileName: 'evaluation.csv',
      outcome: BacktestImportOutcome.Imported,
      tradeCount: 329,
      rejectedRowCount: 0,
      reason: null,
    } as BacktestImportResultDto);
  });

  it('importWalkForward_PostsToTheWalkForwardEndpointAndSurfacesTheBoundary', () => {
    let received: WalkForwardImportResultDto | undefined;
    service.importWalkForward(STRATEGY_ID, makeFile('wf.csv')).subscribe((r) => (received = r));

    const req = httpTesting.expectOne(
      `http://localhost:5001/api/strategies/${STRATEGY_ID}/walk-forward`,
    );
    expect(req.request.method).toBe('POST');

    req.flush({
      fileName: 'wf.csv',
      outcome: BacktestImportOutcome.Imported,
      windowCount: 6,
      oosFromDate: '2025-05-26T00:00:00',
      reason: null,
    } as WalkForwardImportResultDto);

    expect(received?.windowCount).toBe(6);
    expect(received?.oosFromDate).toBe('2025-05-26T00:00:00');
  });

  it('importDeploy_RejectedFile_SurfacesTheServersReasonInsteadOfErroring', () => {
    // A rejection is a 200 with an outcome, not an HTTP failure: the server parsed the file and
    // has something specific to say about it, and that reason is what the user needs to see.
    let received: BacktestImportResultDto | undefined;
    service.importDeploy(STRATEGY_ID, makeFile('wrong.csv')).subscribe((r) => (received = r));

    httpTesting
      .expectOne(`http://localhost:5001/api/strategies/${STRATEGY_ID}/backtests/deploy`)
      .flush({
        fileName: 'wrong.csv',
        outcome: BacktestImportOutcome.Rejected,
        tradeCount: null,
        rejectedRowCount: null,
        reason: 'expected trade-list header, found a different column shape',
      } as BacktestImportResultDto);

    expect(received?.outcome).toBe(BacktestImportOutcome.Rejected);
    expect(received?.reason).toContain('different column shape');
  });

  it('importDeploy_HttpError_MapsToTranslatedMessageKey', () => {
    let capturedError: unknown;
    let nextCalled = false;
    service.importDeploy(STRATEGY_ID, makeFile('deploy.csv')).subscribe({
      next: () => (nextCalled = true),
      error: (err: unknown) => (capturedError = err),
    });

    httpTesting
      .expectOne(`http://localhost:5001/api/strategies/${STRATEGY_ID}/backtests/deploy`)
      .flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });

    expect(nextCalled).toBe(false);
    expect((capturedError as Error).message).toBe('SQX.BACKTESTS.IMPORT_ERROR');
  });

  it('getStrategyBacktests_ReturnsBothSlotsAndTheExport', () => {
    let received: StrategyBacktestsDto | undefined;
    service.getStrategyBacktests(STRATEGY_ID).subscribe((r) => (received = r));

    const req = httpTesting.expectOne(
      `http://localhost:5001/api/strategies/${STRATEGY_ID}/backtests`,
    );
    expect(req.request.method).toBe('GET');

    req.flush({
      strategyId: STRATEGY_ID,
      deploy: {
        id: 'run-1',
        sourceFileName: 'deploy.csv',
        symbol: 'XAUUSD_M1_UTC02',
        kind: BacktestRunKind.Deploy,
        tradeCount: 329,
        createdAt: new Date().toISOString(),
      },
      evaluation: null,
      walkForwardExport: null,
    } as StrategyBacktestsDto);

    expect(received?.deploy?.tradeCount).toBe(329);
    expect(received?.evaluation).toBeNull();
  });

  it('getRuns_SendsGetRequestWithPagingParams', () => {
    service.getRuns(2, 10).subscribe();

    const req = httpTesting.expectOne(
      (r) =>
        r.url === 'http://localhost:5001/api/backtests/runs' &&
        r.params.get('page') === '2' &&
        r.params.get('pageSize') === '10',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 10 });
  });

  it('getCalibrations_SendsGetRequestToCorrectUrl', () => {
    service.getCalibrations().subscribe();

    const req = httpTesting.expectOne('http://localhost:5001/api/backtests/calibrations');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
