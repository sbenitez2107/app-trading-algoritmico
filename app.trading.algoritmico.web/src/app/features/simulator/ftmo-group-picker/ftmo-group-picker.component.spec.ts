import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import {
  FtmoGroupCandidateDto,
  FtmoGroupCandidateRunDto,
} from '../../../core/models/ftmo-group-simulation.model';
import { FtmoGroupPickerComponent } from './ftmo-group-picker.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };
const PICKER_EN = (en as JsonTree)['SIMULATOR']['FTMO_GROUP']['PICKER'] as JsonTree;
const PICKER_ES = (es as JsonTree)['SIMULATOR']['FTMO_GROUP']['PICKER'] as JsonTree;

function run(overrides: Partial<FtmoGroupCandidateRunDto> = {}): FtmoGroupCandidateRunDto {
  return {
    runId: 'run',
    symbol: 'XAUUSD',
    tradeCount: 120,
    firstOpen: '2024-01-02T08:00:00',
    lastClose: '2024-06-30T20:15:00',
    hasInstrumentSpec: true,
    isCalibrated: true,
    profitCurrency: 'USD',
    needsFxBand: false,
    sourceTimeZoneId: 'Etc/GMT-2',
    ...overrides,
  };
}

function candidate(
  strategyId: string,
  name: string,
  symbol: string | null,
  overrides: Partial<FtmoGroupCandidateDto> = {},
): FtmoGroupCandidateDto {
  return {
    strategyId,
    name,
    symbol,
    deploy: run(),
    evaluation: run(),
    nameExistsOnOtherAccount: false,
    ...overrides,
  };
}

const CANDIDATES: FtmoGroupCandidateDto[] = [
  candidate('1', 'Gold Breakout', 'XAUUSD'),
  candidate('2', 'Gold Reversal', 'XAUUSD', {
    evaluation: null,
    nameExistsOnOtherAccount: true,
  }),
  candidate('3', 'Euro Trend', 'EURUSD', {
    deploy: run({
      symbol: 'EURUSD',
      hasInstrumentSpec: false,
      isCalibrated: false,
      needsFxBand: true,
      tradeCount: 0,
      firstOpen: null,
      lastClose: null,
    }),
    evaluation: run({ symbol: 'EURUSD', isCalibrated: false }),
  }),
];

describe('FtmoGroupPickerComponent with the real dictionaries', () => {
  let fixture: ComponentFixture<FtmoGroupPickerComponent>;
  let emitted: ReadonlySet<string>[];

  function render(
    lang: 'en' | 'es',
    selected: string[] = [],
    maxMembers = 2,
    candidates: FtmoGroupCandidateDto[] = CANDIDATES,
  ): HTMLElement {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupPickerComponent, TranslateModule.forRoot()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en);
    translate.setTranslation('es', es);
    translate.use(lang);
    fixture = TestBed.createComponent(FtmoGroupPickerComponent);
    fixture.componentRef.setInput('candidates', candidates);
    fixture.componentRef.setInput('maxMembers', maxMembers);
    fixture.componentRef.setInput('selectedIds', new Set(selected));
    emitted = [];
    fixture.componentInstance.selectionChange.subscribe((s) => emitted.push(s));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function rows(el: HTMLElement): HTMLElement[] {
    return Array.from(el.querySelectorAll<HTMLElement>('tbody tr'));
  }

  function checkbox(el: HTMLElement, name: string): HTMLInputElement {
    const row = rows(el).find((r) => (r.textContent ?? '').includes(name));
    return row!.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
  }

  function assertNoLeaks(text: string): void {
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  }

  it('rendersEveryFlagInEnglish_WithTheDeployRangeAndAbsentEvaluation', () => {
    const el = render('en');
    const text = el.textContent ?? '';
    assertNoLeaks(text);
    const gold = rows(el).find((r) => (r.textContent ?? '').includes('Gold Reversal'))!;
    const goldText = gold.textContent ?? '';
    expect(goldText).toContain('2024-01-02 to 2024-06-30');
    expect(goldText).toContain('120 trades');
    expect(goldText).toContain(PICKER_EN['KIND']['ABSENT']);
    expect(goldText).toContain(PICKER_EN['FLAG']['OTHER_ACCOUNT']);
    expect(goldText).toContain(PICKER_EN['KIND']['SPEC_YES']);
    expect(goldText).toContain(PICKER_EN['KIND']['CALIBRATION_YES']);
  });

  it('flagsAMissingSpecAndCalibrationAndFxNeed_AndStaysSelectable', () => {
    const el = render('en');
    const euro = rows(el).find((r) => (r.textContent ?? '').includes('Euro Trend'))!;
    const euroText = euro.textContent ?? '';
    expect(euroText).toContain(PICKER_EN['KIND']['SPEC_NO']);
    expect(euroText).toContain(PICKER_EN['KIND']['CALIBRATION_NA']);
    expect(euroText).toContain(PICKER_EN['KIND']['CALIBRATION_NO']);
    expect(euroText).toContain(PICKER_EN['KIND']['NO_TRADES']);
    const deployCell = euro.querySelectorAll('td')[3].textContent ?? '';
    expect(deployCell).toContain('0 trades');
    expect(deployCell).not.toContain('120');
    expect(euroText).toContain(PICKER_EN['FLAG']['FX_NEEDED']);
    expect(checkbox(el, 'Euro Trend').disabled).toBe(false);
  });

  it('rendersTheSameFlagsInSpanish_WithNoLeaksAndDifferentText', () => {
    const elEs = render('es');
    const text = elEs.textContent ?? '';
    assertNoLeaks(text);
    expect(text).toContain(PICKER_ES['KIND']['SPEC_NO']);
    expect(text).toContain(PICKER_ES['FLAG']['OTHER_ACCOUNT']);
    expect(text).toContain('2024-01-02 a 2024-06-30');
    expect(text).not.toContain(PICKER_EN['FLAG']['OTHER_ACCOUNT']);
  });

  it('theCapMessageIsTranslatedWithItsMaxParam_OnlyAtTheCap', () => {
    const below = render('en', ['1'], 2);
    expect(below.textContent).not.toContain('Selection limit reached');

    const atCap = render('en', ['1', '2'], 2);
    const text = atCap.textContent ?? '';
    expect(text).toContain('a group has at most 2 strategies');
    assertNoLeaks(text);

    const atCapEs = render('es', ['1', '2'], 2);
    expect(atCapEs.textContent).toContain('como máximo 2 estrategias');
    assertNoLeaks(atCapEs.textContent ?? '');
  });

  it('theSelectedCountCarriesBothParams', () => {
    const el = render('en', ['1'], 4);
    expect(el.textContent).toContain('1 of 4 selected');
    expect(el.textContent).not.toContain('{{');
  });

  it('atTheCap_UnselectedRowsAreDisabledAndSelectedOnesStayEnabled', () => {
    const el = render('en', ['1', '2'], 2);
    expect(checkbox(el, 'Euro Trend').disabled).toBe(true);
    expect(checkbox(el, 'Gold Breakout').disabled).toBe(false);
    expect(checkbox(el, 'Gold Breakout').checked).toBe(true);
  });

  it('theCapComesFromTheInput_SoRaisingItEnablesTheRow', () => {
    const el = render('en', ['1', '2'], 3);
    expect(checkbox(el, 'Euro Trend').disabled).toBe(false);
  });

  it('selectingARow_EmitsTheNewSelectionAndNothingElse', () => {
    const el = render('en', ['1'], 3);
    const box = checkbox(el, 'Euro Trend');
    box.click();
    fixture.detectChanges();
    expect(emitted).toHaveLength(1);
    expect([...emitted[0]].sort()).toEqual(['1', '3']);
  });

  it('deselectingARowAtTheCap_EmitsTheSmallerSelection', () => {
    const el = render('en', ['1', '2'], 2);
    checkbox(el, 'Gold Breakout').click();
    expect(emitted).toHaveLength(1);
    expect([...emitted[0]]).toEqual(['2']);
  });

  it('theSymbolFilterAndNameSearchNarrowTheRows_AndKeepTheSelectionCount', () => {
    const el = render('en', ['3'], 3);
    const select = el.querySelector<HTMLSelectElement>('select')!;
    select.value = 'XAUUSD';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(rows(el)).toHaveLength(2);

    const search = el.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'reversal';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(rows(el)).toHaveLength(1);
    expect(rows(el)[0].textContent).toContain('Gold Reversal');
    // the hidden selected member still counts
    expect(el.textContent).toContain('1 of 3 selected');
    expect(emitted).toHaveLength(0);
  });

  it('noMatchingRows_ShowsTheTranslatedEmptyState', () => {
    const el = render('en', [], 2, []);
    expect(el.textContent).toContain(PICKER_EN['EMPTY']);
  });

  it('aNullSymbolRendersTheTranslatedNoSymbolLabel', () => {
    const el = render('en', [], 2, [candidate('9', 'No Symbol Strategy', null)]);
    expect(rows(el)[0].textContent).toContain(PICKER_EN['NO_SYMBOL']);
  });
});
