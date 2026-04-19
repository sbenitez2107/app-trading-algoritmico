import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../app.config';
import { GridPresetService, CreateGridPresetDto, GridPresetDto } from './grid-preset.service';

function makePreset(): GridPresetDto {
  return {
    id: 'preset-1',
    name: 'Performance',
    visibleColumns: ['totalProfit', 'sharpeRatio'],
    columnOrder: ['totalProfit', 'sharpeRatio'],
    createdAt: new Date().toISOString(),
  };
}

describe('GridPresetService', () => {
  let service: GridPresetService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        GridPresetService,
      ],
    });

    service = TestBed.inject(GridPresetService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('getAll_SendsGetRequestToCorrectUrl', () => {
    // Act
    service.getAll().subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/users/me/grid-presets');
    expect(req.request.method).toBe('GET');
    req.flush([makePreset()]);
  });

  it('create_SendsPostRequestWithDto', () => {
    // Arrange
    const dto: CreateGridPresetDto = {
      name: 'Performance',
      visibleColumns: ['totalProfit'],
      columnOrder: ['totalProfit'],
    };

    // Act
    service.create(dto).subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/users/me/grid-presets');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(dto);
    req.flush(makePreset());
  });

  it('delete_SendsDeleteRequestToCorrectUrl', () => {
    // Act
    service.delete('preset-123').subscribe();

    // Assert
    const req = httpTesting.expectOne('http://localhost:5001/api/users/me/grid-presets/preset-123');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
