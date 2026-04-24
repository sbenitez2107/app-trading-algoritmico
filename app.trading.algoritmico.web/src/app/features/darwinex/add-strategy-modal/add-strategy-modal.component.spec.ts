import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AddStrategyModalComponent } from './add-strategy-modal.component';
import { StrategyService, StrategyDto } from '../../../core/services/strategy.service';
import { HttpErrorResponse } from '@angular/common/http';

function makeStrategyDto(): StrategyDto {
  return {
    id: '123',
    name: 'Test',
    pseudocode: null,
    entryIndicators: null,
    priceIndicators: null,
    indicatorParameters: null,
    symbol: null,
    timeframe: null,
    backtestFrom: null,
    backtestTo: null,
    totalProfit: null,
    profitInPips: null,
    yearlyAvgProfit: null,
    yearlyAvgReturn: null,
    cagr: null,
    numberOfTrades: null,
    sharpeRatio: null,
    profitFactor: null,
    returnDrawdownRatio: null,
    winningPercentage: null,
    drawdown: null,
    drawdownPercent: null,
    dailyAvgProfit: null,
    monthlyAvgProfit: null,
    averageTrade: null,
    annualReturnMaxDdRatio: null,
    rExpectancy: null,
    rExpectancyScore: null,
    strQualityNumber: null,
    sqnScore: null,
    winsLossesRatio: null,
    payoutRatio: null,
    averageBarsInTrade: null,
    ahpr: null,
    zScore: null,
    zProbability: null,
    expectancy: null,
    deviation: null,
    exposure: null,
    stagnationInDays: null,
    stagnationPercent: null,
    numberOfWins: null,
    numberOfLosses: null,
    numberOfCancelled: null,
    grossProfit: null,
    grossLoss: null,
    averageWin: null,
    averageLoss: null,
    largestWin: null,
    largestLoss: null,
    maxConsecutiveWins: null,
    maxConsecutiveLosses: null,
    averageConsecutiveWins: null,
    averageConsecutiveLosses: null,
    averageBarsInWins: null,
    averageBarsInLosses: null,
    magicNumber: null,
    createdAt: new Date().toISOString(),
  };
}

function makeFile(name: string): File {
  return new File(['content'], name, { type: 'application/octet-stream' });
}

describe('AddStrategyModalComponent', () => {
  let strategyServiceMock: Partial<StrategyService>;

  beforeEach(() => {
    strategyServiceMock = {
      addToAccount: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AddStrategyModalComponent],
      providers: [{ provide: StrategyService, useValue: strategyServiceMock }],
    });
  });

  function create(accountId = 'acc-1') {
    const fixture = TestBed.createComponent(AddStrategyModalComponent);
    fixture.componentRef.setInput('accountId', accountId);
    fixture.detectChanges();
    return fixture;
  }

  // --- canSubmit computed tests ---

  it('canSubmit_NameEmptyBothFilesSelected_IsFalse', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act — name empty but files selected
    comp.sqxFile.set(makeFile('test.sqx'));
    comp.htmlFile.set(makeFile('test.html'));
    // name stays ''

    // Assert — spec R6 scenario 1 (missing name edge)
    expect(comp.canSubmit()).toBe(false);
  });

  it('canSubmit_NameSetOnlySqxSelected_IsFalse', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    comp.name.set('My Strategy');
    comp.sqxFile.set(makeFile('test.sqx'));
    // htmlFile stays null

    // Assert — spec R6 scenario 1
    expect(comp.canSubmit()).toBe(false);
  });

  it('canSubmit_NameSetOnlyHtmlSelected_IsFalse', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    comp.name.set('My Strategy');
    comp.htmlFile.set(makeFile('test.html'));
    // sqxFile stays null

    // Assert — spec R6 scenario 1
    expect(comp.canSubmit()).toBe(false);
  });

  it('canSubmit_NameSetBothFilesSelected_IsTrue', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    comp.name.set('My Strategy');
    comp.sqxFile.set(makeFile('test.sqx'));
    comp.htmlFile.set(makeFile('test.html'));

    // Assert — spec R6 scenario 2
    expect(comp.canSubmit()).toBe(true);
  });

  // --- submit tests ---

  it('submit_CallsAddToAccountWithCorrectFormData', () => {
    // Arrange
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;

    const sqx = makeFile('test.sqx');
    const html = makeFile('test.html');
    comp.name.set('My Strategy');
    comp.sqxFile.set(sqx);
    comp.htmlFile.set(html);

    (strategyServiceMock.addToAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeStrategyDto()),
    );

    // Act
    comp.submit();

    // Assert — magicNumber defaults to null when not set
    expect(strategyServiceMock.addToAccount).toHaveBeenCalledWith(
      'acc-1',
      'My Strategy',
      sqx,
      html,
      null,
    );
  });

  it('submit_OnSuccess_EmitsStrategyCreated', () => {
    // Arrange
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;
    const dto = makeStrategyDto();

    comp.name.set('My Strategy');
    comp.sqxFile.set(makeFile('test.sqx'));
    comp.htmlFile.set(makeFile('test.html'));

    (strategyServiceMock.addToAccount as ReturnType<typeof vi.fn>).mockReturnValue(of(dto));

    let emitted: StrategyDto | undefined;
    fixture.componentInstance.strategyCreated.subscribe((d: StrategyDto) => {
      emitted = d;
    });

    // Act
    comp.submit();

    // Assert
    expect(emitted).toEqual(dto);
  });

  it('submit_On400Error_SetsErrorSignal', () => {
    // Arrange
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;

    comp.name.set('My Strategy');
    comp.sqxFile.set(makeFile('test.sqx'));
    comp.htmlFile.set(makeFile('test.html'));

    (strategyServiceMock.addToAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request' })),
    );

    // Act
    comp.submit();

    // Assert
    expect(comp.error()).not.toBeNull();
  });

  // --- #6 Auto-suggest name from filename ---

  it('onSqxFileChange_NameEmpty_SetsNameToBasenameWithoutExtension', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    expect(comp.name()).toBe('');

    // Act
    const file = makeFile('my-strategy-v2.sqx');
    comp.onSqxFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert — name should be auto-set to basename without extension
    expect(comp.name()).toBe('my-strategy-v2');
  });

  it('onSqxFileChange_NameAlreadySet_DoesNotOverwriteName', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.name.set('My Custom Name');

    // Act
    const file = makeFile('other.sqx');
    comp.onSqxFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert — name must NOT be overwritten
    expect(comp.name()).toBe('My Custom Name');
  });

  it('onHtmlFileChange_NameEmpty_SetsNameToBasenameWithoutExtension', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    expect(comp.name()).toBe('');

    // Act
    const file = makeFile('backtest-report.html');
    comp.onHtmlFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert
    expect(comp.name()).toBe('backtest-report');
  });

  it('onHtmlFileChange_NameAlreadySet_DoesNotOverwriteName', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.name.set('Existing Name');

    // Act
    const file = makeFile('report.html');
    comp.onHtmlFileChange({ target: { files: [file] } } as unknown as Event);

    // Assert
    expect(comp.name()).toBe('Existing Name');
  });

  // --- #R-M2 Magic Number field ---

  it('magicNumber_ValidInteger_SubmittedAsMagicNumber', () => {
    // Arrange
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;
    const sqx = makeFile('test.sqx');
    const html = makeFile('test.html');
    const dto = makeStrategyDto();
    dto.magicNumber = 2333376;

    comp.name.set('My Strategy');
    comp.sqxFile.set(sqx);
    comp.htmlFile.set(html);

    (strategyServiceMock.addToAccount as ReturnType<typeof vi.fn>).mockReturnValue(of(dto));

    // Act
    comp.onMagicNumberChange('2333376');
    comp.submit();

    // Assert
    expect(comp.magicNumber()).toBe(2333376);
    expect(strategyServiceMock.addToAccount).toHaveBeenCalledWith(
      'acc-1',
      'My Strategy',
      sqx,
      html,
      2333376,
    );
  });

  it('magicNumber_Empty_SubmitsAsNull', () => {
    // Arrange
    const fixture = create('acc-1');
    const comp = fixture.componentInstance;
    const sqx = makeFile('test.sqx');
    const html = makeFile('test.html');

    comp.name.set('My Strategy');
    comp.sqxFile.set(sqx);
    comp.htmlFile.set(html);

    (strategyServiceMock.addToAccount as ReturnType<typeof vi.fn>).mockReturnValue(
      of(makeStrategyDto()),
    );

    // Act — leave magicNumber empty (default null)
    comp.submit();

    // Assert
    expect(comp.magicNumber()).toBeNull();
    expect(strategyServiceMock.addToAccount).toHaveBeenCalledWith(
      'acc-1',
      'My Strategy',
      sqx,
      html,
      null,
    );
  });

  it('magicNumber_NonNumeric_SetsValidationError', () => {
    // Arrange
    const fixture = create();
    const comp = fixture.componentInstance;

    // Act
    comp.onMagicNumberChange('abc');

    // Assert
    expect(comp.magicNumberError()).not.toBeNull();
  });
});
