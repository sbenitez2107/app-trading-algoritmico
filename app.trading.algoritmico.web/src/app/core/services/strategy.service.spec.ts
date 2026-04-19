import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import { StrategyService } from './strategy.service';

describe('StrategyService — account methods', () => {
  let service: StrategyService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        StrategyService,
      ],
    });

    service = TestBed.inject(StrategyService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getByAccount_BuildsCorrectUrlWithPaginationParams', () => {
    // Act
    service.getByAccount('acc-1', 1, 20).subscribe();

    // Assert — spec R1: correct URL with pagination params
    const req = httpTesting.expectOne(
      'http://localhost:5001/api/trading-accounts/acc-1/strategies?page=1&pageSize=20',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('addToAccount_PostsFormDataWithNameAndBothFiles', () => {
    // Arrange
    const sqxFile = new File(['sqx content'], 'test.sqx', { type: 'application/octet-stream' });
    const htmlFile = new File(['<html/>'], 'test.html', { type: 'text/html' });

    // Act — spec R2: POST to correct URL with FormData
    service.addToAccount('acc-1', 'MyStrat', sqxFile, htmlFile).subscribe();

    // Assert
    const req = httpTesting.expectOne(
      'http://localhost:5001/api/trading-accounts/acc-1/strategies',
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);

    const formData = req.request.body as FormData;
    expect(formData.get('name')).toBe('MyStrat');
    expect(formData.get('sqxFile')).toBeTruthy();
    expect(formData.get('htmlFile')).toBeTruthy();

    req.flush({ id: '123', name: 'MyStrat', createdAt: new Date().toISOString() });
  });

  // --- #3 delete method ---

  it('delete_SendsDeleteRequestToCorrectUrl', () => {
    // Act
    service.delete('strategy-id-123').subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/strategies/strategy-id-123');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  // --- comments ---

  it('getComments_ConstructsCorrectUrl', () => {
    // Act
    service.getComments('strat-abc').subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/strategies/strat-abc/comments');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('addComment_PostsCorrectBody', () => {
    // Act
    service.addComment('strat-abc', 'Great performance in volatile markets').subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/strategies/strat-abc/comments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ content: 'Great performance in volatile markets' });
    req.flush({
      id: 'c-1',
      content: 'Great performance in volatile markets',
      createdAt: new Date().toISOString(),
      createdBy: null,
    });
  });
});
