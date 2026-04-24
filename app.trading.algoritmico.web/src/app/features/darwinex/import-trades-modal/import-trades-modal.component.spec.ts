import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ImportTradesModalComponent } from './import-trades-modal.component';
import {
  TradingAccountService,
  TradeImportResultDto,
  OrphanMagicNumberDto,
} from '../../../core/services/trading-account.service';

function makeOrphan(magicNumber: number): OrphanMagicNumberDto {
  return {
    magicNumber,
    count: 3,
    firstSeen: '2026-01-01T00:00:00Z',
    lastSeen: '2026-01-31T00:00:00Z',
  };
}

function makeResult(overrides: Partial<TradeImportResultDto> = {}): TradeImportResultDto {
  return {
    imported: 10,
    updated: 2,
    skipped: 1,
    orphans: [],
    ...overrides,
  };
}

function makeFile(name: string, type = 'text/html'): File {
  return new File(['<html></html>'], name, { type });
}

describe('ImportTradesModalComponent', () => {
  let tradingAccountServiceMock: Partial<TradingAccountService>;

  beforeEach(() => {
    tradingAccountServiceMock = {
      importTrades: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ImportTradesModalComponent],
      providers: [{ provide: TradingAccountService, useValue: tradingAccountServiceMock }],
    });
  });

  function create(accountId = 'acc-1') {
    const fixture = TestBed.createComponent(ImportTradesModalComponent);
    fixture.componentRef.setInput('accountId', accountId);
    fixture.detectChanges();
    return fixture;
  }

  // Test 1: file picker accepts .htm / .html, rejects .txt
  it('onFileChange_HtmlFile_AcceptsFile', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html', 'text/html');

    // Act
    comp.onFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert
    expect(comp.selectedFile()).toBe(file);
    expect(comp.fileError()).toBeNull();
  });

  it('onFileChange_HtmFile_AcceptsFile', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.htm', 'text/html');

    // Act
    comp.onFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert
    expect(comp.selectedFile()).toBe(file);
    expect(comp.fileError()).toBeNull();
  });

  it('onFileChange_TxtFile_SetsFileError', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('trades.txt', 'text/plain');

    // Act
    comp.onFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert
    expect(comp.selectedFile()).toBeNull();
    expect(comp.fileError()).not.toBeNull();
  });

  // Test 2: submit calls TradingAccountService.importTrades
  it('submit_CallsImportTradesWithCorrectArgs', () => {
    // Arrange
    const fixture = create('acc-42');
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult()),
    );

    // Act
    comp.submit();

    // Assert
    expect(tradingAccountServiceMock.importTrades).toHaveBeenCalledWith('acc-42', file);
  });

  // Test 3: result panel shows imported/updated/skipped counts
  it('submit_OnSuccess_SetsResultSignal', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');
    const result = makeResult({ imported: 15, updated: 3, skipped: 0 });

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(result),
    );

    // Act
    comp.submit();

    // Assert
    expect(comp.result()).toEqual(result);
    expect(comp.result()!.imported).toBe(15);
    expect(comp.result()!.updated).toBe(3);
    expect(comp.result()!.skipped).toBe(0);
  });

  // Test 4: orphan panel hidden when orphans=[]
  it('orphanPanel_HiddenWhenOrphansEmpty', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult({ orphans: [] })),
    );

    // Act
    comp.submit();
    fixture.detectChanges();

    // Assert
    const orphanPanel = fixture.nativeElement.querySelector('.import-trades-modal__orphans');
    expect(orphanPanel).toBeNull();
  });

  // Test 5: orphan panel shows 3 rows when 3 orphans returned
  it('orphanPanel_Shows3RowsWhen3OrphansReturned', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');
    const orphans = [makeOrphan(111), makeOrphan(222), makeOrphan(333)];

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult({ orphans })),
    );

    // Act
    comp.submit();
    fixture.detectChanges();

    // Assert
    const rows = fixture.nativeElement.querySelectorAll('.import-trades-modal__orphan-row');
    expect(rows.length).toBe(3);
  });

  // Test 6: Copy button calls navigator.clipboard.writeText with magicNumber
  it('copyMagicNumber_CallsClipboardWriteText', async () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: writeTextMock },
      writable: true,
      configurable: true,
    });

    // Act
    comp.copyMagicNumber(2333376);

    // Assert
    expect(writeTextMock).toHaveBeenCalledWith('2333376');
  });
});
