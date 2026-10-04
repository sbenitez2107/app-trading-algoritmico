import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import { DEFAULT_GROUP_FORM } from '../ftmo-group-simulation.mappers';
import { FtmoGroupFormComponent } from './ftmo-group-form.component';

// The browser's own constraint validation must never block a Run: a number input without a step
// attribute only accepts integers, so a lot step of 0.01 or an FX band of 1.05 was rejected by the
// browser before onSubmit ran. Validation belongs to canRunGroup and the backend, not the browser.
describe('FtmoGroupFormComponent native validation', () => {
  function render(): HTMLElement {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupFormComponent, TranslateModule.forRoot()],
    });
    const fixture = TestBed.createComponent(FtmoGroupFormComponent);
    fixture.componentRef.setInput('value', DEFAULT_GROUP_FORM);
    fixture.componentRef.setInput('showFx', true);
    fixture.componentRef.setInput('canRun', true);
    fixture.componentRef.setInput('running', false);
    fixture.componentRef.setInput('readout', null);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('theFormOptsOutOfBrowserConstraintValidation', () => {
    const form = render().querySelector('form');
    expect(form?.hasAttribute('novalidate')).toBe(true);
  });

  it('everyNumberInputAcceptsDecimals_SoA0_01StepIsNotRejected', () => {
    const inputs = Array.from(render().querySelectorAll<HTMLInputElement>('input[type="number"]'));
    expect(inputs.length).toBeGreaterThan(0);
    for (const input of inputs) {
      expect(input.getAttribute('step'), input.name).toBe('any');
    }
  });
});
