import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ImportStrategyBacktestsModalComponent } from './import-strategy-backtests-modal.component';
import {
  BacktestService,
  BacktestImportOutcome,
  BacktestImportResultDto,
  WalkForwardImportResultDto,
} from '../../../../core/services/backtest.service';
import { PlatformType } from '../../../../core/models/platform-type.model';

const STRATEGY_ID = 'strategy-1';

function makeFile(name: string): File {
  return new File(['x'], name, { type: 'text/csv' });
}

function imported(fileName: string, tradeCount = 329): BacktestImportResultDto {
  return {
    fileName,
    outcome: BacktestImportOutcome.Imported,
    tradeCount,
    rejectedRowCount: 0,
    reason: null,
  };
}

function rejected(fileName: string, reason: string): BacktestImportResultDto {
  return {
    fileName,
    outcome: BacktestImportOutcome.Rejected,
    tradeCount: null,
    rejectedRowCount: null,
    reason,
  };
}

describe('ImportStrategyBacktestsModalComponent', () => {
  let backtestServiceMock: Partial<BacktestService>;

  beforeEach(() => {
    backtestServiceMock = {
      importDeploy: vi.fn().mockReturnValue(of(imported('deploy.csv'))),
      importEvaluation: vi.fn().mockReturnValue(of(imported('evaluation.csv'))),
      importWalkForward: vi.fn().mockReturnValue(
        of({
          fileName: 'wf.csv',
          outcome: BacktestImportOutcome.Imported,
          windowCount: 6,
          oosFromDate: '2025-05-26T00:00:00',
          reason: null,
        } as WalkForwardImportResultDto),
      ),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ImportStrategyBacktestsModalComponent, TranslateModule.forRoot()],
      providers: [{ provide: BacktestService, useValue: backtestServiceMock }],
    });
  });

  function create(): ComponentFixture<ImportStrategyBacktestsModalComponent> {
    const fixture = TestBed.createComponent(ImportStrategyBacktestsModalComponent);
    fixture.componentRef.setInput('strategyId', STRATEGY_ID);
    fixture.componentRef.setInput('strategyName', 'BTC_H1_Fractal_MACD');
    fixture.detectChanges();
    return fixture;
  }

  it('submit_ImportedWithAWarningReason_ShowsTheWarningWithoutClaimingTheSlotFailed', async () => {
    // The server commits the run and its trades, THEN calibrates. A calibration failure leaves the
    // import true and the per-symbol point value stale, so it arrives as a non-Rejected outcome
    // carrying a reason. Rendering nothing for it is a silent skip; rendering it as an error would
    // claim the slot failed when its data landed.
    const warned: BacktestImportResultDto = {
      fileName: 'deploy.csv',
      outcome: BacktestImportOutcome.Imported,
      tradeCount: 329,
      rejectedRowCount: 0,
      reason: "imported, but the calibration of 'XAUUSD_M1_UTC02' failed",
    };
    (backtestServiceMock.importDeploy as ReturnType<typeof vi.fn>).mockReturnValue(of(warned));

    const fixture = create();
    fixture.componentInstance.onFileSelected('deploy', makeFile('deploy.csv'));
    fixture.componentInstance.submit();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('calibration');
    expect(host.querySelector('.import-backtests-modal__slot-warning')).not.toBeNull();
    expect(host.querySelectorAll('.import-backtests-modal__slot-error')).toHaveLength(0);
    expect(host.textContent).toContain('329');
  });

  it('slots_AreThreeLabelledFileInputs_NotOneInferringDropZone', () => {
    // The slot IS the declaration. Nothing in a trade-list CSV says whether it came from the
    // deployed parameters or the previous window's, so an unlabelled drop zone would have to guess
    // — and a wrong guess is a silent false out-of-sample claim.
    const fixture = create();
    const inputs = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('input[type="file"]'),
    ) as HTMLInputElement[];

    expect(inputs).toHaveLength(3);
    expect(inputs.every((i) => !i.multiple)).toBe(true);

    const labels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('label')).map(
      (l) => l.getAttribute('for'),
    );
    expect(labels).toEqual(
      expect.arrayContaining(['slot-deploy', 'slot-evaluation', 'slot-walk-forward']),
    );
  });

  it('submit_OnlyDeployFilled_ImportsOnlyDeployAndLeavesTheOtherSlotsUntouched', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.submit();

    expect(backtestServiceMock.importDeploy).toHaveBeenCalledTimes(1);
    // Third argument is explicit `undefined`, not omitted — this is the proof that the
    // undeclared path sends nothing over the wire (design D6/D7 arity change, task 10.6/10.4).
    expect(backtestServiceMock.importDeploy).toHaveBeenCalledWith(
      STRATEGY_ID,
      expect.any(File),
      undefined,
    );
    expect(backtestServiceMock.importEvaluation).not.toHaveBeenCalled();
    expect(backtestServiceMock.importWalkForward).not.toHaveBeenCalled();
  });

  it('submit_AllThreeFilled_ImportsEachSlotThroughItsOwnEndpoint', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.onFileSelected('evaluation', makeFile('e.csv'));
    comp.onFileSelected('walkForward', makeFile('wf.csv'));
    comp.submit();

    expect(backtestServiceMock.importDeploy).toHaveBeenCalledTimes(1);
    expect(backtestServiceMock.importEvaluation).toHaveBeenCalledTimes(1);
    expect(backtestServiceMock.importWalkForward).toHaveBeenCalledTimes(1);
  });

  it('submit_NothingSelected_DoesNotCallTheServerAtAll', () => {
    const fixture = create();

    fixture.componentInstance.submit();

    expect(backtestServiceMock.importDeploy).not.toHaveBeenCalled();
    expect(backtestServiceMock.importEvaluation).not.toHaveBeenCalled();
    expect(backtestServiceMock.importWalkForward).not.toHaveBeenCalled();
  });

  it('submit_WrongShapedFileInASlot_SurfacesThatSlotsMismatchReason', () => {
    (backtestServiceMock.importDeploy as ReturnType<typeof vi.fn>).mockReturnValue(
      of(rejected('wf.csv', 'expected trade-list header, found a different column shape')),
    );
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('wf.csv'));
    comp.submit();
    fixture.detectChanges();

    expect(comp.slotResult('deploy')?.outcome).toBe(BacktestImportOutcome.Rejected);
    expect(comp.slotResult('deploy')?.reason).toContain('different column shape');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('different column shape');
  });

  it('submit_OneSlotRejected_DoesNotStopTheOthersFromImporting', () => {
    (backtestServiceMock.importDeploy as ReturnType<typeof vi.fn>).mockReturnValue(
      of(rejected('bad.csv', 'multiple values in the Sample type column: IS, OOS1')),
    );
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('bad.csv'));
    comp.onFileSelected('evaluation', makeFile('good.csv'));
    comp.submit();

    expect(comp.slotResult('deploy')?.outcome).toBe(BacktestImportOutcome.Rejected);
    expect(comp.slotResult('evaluation')?.outcome).toBe(BacktestImportOutcome.Imported);
    expect(backtestServiceMock.importEvaluation).toHaveBeenCalledTimes(1);
  });

  it('submit_HttpFailureOnOneSlot_ShowsThatSlotsErrorAndKeepsTheModalOpen', () => {
    (backtestServiceMock.importDeploy as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new Error('SQX.BACKTESTS.IMPORT_ERROR')),
    );
    const fixture = create();
    const comp = fixture.componentInstance;
    let closed = false;
    comp.closed.subscribe(() => (closed = true));

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.submit();

    expect(comp.slotError('deploy')).toBe('SQX.BACKTESTS.IMPORT_ERROR');
    expect(closed).toBe(false);
  });

  it('submit_WalkForwardImported_SurfacesTheBoundaryDateItProduced', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('walkForward', makeFile('wf.csv'));
    comp.submit();

    expect(comp.walkForwardResult()?.windowCount).toBe(6);
    expect(comp.walkForwardResult()?.oosFromDate).toBe('2025-05-26T00:00:00');
  });

  it('onFileSelected_ReplacesThePreviousChoiceForThatSlotOnly', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('first.csv'));
    comp.onFileSelected('deploy', makeFile('second.csv'));
    comp.onFileSelected('evaluation', makeFile('eval.csv'));

    expect(comp.selectedFile('deploy')?.name).toBe('second.csv');
    expect(comp.selectedFile('evaluation')?.name).toBe('eval.csv');
    expect(comp.selectedFile('walkForward')).toBeNull();
  });

  it('sourcePlatformSelect_RendersExactlyOneOptionalSelectDefaultedToNotDeclared', () => {
    const fixture = create();
    const host = fixture.nativeElement as HTMLElement;

    const body = host.querySelector('.import-backtests-modal__body') as HTMLElement;
    const selects = body.querySelectorAll('select');
    expect(selects).toHaveLength(1);

    const select = selects[0];
    const intro = body.querySelector('.import-backtests-modal__intro') as HTMLElement;
    const firstSlot = body.querySelector('.import-backtests-modal__slot') as HTMLElement;
    // Placed after the intro, before the first slot section (design D6 "Where" row).
    expect(intro.compareDocumentPosition(select) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(
      select.compareDocumentPosition(firstSlot) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    const firstOption = select.querySelector('option') as HTMLOptionElement;
    expect(firstOption.value).toBe('');
    expect(firstOption.selected).toBe(true);
  });

  it('sourcePlatformSelect_SelectingMT4_UpdatesTheSignalToPlatformTypeMT4AndStartsAtNull', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    expect(comp.sourcePlatform()).toBeNull();

    const select = (fixture.nativeElement as HTMLElement).querySelector(
      '.import-backtests-modal__body select',
    ) as HTMLSelectElement;
    select.value = String(PlatformType.MT4);
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(comp.sourcePlatform()).toBe(PlatformType.MT4);
  });

  it('submit_WithDeployAndEvaluationFilledAndMT4Selected_ForwardsMT4ToDeployAndEvaluationOnlyNeverToWalkForward', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.onFileSelected('evaluation', makeFile('e.csv'));
    comp.onFileSelected('walkForward', makeFile('wf.csv'));
    comp.onSourcePlatformChange(String(PlatformType.MT4));
    comp.submit();

    expect(backtestServiceMock.importDeploy).toHaveBeenCalledWith(
      STRATEGY_ID,
      expect.any(File),
      PlatformType.MT4,
    );
    expect(backtestServiceMock.importEvaluation).toHaveBeenCalledWith(
      STRATEGY_ID,
      expect.any(File),
      PlatformType.MT4,
    );
    // walkForward's signature is untouched — no third argument at all.
    expect(backtestServiceMock.importWalkForward).toHaveBeenCalledWith(
      STRATEGY_ID,
      expect.any(File),
    );
  });

  it('submit_WithNoPlatformChosen_ForwardsUndefinedNotNullNotZero', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.submit();

    const call = (backtestServiceMock.importDeploy as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(call[2]).toBeUndefined();
  });

  it('undeclaredNote_WhenATradeListFileIsQueuedAndPlatformIsNull_RendersAndSubmitStaysEnabled', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.onFileSelected('deploy', makeFile('d.csv'));
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const note = host.querySelector('[role="status"].import-backtests-modal__undeclared-note');
    expect(note).not.toBeNull();
    expect(note!.textContent).toContain('SQX.BACKTESTS.SOURCE_PLATFORM_UNDECLARED_NOTE');

    const submitBtn = host.querySelector('.btn--primary') as HTMLButtonElement;
    expect(submitBtn.disabled).toBe(false);
  });

  it('cancel_EmitsClosedWithoutImportingAnything', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    let closed = false;
    comp.closed.subscribe(() => (closed = true));

    comp.onFileSelected('deploy', makeFile('d.csv'));
    comp.onClose();

    expect(closed).toBe(true);
    expect(backtestServiceMock.importDeploy).not.toHaveBeenCalled();
  });
});
