import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import { PortfolioService, PortfolioTradeDto, PagedResult } from './portfolio.service';

function makePagedResult(): PagedResult<PortfolioTradeDto> {
  return {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 50,
  };
}

describe('PortfolioService', () => {
  let service: PortfolioService;
  let httpTesting: HttpTestingController;
  const apiBase = 'http://localhost:5001';

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: apiBase },
        PortfolioService,
      ],
    });

    service = TestBed.inject(PortfolioService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getTradesByPortfolio_SendsGetToTradesUrlWithDefaultParams', () => {
    // Act
    service.getTradesByPortfolio('pf-1').subscribe();

    // Assert
    const req = httpTesting.expectOne((r) => r.url === `${apiBase}/api/portfolios/pf-1/trades`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('all');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush(makePagedResult());
  });

  it('getTradesByPortfolio_ForwardsStatusPageAndPageSize', () => {
    // Act
    service.getTradesByPortfolio('pf-2', 'closed', 3, 25).subscribe();

    // Assert
    const req = httpTesting.expectOne((r) => r.url === `${apiBase}/api/portfolios/pf-2/trades`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('closed');
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush(makePagedResult());
  });
});
