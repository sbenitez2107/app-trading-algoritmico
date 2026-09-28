import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import { FtmoSimulationQuery } from '../models/ftmo-simulation.model';
import { FtmoSimulationService } from './ftmo-simulation.service';

const STRATEGY_ID = '655ef82d-20cc-4108-a1f5-a782587fca36';

const BASE_QUERY: FtmoSimulationQuery = {
  broker: 'FTMO',
  sqxSymbol: 'XAUUSD_M1_UTC02',
  initialCapital: 10000,
  targetRiskPerTrade: 0.01,
  sizeDecimals: 0,
  step: 0.01,
  minLot: 0.01,
  maxLots: 10,
};

describe('FtmoSimulationService', () => {
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

  it('getMultiStart_SendsEveryRequiredQueryParam_WithSizeDecimalsZeroIncluded', () => {
    // sizeDecimals=0 is a legal whole-lot grid declaration (hard rule 2). An `if (sizeDecimals)`
    // truthy check would silently drop it.
    service.getMultiStart(STRATEGY_ID, BASE_QUERY).subscribe();

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `http://localhost:5001/api/strategies/${STRATEGY_ID}/ftmo-breach/multi-start`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('broker')).toBe('FTMO');
    expect(req.request.params.get('sqxSymbol')).toBe('XAUUSD_M1_UTC02');
    expect(req.request.params.get('initialCapital')).toBe('10000');
    expect(req.request.params.get('targetRiskPerTrade')).toBe('0.01');
    expect(req.request.params.get('sizeDecimals')).toBe('0');
    expect(req.request.params.get('step')).toBe('0.01');
    expect(req.request.params.get('minLot')).toBe('0.01');
    expect(req.request.params.get('maxLots')).toBe('10');
    expect(req.request.params.has('fxLow')).toBe(false);
    expect(req.request.params.has('fxHigh')).toBe(false);

    req.flush({ strategyId: STRATEGY_ID, runs: [] });
  });

  it('getMultiStart_fxLowAndFxHighAreIncludedOnlyWhenNotNull', () => {
    service
      .getMultiStart(STRATEGY_ID, { ...BASE_QUERY, sizeDecimals: 2, fxLow: 1.05, fxHigh: 1.1 })
      .subscribe();

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `http://localhost:5001/api/strategies/${STRATEGY_ID}/ftmo-breach/multi-start`,
    );
    expect(req.request.params.get('fxLow')).toBe('1.05');
    expect(req.request.params.get('fxHigh')).toBe('1.1');

    req.flush({ strategyId: STRATEGY_ID, runs: [] });
  });

  it('getMultiStart_fxLowAndFxHighOfZeroAreSentAsZero_NeverDropped', () => {
    // design.md AD6: a present fx bound passes through verbatim, and the backend refuses a 0 band
    // with InvalidFxBand. A truthy `if (query.fxLow)` check would drop 0 and silently turn an invalid
    // band into "no band declared" (a different refusal, or none for a USD-settled symbol).
    service.getMultiStart(STRATEGY_ID, { ...BASE_QUERY, fxLow: 0, fxHigh: 0 }).subscribe();

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `http://localhost:5001/api/strategies/${STRATEGY_ID}/ftmo-breach/multi-start`,
    );
    expect(req.request.params.get('fxLow')).toBe('0');
    expect(req.request.params.get('fxHigh')).toBe('0');

    req.flush({ strategyId: STRATEGY_ID, runs: [] });
  });

  it('getMultiStart_a400ResponseMapsToInvalidQueryWithTheServerMessageAsDetail', () => {
    let captured: unknown;
    service.getMultiStart(STRATEGY_ID, BASE_QUERY).subscribe({
      error: (err: unknown) => (captured = err),
    });

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `http://localhost:5001/api/strategies/${STRATEGY_ID}/ftmo-breach/multi-start`,
    );
    req.flush(
      { message: 'The sqxSymbol matches no FTMO instrument spec.' },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(captured).toEqual({
      key: 'ERRORS.INVALID_QUERY',
      detail: 'The sqxSymbol matches no FTMO instrument spec.',
    });
  });

  it('getMultiStart_aNetworkFailureMapsToRequestFailedWithNullDetail', () => {
    let captured: unknown;
    service.getMultiStart(STRATEGY_ID, BASE_QUERY).subscribe({
      error: (err: unknown) => (captured = err),
    });

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `http://localhost:5001/api/strategies/${STRATEGY_ID}/ftmo-breach/multi-start`,
    );
    req.error(new ProgressEvent('network error'));

    expect(captured).toEqual({ key: 'ERRORS.REQUEST_FAILED', detail: null });
  });
});
