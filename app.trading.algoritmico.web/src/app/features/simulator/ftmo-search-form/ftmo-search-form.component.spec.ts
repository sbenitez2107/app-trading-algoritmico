import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';
import { DEFAULT_SEARCH_FORM, SearchFormValue } from '../ftmo-group-search.mappers';
import { FtmoSearchFormComponent } from './ftmo-search-form.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

const ACCOUNTS = [
  { id: 'a1', name: 'Other' },
  { id: 'a2', name: 'SBDEMO2' },
];

interface Setup {
  value?: SearchFormValue;
  maxMembers?: number;
  showFx?: boolean;
  canStart?: boolean;
  running?: boolean;
  accountId?: string | null;
}

function setup(lang: 'en' | 'es', o: Setup = {}) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoSearchFormComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(lang);
  const fixture: ComponentFixture<FtmoSearchFormComponent> =
    TestBed.createComponent(FtmoSearchFormComponent);
  fixture.componentRef.setInput('value', o.value ?? DEFAULT_SEARCH_FORM);
  fixture.componentRef.setInput('accounts', ACCOUNTS);
  fixture.componentRef.setInput('accountId', o.accountId === undefined ? 'a2' : o.accountId);
  fixture.componentRef.setInput('maxMembers', o.maxMembers ?? 4);
  fixture.componentRef.setInput('showFx', o.showFx ?? false);
  fixture.componentRef.setInput('canStart', o.canStart ?? true);
  fixture.componentRef.setInput('running', o.running ?? false);
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

function input(el: HTMLElement, name: string): HTMLInputElement {
  return el.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
}

function type(fixture: ComponentFixture<unknown>, el: HTMLElement, name: string, text: string) {
  const field = input(el, name);
  field.value = text;
  field.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

describe('FtmoSearchFormComponent', () => {
  it('rendersTheDefaults', () => {
    const { el } = setup('en');
    expect(input(el, 'minMembers').value).toBe('2');
    expect(input(el, 'maxMembers').value).toBe('4');
    expect(input(el, 'maxPerInstrument').value).toBe('2');
    expect(input(el, 'excludeIdentical').checked).toBe(true);
    expect(input(el, 'onePercentRule').checked).toBe(false);
    expect(input(el, 'ceilingPercent').value).toBe('5');
    expect(input(el, 'initialCapital').value).toBe('10000');
    expect(input(el, 'targetRiskPerTrade').value).toBe('');
  });

  it('selectsTheGivenAccount_AndEmitsAChange', () => {
    const { fixture, el } = setup('en');
    const select = el.querySelector<HTMLSelectElement>('select[name="account"]')!;
    expect(select.value).toBe('a2');
    let emitted: string | null = null;
    fixture.componentInstance.accountChange.subscribe((id) => (emitted = id));
    select.value = 'a1';
    select.dispatchEvent(new Event('change'));
    expect(emitted).toBe('a1');
  });

  it('doesNotExposeTheBrokerOrTheLotGrid', () => {
    const { el } = setup('en');
    for (const name of ['broker', 'sizeDecimals', 'step', 'minLot', 'maxLots']) {
      expect(el.querySelector(`[name="${name}"]`), name).toBeNull();
    }
  });

  it('statesTheSizeBoundFromTheCandidatesCap', () => {
    const { el } = setup('en', { maxMembers: 4 });
    expect(el.textContent).toContain('between 2 and 4');
    const five = setup('en', { maxMembers: 5 });
    expect(five.el.textContent).toContain('between 2 and 5');
  });

  it('flagsAMaxAboveTheCap_AndDisablesStartViaCanStart', () => {
    const { el } = setup('en', {
      value: { ...DEFAULT_SEARCH_FORM, targetRiskPerTrade: 25, maxMembers: 5 },
      maxMembers: 4,
      canStart: false,
    });
    expect(el.querySelector('.ftmo-search-form__size-error')).not.toBeNull();
    expect(el.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  });

  it('startIsDisabledWhenCanStartIsFalse_AndEnabledOtherwise', () => {
    expect(
      setup('en', { canStart: false }).el.querySelector<HTMLButtonElement>('button[type="submit"]')!
        .disabled,
    ).toBe(true);
    expect(
      setup('en', { canStart: true }).el.querySelector<HTMLButtonElement>('button[type="submit"]')!
        .disabled,
    ).toBe(false);
  });

  it('showsTheFxInputsOnlyWhenNeeded', () => {
    expect(input(setup('en', { showFx: false }).el, 'fxLow')).toBeNull();
    expect(input(setup('en', { showFx: true }).el, 'fxLow')).not.toBeNull();
    expect(input(setup('en', { showFx: true }).el, 'fxHigh')).not.toBeNull();
  });

  it.each(['en', 'es'] as const)('anInvalidFxBandShowsATranslatedHint_%s', (lang) => {
    const dict = (lang === 'en' ? en : es) as JsonTree;
    const hint = dict['SIMULATOR']['FTMO_SEARCH']['FORM']['FX_INVALID'];
    expect(typeof hint).toBe('string');
    const bad = setup(lang, { showFx: true, value: { ...DEFAULT_SEARCH_FORM, fxLow: 1.2 } }).el;
    const alert = bad.querySelector('.ftmo-search-form__fx-error')!;
    expect(alert.textContent).toContain(hint);
    expect(alert.textContent).not.toContain('{{');
    expect(alert.textContent).not.toContain('SIMULATOR.');
    const ok = setup(lang, {
      showFx: true,
      value: { ...DEFAULT_SEARCH_FORM, fxLow: 1.05, fxHigh: 1.1 },
    }).el;
    expect(ok.querySelector('.ftmo-search-form__fx-error')).toBeNull();
    const hidden = setup(lang, { showFx: false, value: { ...DEFAULT_SEARCH_FORM, fxLow: 1.2 } }).el;
    expect(hidden.querySelector('.ftmo-search-form__fx-error')).toBeNull();
  });

  it('theFormOptsOutOfNativeValidation_AndEveryNumberInputAcceptsDecimals', () => {
    const { el } = setup('en', { showFx: true });
    expect(el.querySelector('form')?.hasAttribute('novalidate')).toBe(true);
    const numbers = Array.from(el.querySelectorAll<HTMLInputElement>('input[type="number"]'));
    expect(numbers.length).toBeGreaterThan(5);
    for (const field of numbers) expect(field.getAttribute('step'), field.name).toBe('any');
  });

  it('emitsAnEmptyInputAsNull_AndATypedZeroAsZero', () => {
    const { fixture, el } = setup('en');
    const emitted: SearchFormValue[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));
    type(fixture, el, 'maxPerInstrument', '');
    type(fixture, el, 'maxPerInstrument', '0');
    expect(emitted[0].maxPerInstrument).toBeNull();
    expect(emitted[1].maxPerInstrument).toBe(0);
  });

  it('rendersAZeroValueInTheInput_NotAsEmpty', () => {
    const { el } = setup('en', { value: { ...DEFAULT_SEARCH_FORM, maxFullSimulations: 0 } });
    expect(input(el, 'maxFullSimulations').value).toBe('0');
  });

  it('emitsTheCeilingAsTypedPercent_WithoutConverting', () => {
    const { fixture, el } = setup('en');
    const emitted: SearchFormValue[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));
    type(fixture, el, 'ceilingPercent', '7');
    expect(emitted[0].ceilingPercent).toBe(7);
  });

  it('emitsTheCheckboxes', () => {
    const { fixture, el } = setup('en');
    const emitted: SearchFormValue[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));
    const box = input(el, 'onePercentRule');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    expect(emitted[0].onePercentRule).toBe(true);
    const exclude = input(el, 'excludeIdentical');
    exclude.checked = false;
    exclude.dispatchEvent(new Event('change'));
    expect(emitted[1].excludeIdentical).toBe(false);
  });

  it('startEmitsOnlyWhenAllowed', () => {
    const allowed = setup('en', { canStart: true });
    let count = 0;
    allowed.fixture.componentInstance.start.subscribe(() => count++);
    allowed.el.querySelector('form')!.dispatchEvent(new Event('submit'));
    expect(count).toBe(1);
    const blocked = setup('en', { canStart: false });
    blocked.fixture.componentInstance.start.subscribe(() => count++);
    blocked.el.querySelector('form')!.dispatchEvent(new Event('submit'));
    expect(count).toBe(1);
  });

  it.each([
    ['en', en],
    ['es', es],
  ] as const)('rendersWithTheRealDictionary_NoBracesNoRawKeys_%s', (lang, dictionary) => {
    const { el } = setup(lang, { showFx: true, running: true });
    const text = el.textContent ?? '';
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
    expect(text).toContain((dictionary as JsonTree)['SIMULATOR']['FTMO_SEARCH']['FORM']['START']);
  });

  it('theTwoLocalesRenderDifferentText', () => {
    expect(setup('en').el.textContent).not.toBe(setup('es').el.textContent);
  });
});
