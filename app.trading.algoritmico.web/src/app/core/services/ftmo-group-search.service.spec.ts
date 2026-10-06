import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../app.config';
import {
  FtmoGroupSearchJobDto,
  FtmoGroupSearchRequest,
  FtmoGroupSearchStage,
  FtmoGroupSearchStatus,
  FtmoGroupSearchStopReason,
} from '../models/ftmo-group-search.model';
import {
  FtmoGroupSearchRequestError,
  FtmoGroupSearchService,
  FtmoGroupSearchStartOutcome,
} from './ftmo-group-search.service';

const BASE = 'http://localhost:5001/api/ftmo-simulations/group-search';
const JOB_ID = '655ef82d-20cc-4108-a1f5-a782587fca36';

function job(status = FtmoGroupSearchStatus.Running): FtmoGroupSearchJobDto {
  return {
    jobId: JOB_ID,
    status,
    progress: {
      stage: FtmoGroupSearchStage.Loading,
      processed: 0,
      total: 0,
      elapsedMs: 0,
      fullSimulationsDone: 0,
      maxFullSimulations: 150,
      funnel: {
        examined: 0,
        removedByCap: 0,
        removedByPairConflict: 0,
        removedByOnePercentRule: 0,
        removedNoCommonWindow: 0,
        removedMemberHasNoTrades: 0,
        shortlisted: 0,
      },
    },
    stopReason: FtmoGroupSearchStopReason.None,
    notComputed: 0,
    ineligible: [],
    rows: [],
    disclosures: [],
    errorMessage: null,
    request: {
      tradingAccountId: 'a',
      minMembers: 2,
      maxMembers: 4,
      maxPerInstrument: 2,
      includeIdenticalDeployEval: true,
      onePercentRule: false,
      eliminationCeiling: 0.05,
      broker: 'FTMO',
      initialCapital: 10000,
      targetRiskPerTrade: 100,
      fxLow: null,
      fxHigh: null,
      sizeDecimals: 2,
      step: 0.01,
      minLot: 0.01,
      maxLots: 100,
      maxFullSimulations: null,
      maxWallClockSeconds: null,
    },
  };
}

function request(overrides: Partial<FtmoGroupSearchRequest> = {}): FtmoGroupSearchRequest {
  return {
    tradingAccountId: 'a',
    minMembers: 2,
    maxMembers: 4,
    maxPerInstrument: 1,
    includeIdenticalDeployEval: false,
    onePercentRule: false,
    eliminationCeiling: 0.05,
    broker: 'FTMO',
    initialCapital: 10000,
    targetRiskPerTrade: 25,
    fxLow: null,
    fxHigh: null,
    sizeDecimals: 0,
    step: 0.01,
    minLot: 0.01,
    maxLots: 10,
    maxFullSimulations: null,
    maxWallClockSeconds: null,
    ...overrides,
  };
}

describe('FtmoGroupSearchService', () => {
  let service: FtmoGroupSearchService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        FtmoGroupSearchService,
      ],
    });
    service = TestBed.inject(FtmoGroupSearchService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function capture<T>(obs: Observable<T>) {
    const out: { value?: T; error?: FtmoGroupSearchRequestError; done: boolean } = { done: false };
    obs.subscribe({
      next: (v) => (out.value = v),
      error: (e: FtmoGroupSearchRequestError) => (out.error = e),
      complete: () => (out.done = true),
    });
    return out;
  }

  describe('startGroupSearch', () => {
    it('postsTheBodyAsBuilt_KeepingZeroAndNull', () => {
      capture(service.startGroupSearch(request()));
      const req = http.expectOne(BASE);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.sizeDecimals).toBe(0);
      expect(req.request.body.fxLow).toBeNull();
      expect('fxHigh' in req.request.body).toBe(true);
      req.flush({ jobId: JOB_ID, job: job() }, { status: 202, statusText: 'Accepted' });
    });

    it('postsTheFxBandWhenGiven', () => {
      capture(service.startGroupSearch(request({ fxLow: 1.05, fxHigh: 1.15 })));
      const req = http.expectOne(BASE);
      expect(req.request.body.fxLow).toBe(1.05);
      expect(req.request.body.fxHigh).toBe(1.15);
      req.flush({ jobId: JOB_ID, job: job() }, { status: 202, statusText: 'Accepted' });
    });

    it('a202_MapsToStartedWithTheJob', () => {
      const out = capture(service.startGroupSearch(request()));
      http.expectOne(BASE).flush({ jobId: JOB_ID, job: job() }, { status: 202, statusText: 'A' });
      const value = out.value as FtmoGroupSearchStartOutcome;
      expect(value.outcome).toBe('started');
      if (value.outcome === 'started') {
        expect(value.jobId).toBe(JOB_ID);
        expect(value.job.status).toBe(FtmoGroupSearchStatus.Running);
      }
    });

    it('a409_MapsToTheRunningId_NotAnError', () => {
      const out = capture(service.startGroupSearch(request()));
      http.expectOne(BASE).flush({ runningJobId: JOB_ID }, { status: 409, statusText: 'Conflict' });
      expect(out.error).toBeUndefined();
      expect(out.value).toEqual({ outcome: 'alreadyRunning', runningJobId: JOB_ID });
    });

    it('a409WithoutARunningId_IsAGenericFailure', () => {
      const out = capture(service.startGroupSearch(request()));
      http.expectOne(BASE).flush({}, { status: 409, statusText: 'Conflict' });
      expect(out.error).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
    });

    it('a400_MapsToInvalidQueryWithTheServerMessage', () => {
      const out = capture(service.startGroupSearch(request()));
      http
        .expectOne(BASE)
        .flush({ message: 'minMembers must be at least 2.' }, { status: 400, statusText: 'Bad' });
      expect(out.error).toEqual({
        key: 'ERRORS.INVALID_QUERY',
        detail: 'minMembers must be at least 2.',
      });
    });

    it('a400WithoutAMessage_HasNullDetail', () => {
      const out = capture(service.startGroupSearch(request()));
      http.expectOne(BASE).flush(null, { status: 400, statusText: 'Bad' });
      expect(out.error).toEqual({ key: 'ERRORS.INVALID_QUERY', detail: null });
    });

    it('a500_IsAGenericFailure', () => {
      const out = capture(service.startGroupSearch(request()));
      http.expectOne(BASE).flush('boom', { status: 500, statusText: 'Err' });
      expect(out.error).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
    });
  });

  describe('getGroupSearch', () => {
    it('getsTheJobById', () => {
      const out = capture(service.getGroupSearch(JOB_ID));
      const req = http.expectOne(`${BASE}/${JOB_ID}`);
      expect(req.request.method).toBe('GET');
      req.flush(job(FtmoGroupSearchStatus.Completed));
      expect(out.value?.status).toBe(FtmoGroupSearchStatus.Completed);
    });

    it('a404_MapsToTheLostState', () => {
      const out = capture(service.getGroupSearch(JOB_ID));
      http
        .expectOne(`${BASE}/${JOB_ID}`)
        .flush({ message: 'lost' }, { status: 404, statusText: 'Not Found' });
      expect(out.error).toEqual({ key: 'SIMULATOR.FTMO_SEARCH.ERRORS.LOST', detail: null });
    });

    it('a500_IsAGenericFailure', () => {
      const out = capture(service.getGroupSearch(JOB_ID));
      http.expectOne(`${BASE}/${JOB_ID}`).flush('x', { status: 500, statusText: 'E' });
      expect(out.error?.key).toBe('ERRORS.REQUEST_FAILED');
    });
  });

  describe('getCurrentGroupSearch', () => {
    it('a200_ReturnsTheRetainedJob', () => {
      const out = capture(service.getCurrentGroupSearch());
      const req = http.expectOne(`${BASE}/current`);
      expect(req.request.method).toBe('GET');
      req.flush(job());
      expect(out.value?.jobId).toBe(JOB_ID);
    });

    it('a204_MapsToNull', () => {
      const out = capture(service.getCurrentGroupSearch());
      http.expectOne(`${BASE}/current`).flush(null, { status: 204, statusText: 'No Content' });
      expect(out.error).toBeUndefined();
      expect(out.done).toBe(true);
      expect(out.value).toBeNull();
    });
  });

  describe('cancelGroupSearch', () => {
    it('sendsDeleteAndCompletesOn204', () => {
      const out = capture(service.cancelGroupSearch(JOB_ID));
      const req = http.expectOne(`${BASE}/${JOB_ID}`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(out.done).toBe(true);
      expect(out.error).toBeUndefined();
    });

    it('a404_MapsToTheLostState', () => {
      const out = capture(service.cancelGroupSearch(JOB_ID));
      http
        .expectOne(`${BASE}/${JOB_ID}`)
        .flush({ message: 'lost' }, { status: 404, statusText: 'N' });
      expect(out.error?.key).toBe('SIMULATOR.FTMO_SEARCH.ERRORS.LOST');
    });
  });
});
