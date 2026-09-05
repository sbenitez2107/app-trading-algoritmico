import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import {
  BacktestService,
  BacktestImportOutcome,
  BacktestImportResultDto,
  BacktestRunKind,
  BacktestSegment,
  GroupRiskAnalysisDto,
  GroupRiskAnalysisStatus,
  GroupRiskMemberStatus,
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

  // ---------------------------------------------------------------------------
  // getGroupRisk — the refusal-unwrapping catchError.
  //
  // The server answers 400/404/422 with the SAME GroupRiskAnalysisDto it answers 200 with, because
  // the per-member evidence is what the operator acts on. Unwrapping that body is the only thing
  // standing between a refusal and the generic REQUEST_ERROR branch, and it is only reachable
  // through a real HTTP error response — which is why it has to be tested HERE. The panel's spec
  // mocks this service with `of(analysis)`, so no refusal traverses HTTP in it at all.
  // ---------------------------------------------------------------------------

  const GROUP_RISK_URL = 'http://localhost:5001/api/backtests/portfolio-risk';

  function refusal(status: GroupRiskAnalysisStatus, detail: string): GroupRiskAnalysisDto {
    return {
      status,
      segment: BacktestSegment.InSampleTest,
      members: [
        {
          strategyId: STRATEGY_ID,
          label: 'Alpha',
          status: GroupRiskMemberStatus.NonUnitWeight,
          segment: BacktestSegment.InSampleTest,
          runKind: BacktestRunKind.Deploy,
          runId: 'run-1',
          detail,
        },
      ],
      risk: null,
      correlation: null,
      refusal: detail,
    };
  }

  const query = {
    strategyIds: [STRATEGY_ID],
    initialCapital: 10_000,
    targetRiskPerTrade: 199.98,
    segment: BacktestSegment.InSampleTest,
  };

  it.each([
    ['a 400 request refusal', 400, GroupRiskAnalysisStatus.InvalidInitialCapital],
    ['a 404 missing strategy', 404, GroupRiskAnalysisStatus.StrategyNotFound],
    ['a 422 data refusal', 422, GroupRiskAnalysisStatus.NonUnitWeight],
  ])(
    'getGroupRisk_%s_EmitsTheEvidenceBodyInsteadOfAGenericError',
    (_name, httpStatus, status) => {
      let received: GroupRiskAnalysisDto | undefined;
      let erroredWith: unknown;
      service
        .getGroupRisk(query)
        .subscribe({ next: (r) => (received = r), error: (e: unknown) => (erroredWith = e) });

      httpTesting
        .expectOne((r) => r.url === GROUP_RISK_URL)
        .flush(refusal(status, 'Alpha carries weight 1.5'), {
          status: httpStatus,
          statusText: 'Refused',
        });

      expect(erroredWith).toBeUndefined();
      expect(received?.status).toBe(status);
      expect(received?.members[0].label).toBe('Alpha');
      expect(received?.refusal).toContain('1.5');
    },
  );

  it('getGroupRisk_TransportFailureWithNoAnalysisBody_IsAStableI18nError', () => {
    let nextCalled = false;
    let capturedError: unknown;
    service.getGroupRisk(query).subscribe({
      next: () => (nextCalled = true),
      error: (e: unknown) => (capturedError = e),
    });

    httpTesting
      .expectOne((r) => r.url === GROUP_RISK_URL)
      .flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });

    expect(nextCalled).toBe(false);
    expect((capturedError as Error).message).toBe('SQX.BACKTESTS.GROUP_RISK.REQUEST_ERROR');
  });

  it('getGroupRisk_OmittedSegment_IsNotSentAsZero', () => {
    // `segment=0` is a request for Unknown, which the server refuses for its OWN reason. Not
    // choosing one is a different request, and the difference has to survive the query string.
    service.getGroupRisk({ ...query, segment: undefined }).subscribe();

    const req = httpTesting.expectOne((r) => r.url === GROUP_RISK_URL);
    expect(req.request.params.has('segment')).toBe(false);
    expect(req.request.params.getAll('strategyIds')).toEqual([STRATEGY_ID]);
    req.flush(refusal(GroupRiskAnalysisStatus.SegmentNotSpecified, 'none chosen'), {
      status: 400,
      statusText: 'Bad Request',
    });
  });
});
