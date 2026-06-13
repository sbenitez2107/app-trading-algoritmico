import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ImportTradesModalComponent } from './import-trades-modal.component';
import {
  TradingAccountService,
  TradeImportResultDto,
  OrphanMagicNumberDto,
  AutoAssignedStrategyDto,
  AvailableStrategyDto,
  SnapshotDto,
} from '../../../core/services/trading-account.service';
import { StrategyService } from '../../../core/services/strategy.service';

function makeOrphan(magicNumber: number, hint = 'Strategy_X'): OrphanMagicNumberDto {
  return {
    magicNumber,
    strategyNameHint: hint,
    tradeCount: 3,
  };
}

function makeAutoAssigned(
  magicNumber: number,
  strategyName = 'Strategy_X',
): AutoAssignedStrategyDto {
  return {
    strategyId: '00000000-0000-0000-0000-000000000001',
    strategyName,
    magicNumber,
    tradeCount: 2,
  };
}

function makeAvailable(
  id: string,
  name: string,
  magicNumber: number | null = null,
): AvailableStrategyDto {
  return { id, name, magicNumber };
}

function makeSnapshot(): SnapshotDto {
  return {
    reportTime: '2026-04-21T07:06:00Z',
    balance: 100_000,
    equity: 100_500,
    floatingPnL: 500,
    margin: 1_000,
    freeMargin: 99_000,
    closedTradePnL: 2_897,
    currency: 'USD',
  };
}

function makeResult(overrides: Partial<TradeImportResultDto> = {}): TradeImportResultDto {
  return {
    imported: 10,
    updated: 2,
    skipped: 1,
    orphans: [],
    autoAssigned: [],
    availableStrategies: [],
    snapshot: makeSnapshot(),
    ...overrides,
  };
}

function makeFile(name: string, type = 'text/html'): File {
  return new File(['<html></html>'], name, { type });
}

describe('ImportTradesModalComponent', () => {
  let tradingAccountServiceMock: Partial<TradingAccountService>;
  let strategyServiceMock: Partial<StrategyService>;

  beforeEach(() => {
    tradingAccountServiceMock = {
      importTrades: vi.fn(),
    };
    strategyServiceMock = {
      assignMagicNumber: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ImportTradesModalComponent],
      providers: [
        { provide: TradingAccountService, useValue: tradingAccountServiceMock },
        { provide: StrategyService, useValue: strategyServiceMock },
      ],
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

  // Auto-assigned panel hidden when autoAssigned=[]
  it('autoAssignedPanel_HiddenWhenEmpty', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult({ autoAssigned: [] })),
    );

    comp.submit();
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.import-trades-modal__auto-assigned');
    expect(panel).toBeNull();
  });

  // Auto-assigned panel renders one row per auto-assigned strategy
  it('autoAssignedPanel_Shows2RowsWhen2AutoAssigned', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');
    const autoAssigned = [
      makeAutoAssigned(111, 'Strategy 1.15.198'),
      makeAutoAssigned(222, 'Strategy 5.20.225'),
    ];

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult({ autoAssigned })),
    );

    comp.submit();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.import-trades-modal__auto-assigned-row');
    expect(rows.length).toBe(2);
  });

  // assignAndReimport: assigns magic and re-imports the same file with the new result
  it('assignAndReimport_HappyPath_CallsAssignThenImportAndUpdatesResult', async () => {
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);

    const initialResult = makeResult({
      orphans: [makeOrphan(392709, 'WF_X')],
      availableStrategies: [makeAvailable('strat-1', 'Strategy 1.15.198')],
    });
    const reimportedResult = makeResult({ imported: 12, orphans: [], availableStrategies: [] });

    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>)
      .mockReturnValueOnce(of(initialResult))
      .mockReturnValueOnce(of(reimportedResult));
    (strategyServiceMock.assignMagicNumber as ReturnType<typeof vi.fn>).mockReturnValue(
      of({ id: 'strat-1', magicNumber: 392709 } as never),
    );

    comp.submit();
    fixture.detectChanges();

    comp.onSelectStrategy(392709, 'strat-1');
    comp.assignAndReimport(392709);

    expect(strategyServiceMock.assignMagicNumber).toHaveBeenCalledWith('acc-1', 'strat-1', 392709);
    expect(tradingAccountServiceMock.importTrades).toHaveBeenCalledTimes(2);
    expect(comp.result()).toEqual(reimportedResult);
    expect(comp.assigningMagic()).toBeNull();
  });

  // assignAndReimport with no selection is a no-op
  it('assignAndReimport_NoSelection_DoesNothing', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeResult({ orphans: [makeOrphan(111)] })),
    );

    comp.submit();
    fixture.detectChanges();

    comp.assignAndReimport(111);

    expect(strategyServiceMock.assignMagicNumber).not.toHaveBeenCalled();
  });

  // assign error from backend (409) shows in assignError signal
  it('assignAndReimport_ConflictError_SetsAssignErrorMessage', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const file = makeFile('report.html');

    comp.onFileChange({ target: { files: [file] } } as unknown as Event);
    (tradingAccountServiceMock.importTrades as ReturnType<typeof vi.fn>).mockReturnValue(
      of(
        makeResult({
          orphans: [makeOrphan(222)],
          availableStrategies: [makeAvailable('strat-2', 'Strat')],
        }),
      ),
    );
    (strategyServiceMock.assignMagicNumber as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { message: 'Magic number already in use.' },
          }),
      ),
    );

    comp.submit();
    fixture.detectChanges();

    comp.onSelectStrategy(222, 'strat-2');
    comp.assignAndReimport(222);

    expect(comp.assignError()).toBe('Magic number already in use.');
    expect(comp.assigningMagic()).toBeNull();
    expect(tradingAccountServiceMock.importTrades).toHaveBeenCalledTimes(1);
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
