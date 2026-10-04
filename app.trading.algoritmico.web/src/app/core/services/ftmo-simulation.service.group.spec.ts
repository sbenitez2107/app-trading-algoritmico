import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import { FtmoGroupCandidatesDto } from '../models/ftmo-group-simulation.model';
import { FtmoRequestError, FtmoSimulationService } from './ftmo-simulation.service';

const ACCOUNT_ID = '655ef82d-20cc-4108-a1f5-a782587fca36';

describe('FtmoSimulationService.getGroupCandidates', () => {
  let service: FtmoSimulationService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        FtmoSimulationService,
      ],
    });
    service = TestBed.inject(FtmoSimulationService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('issuesAGetToTheCandidatesRouteWithTheAccountIdAsAQueryParam', () => {
    let received: FtmoGroupCandidatesDto | undefined;
    service.getGroupCandidates(ACCOUNT_ID).subscribe((r) => (received = r));

    const req = httpTesting.expectOne(
      (r) => r.url === 'http://localhost:5001/api/ftmo-simulations/candidates',
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('tradingAccountId')).toBe(ACCOUNT_ID);
    const body: FtmoGroupCandidatesDto = {
      tradingAccountId: ACCOUNT_ID,
      maxMembers: 4,
      candidates: [],
    };
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('aBadRequest_MapsToTheInvalidQueryErrorCarryingTheServerMessage', () => {
    let error: FtmoRequestError | undefined;
    service
      .getGroupCandidates(ACCOUNT_ID)
      .subscribe({ error: (e: FtmoRequestError) => (error = e) });

    httpTesting
      .expectOne((r) => r.url.endsWith('/api/ftmo-simulations/candidates'))
      .flush(
        { message: "The 'tradingAccountId' query parameter is required." },
        { status: 400, statusText: 'Bad Request' },
      );

    expect(error).toEqual({
      key: 'ERRORS.INVALID_QUERY',
      detail: "The 'tradingAccountId' query parameter is required.",
    });
  });

  it('aServerError_MapsToTheGenericRequestFailedErrorWithNoDetail', () => {
    let error: FtmoRequestError | undefined;
    service
      .getGroupCandidates(ACCOUNT_ID)
      .subscribe({ error: (e: FtmoRequestError) => (error = e) });

    httpTesting
      .expectOne((r) => r.url.endsWith('/api/ftmo-simulations/candidates'))
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(error).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
  });
});
