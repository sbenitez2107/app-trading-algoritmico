import { TestBed } from '@angular/core/testing';
import { SavePresetModalComponent } from './save-preset-modal.component';

describe('SavePresetModalComponent', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SavePresetModalComponent],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(SavePresetModalComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('canSave_EmptyName_IsFalse', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    expect(comp.presetName()).toBe('');
    expect(comp.canSave()).toBe(false);
  });

  it('canSave_NameHasValue_IsTrue', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.presetName.set('Performance view');
    expect(comp.canSave()).toBe(true);
  });

  it('onSave_EmptyName_DoesNotEmit', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    let emitted: string | undefined;
    comp.save.subscribe((v: string) => {
      emitted = v;
    });

    comp.onSave();

    expect(emitted).toBeUndefined();
  });

  it('onSave_ValidName_EmitsPresetName', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.presetName.set('Performance view');

    let emitted: string | undefined;
    comp.save.subscribe((v: string) => {
      emitted = v;
    });

    comp.onSave();

    expect(emitted).toBe('Performance view');
  });

  it('onCancel_EmitsCancelledEvent', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    let cancelled = false;
    comp.cancelled.subscribe(() => {
      cancelled = true;
    });

    comp.onCancel();

    expect(cancelled).toBe(true);
  });

  it('onSave_TrimsWhitespace_BeforeEmitting', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    comp.presetName.set('  Performance  ');

    let emitted: string | undefined;
    comp.save.subscribe((v: string) => {
      emitted = v;
    });

    comp.onSave();

    expect(emitted).toBe('Performance');
  });
});
