import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import {
  FtmoGroupRefusal,
  FtmoGroupSimulationDto,
  FtmoGroupSimulationRequest,
} from '../models/ftmo-group-simulation.model';
import { FtmoSimulationStatus } from '../models/ftmo-simulation.model';
import { FtmoRequestError, FtmoSimulationService } from './ftmo-simulation.service';

const URL = 'http://localhost:5001/api/ftmo-simulations/group';

const BODY: FtmoGroupSimulationRequest = {
  memberStrategyIds: ['a', 'b'],
  broker: 'FTMO',
  initialCapital: 10000,
  targetRiskPerTrade: 25,
  fxLow: null,
  fxHigh: null,
  sizeDecimals: 0,
  step: 0.01,
  minLot: 0.01,
  maxLots: 10,
};

describe('FtmoSimulationService.simulateGroup', () => {
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

  afterEach(() => httpTesting.verify());

  it('postsTheBodyUnchanged_KeepingSizeDecimalsZeroAndNullFx', () => {
    let received: FtmoGroupSimulationDto | undefined;
    service.simulateGroup(BODY).subscribe((r) => (received = r));

    const req = httpTesting.expectOne(URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(BODY);
    expect(req.request.body.sizeDecimals).toBe(0);
    expect(req.request.body.fxLow).toBeNull();
    const dto: FtmoGroupSimulationDto = {
      status: FtmoSimulationStatus.Refused,
      refusal: FtmoGroupRefusal.InvalidRequest,
      sharedRefusal: null,
      dailyLossLimitPct: null,
      maxLossLimitPct: null,
      members: [],
      duplicateIdsRemoved: [],
      duplicateNameWarnings: [],
      unknownStrategyIds: [],
      kinds: [],
      disclosures: [],
    };
    req.flush(dto);
    expect(received).toEqual(dto);
  });

  it('aBadRequest_MapsToTheInvalidQueryErrorWithTheServerMessage', () => {
    let error: FtmoRequestError | undefined;
    service.simulateGroup(BODY).subscribe({ error: (e: FtmoRequestError) => (error = e) });
    httpTesting
      .expectOne(URL)
      .flush({ message: 'too many members' }, { status: 400, statusText: 'Bad Request' });
    expect(error).toEqual({ key: 'ERRORS.INVALID_QUERY', detail: 'too many members' });
  });

  it('aServerError_MapsToTheGenericRequestFailedError', () => {
    let error: FtmoRequestError | undefined;
    service.simulateGroup(BODY).subscribe({ error: (e: FtmoRequestError) => (error = e) });
    httpTesting.expectOne(URL).flush('boom', { status: 500, statusText: 'Server Error' });
    expect(error).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
  });
});
