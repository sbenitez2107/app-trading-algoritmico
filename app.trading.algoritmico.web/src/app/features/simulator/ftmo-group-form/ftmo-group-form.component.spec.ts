import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it, vi } from 'vitest';
import { BacktestRunKind } from '../../../core/services/backtest.service';
import {
  DEFAULT_GROUP_FORM,
  GroupFormValue,
  WorstCaseReadoutVm,
  toWorstCaseReadout,
} from '../ftmo-group-simulation.mappers';
import { FtmoGroupFormComponent } from './ftmo-group-form.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

interface Setup {
  value?: GroupFormValue;
  showFx?: boolean;
  canRun?: boolean;
  running?: boolean;
  readout?: WorstCaseReadoutVm | null;
  lang?: 'en' | 'es';
}

function create(setup: Setup = {}): ComponentFixture<FtmoGroupFormComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FtmoGroupFormComponent, TranslateModule.forRoot()],
  });
  const translate = TestBed.inject(TranslateService);
  translate.setTranslation('en', en);
  translate.setTranslation('es', es);
  translate.use(setup.lang ?? 'en');
  const fixture = TestBed.createComponent(FtmoGroupFormComponent);
  fixture.componentRef.setInput('value', setup.value ?? DEFAULT_GROUP_FORM);
  fixture.componentRef.setInput('showFx', setup.showFx ?? false);
  fixture.componentRef.setInput('canRun', setup.canRun ?? false);
  fixture.componentRef.setInput('running', setup.running ?? false);
  fixture.componentRef.setInput('readout', setup.readout ?? null);
  fixture.detectChanges();
  return fixture;
}

function el(f: ComponentFixture<unknown>): HTMLElement {
  return f.nativeElement as HTMLElement;
}

function input(f: ComponentFixture<unknown>, name: string): HTMLInputElement | null {
  return el(f).querySelector<HTMLInputElement>(`input[name="${name}"]`);
}

function text(f: ComponentFixture<unknown>): string {
  return (el(f).textContent ?? '').replace(/\s+/g, ' ');
}

const SIM = (en as JsonTree)['FTMO_SIMULATION'];
const SIM_ES = (es as JsonTree)['FTMO_SIMULATION'];
const READOUT_EN = (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['READOUT'];
const READOUT_ES = (es as JsonTree)['SIMULATOR']['FTMO_GROUP']['READOUT'];

/** The exact text of the FTMO daily limit reference line (the second reference). */
function dailyRef(f: ComponentFixture<unknown>): string {
  const refs = el(f).querySelectorAll('.ftmo-group-form__reference');
  return (refs[1]?.textContent ?? '').trim();
}

describe('FtmoGroupFormComponent', () => {
  it('rendersThePrefillsAndLeavesTheRiskEmpty', () => {
    const f = create();
    expect(input(f, 'broker')!.value).toBe('FTMO');
    expect(input(f, 'initialCapital')!.value).toBe('10000');
    expect(input(f, 'targetRiskPerTrade')!.value).toBe('');
    expect(input(f, 'sizeDecimals')!.value).toBe('2');
    expect(input(f, 'step')!.value).toBe('0.01');
    expect(input(f, 'minLot')!.value).toBe('0.01');
    expect(input(f, 'maxLots')!.value).toBe('10');
  });

  it('labelsTheGridAsTheBacktestGrid_InEnglishAndSpanish', () => {
    expect(el(create()).querySelector('legend')!.textContent).toContain(
      SIM['FIELDS']['SOURCE_LOT_GRID_LABEL'],
    );
    expect(el(create({ lang: 'es' })).querySelector('legend')!.textContent).toContain(
      SIM_ES['FIELDS']['SOURCE_LOT_GRID_LABEL'],
    );
  });

  it('hasASingleRiskFieldAndNoPerMemberRiskInput', () => {
    const f = create();
    expect(el(f).querySelectorAll('input[name="targetRiskPerTrade"]')).toHaveLength(1);
    expect(el(f).querySelectorAll('input[name^="risk"]')).toHaveLength(0);
  });

  it('hidesTheFxInputsUnlessAMemberNeedsABand', () => {
    expect(input(create({ showFx: false }), 'fxLow')).toBeNull();
    expect(input(create({ showFx: false }), 'fxHigh')).toBeNull();
    const shown = create({ showFx: true });
    expect(input(shown, 'fxLow')).not.toBeNull();
    expect(input(shown, 'fxHigh')).not.toBeNull();
  });

  it('disablesRunWhenItCannotRun_AndEnablesItWithoutAnFxBand', () => {
    const run = (f: ComponentFixture<unknown>): HTMLButtonElement =>
      el(f).querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(run(create({ canRun: false })).disabled).toBe(true);
    expect(run(create({ canRun: true, showFx: true })).disabled).toBe(false);
  });

  it('emitsRunOnSubmitOnlyWhenRunnable', () => {
    const ran = vi.fn();
    const f = create({ canRun: true });
    f.componentInstance.run.subscribe(ran);
    el(f)
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    expect(ran).toHaveBeenCalledTimes(1);

    const blocked = vi.fn();
    const g = create({ canRun: false });
    g.componentInstance.run.subscribe(blocked);
    el(g)
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    expect(blocked).not.toHaveBeenCalled();
  });

  it('emitsTheMergedValueOnEdit_KeepingSizeDecimalsZeroAndClearingToNull', () => {
    const f = create();
    const emitted: GroupFormValue[] = [];
    f.componentInstance.valueChange.subscribe((v) => emitted.push(v));

    const decimals = input(f, 'sizeDecimals')!;
    decimals.value = '0';
    decimals.dispatchEvent(new Event('input'));
    expect(emitted[0].sizeDecimals).toBe(0);
    expect(emitted[0].broker).toBe('FTMO');

    const capital = input(f, 'initialCapital')!;
    capital.value = '';
    capital.dispatchEvent(new Event('input'));
    expect(emitted[1].initialCapital).toBeNull();

    const broker = input(f, 'broker')!;
    broker.value = 'Other';
    broker.dispatchEvent(new Event('input'));
    expect(emitted[2].broker).toBe('Other');
  });

  it('showsARunningStatusWhileInFlight', () => {
    expect(text(create({ running: true }))).toContain(SIM['RUNNING']);
    expect(text(create({ running: false }))).not.toContain(SIM['RUNNING']);
  });
});

describe('FtmoGroupFormComponent worst-case readout', () => {
  const readoutText = (f: ComponentFixture<unknown>): string => text(f);

  it('showsKTimesRiskAgainstBothReferences_WithEveryPlaceholderFilled', () => {
    const f = create({ readout: toWorstCaseReadout(4, 25, 10000, null, []) });
    const t = readoutText(f);
    expect(t).toContain('100');
    expect(t).toContain('1.00%');
    expect(t).toContain('1%');
    expect(t).toContain('5%');
    expect(t).not.toContain('{{');
    expect(t).not.toContain('SIMULATOR.');
    expect(el(f).querySelector('.ftmo-group-form__exceeded')).toBeNull();
  });

  it('marksTheAcademyCriterionAsExceededWithNeutralWording_AndRunStaysEnabled', () => {
    const f = create({ readout: toWorstCaseReadout(4, 50, 10000, null, []), canRun: true });
    const flags = Array.from(el(f).querySelectorAll('.ftmo-group-form__exceeded')).map(
      (e) => e.textContent ?? '',
    );
    expect(flags).toHaveLength(1);
    expect(flags[0]).toContain('1%');
    expect(el(f).querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
  });

  it('showsTheObservedPeakLabelledWithItsKind', () => {
    const f = create({
      readout: toWorstCaseReadout(4, 25, 10000, 0.05, [{ kind: BacktestRunKind.Deploy, peak: 3 }]),
    });
    const observed = el(f).querySelector('.ftmo-group-form__observed')!.textContent ?? '';
    expect(observed).toContain(SIM['KIND']['DEPLOY']);
    expect(observed).toContain('75');
    expect(observed).not.toContain('{{');
  });

  it('rendersInSpanishWithNoPlaceholderOrRawKey', () => {
    const f = create({
      lang: 'es',
      readout: toWorstCaseReadout(4, 50, 10000, 0.04, [
        { kind: BacktestRunKind.Evaluation, peak: 2 },
      ]),
    });
    const t = readoutText(f);
    expect(t).not.toContain('{{');
    expect(t).not.toContain('SIMULATOR.');
    expect(t).toContain(SIM_ES['KIND']['EVALUATION']);
    expect(dailyRef(f)).toBe(READOUT_ES['DAILY_REF'].replace('{{limit}}', '4'));
  });

  it('rendersAnEchoedFraction0_05As5Percent_InEnglishAndSpanish_AndDoesNotFlagA4PercentRisk (F3a RELIABILITY-001)', () => {
    for (const [lang, dict] of [
      ['en', READOUT_EN],
      ['es', READOUT_ES],
    ] as const) {
      const f = create({ lang, readout: toWorstCaseReadout(4, 100, 10000, 0.05, []) });
      expect(dailyRef(f)).toBe(dict['DAILY_REF'].replace('{{limit}}', '5'));
      const flags = Array.from(el(f).querySelectorAll('.ftmo-group-form__exceeded')).map((e) =>
        (e.textContent ?? '').trim(),
      );
      expect(flags).toEqual([dict['ABOVE_ACADEMY'].replace('{{limit}}', '1')]);
    }
  });

  it('showsNothingWithoutAReadout', () => {
    expect(el(create({ readout: null })).querySelector('.ftmo-group-form__readout')).toBeNull();
  });
});
