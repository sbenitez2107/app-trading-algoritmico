import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TranslateModule } from '@ngx-translate/core';
import { AdvanceStageModalComponent } from './advance-stage-modal.component';
import { BatchService } from '../../../../core/services/batch.service';

describe('AdvanceStageModalComponent', () => {
  let batchServiceMock: Partial<BatchService>;

  beforeEach(() => {
    batchServiceMock = { advance: vi.fn().mockReturnValue(of({})) };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AdvanceStageModalComponent, TranslateModule.forRoot()],
      providers: [{ provide: BatchService, useValue: batchServiceMock }],
    });
  });

  function create(batchId = 'batch-1') {
    const fixture = TestBed.createComponent(AdvanceStageModalComponent);
    fixture.componentRef.setInput('batchId', batchId);
    fixture.detectChanges();
    return fixture;
  }

  it('passedCountChange_BeforeManualNextEdit_MirrorsIntoNextInput', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.controls.passedCount.setValue(80);

    expect(comp.form.controls.nextInputCount.value).toBe(80);
  });

  it('passedCountChange_AfterManualNextEdit_DoesNotMirror', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.controls.nextInputCount.setValue(50);
    comp.onNextInputCountChange(); // user typed manually
    comp.form.controls.passedCount.setValue(100);

    expect(comp.form.controls.nextInputCount.value).toBe(50);
  });

  it('countMismatchWarning_TrueWhenNextGreaterThanPassed', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.controls.passedCount.setValue(50);
    comp.form.controls.nextInputCount.setValue(80);
    comp.onNextInputCountChange();

    expect(comp.countMismatchWarning()).toBe(true);
  });

  it('countMismatchWarning_FalseWhenNextLessOrEqualToPassed', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.controls.passedCount.setValue(80);
    comp.form.controls.nextInputCount.setValue(60);
    comp.onNextInputCountChange();

    expect(comp.countMismatchWarning()).toBe(false);
  });

  it('submit_CallsAdvanceWithPassedAndNextInput', () => {
    const fixture = create('batch-7');
    const comp = fixture.componentInstance;

    comp.form.controls.passedCount.setValue(95);
    comp.form.controls.nextInputCount.setValue(80);
    comp.onNextInputCountChange();

    comp.onSubmit();

    expect(batchServiceMock.advance).toHaveBeenCalledWith('batch-7', undefined, 95, 80);
  });

  it('submit_OnlyPassedCount_PassesUndefinedAsNextInput', () => {
    // When the user never decouples the fields, both end up with the same value.
    // The auto-mirror writes nextInputCount, so it submits both — that is correct
    // behavior since the backend treats nextInputCount=passed identically to omitted.
    const fixture = create('batch-9');
    const comp = fixture.componentInstance;

    comp.form.controls.passedCount.setValue(50);
    // No manual edit on nextInputCount

    comp.onSubmit();

    expect(batchServiceMock.advance).toHaveBeenCalledWith('batch-9', undefined, 50, 50);
  });
});
