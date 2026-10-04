import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import { FtmoGroupCandidateDto } from '../../../core/models/ftmo-group-simulation.model';
import { FtmoGroupPickerComponent } from './ftmo-group-picker.component';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

function candidate(strategyId: string, name: string, symbol: string | null): FtmoGroupCandidateDto {
  return {
    strategyId,
    name,
    symbol,
    deploy: null,
    evaluation: null,
    nameExistsOnOtherAccount: false,
  } as unknown as FtmoGroupCandidateDto;
}

const CANDIDATES = [
  candidate('1', 'Gold Breakout', 'XAUUSD'),
  candidate('2', 'Gold Reversal', 'XAUUSD'),
  candidate('3', 'Euro Trend', 'EURUSD'),
  candidate('4', 'Nasdaq Swing', null),
];

describe('FtmoGroupPickerComponent selected strategies list', () => {
  let fixture: ComponentFixture<FtmoGroupPickerComponent>;
  let emitted: ReadonlySet<string>[];

  function render(lang: 'en' | 'es', selected: string[], maxMembers = 4): HTMLElement {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupPickerComponent, TranslateModule.forRoot()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en);
    translate.setTranslation('es', es);
    translate.use(lang);
    fixture = TestBed.createComponent(FtmoGroupPickerComponent);
    fixture.componentRef.setInput('candidates', CANDIDATES);
    fixture.componentRef.setInput('maxMembers', maxMembers);
    fixture.componentRef.setInput('selectedIds', new Set(selected));
    emitted = [];
    fixture.componentInstance.selectionChange.subscribe((s) => emitted.push(s));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function search(el: HTMLElement, value: string): void {
    const input = el.querySelector<HTMLInputElement>('input[type="search"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function items(el: HTMLElement): HTMLElement[] {
    return Array.from(el.querySelectorAll<HTMLElement>('.ftmo-group-picker__selected-item'));
  }

  function assertNoLeaks(text: string): void {
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  }

  it('keepsEverySelectedStrategyListed_WhenTheSearchHidesAllRows_AndRemovingFreesASlot', () => {
    const el = render('en', ['1', '2', '3', '4']);
    search(el, 'zzz-no-match');
    expect(el.querySelectorAll('tbody tr').length).toBe(0);
    const listed = items(el);
    expect(listed.length).toBe(4);
    expect(listed[0].textContent).toContain('Gold Breakout');
    expect(listed[0].textContent).toContain('XAUUSD');

    const remove = listed[2].querySelector<HTMLButtonElement>('button')!;
    expect(remove.getAttribute('aria-label')).toContain('Euro Trend');
    remove.click();
    expect(emitted.length).toBe(1);
    expect(Array.from(emitted[0]).sort()).toEqual(['1', '2', '4']);
    expect(emitted[0].size).toBeLessThan(4);
  });

  it('listsSelectedStrategiesOutsideTheSymbolFilter', () => {
    const el = render('en', ['1', '3'], 4);
    const select = el.querySelector<HTMLSelectElement>('select')!;
    select.value = 'XAUUSD';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(items(el).map((i) => i.textContent)).toEqual([
      expect.stringContaining('Gold Breakout'),
      expect.stringContaining('Euro Trend'),
    ]);
  });

  it('suggestsClearingTheSearch_WhenAtTheCapAndNoRowsShow', () => {
    const el = render('en', ['1', '2'], 2);
    expect(el.textContent).not.toContain(en.SIMULATOR.FTMO_GROUP.PICKER.CAP_CLEAR_SEARCH);
    search(el, 'zzz-no-match');
    expect(el.textContent).toContain(en.SIMULATOR.FTMO_GROUP.PICKER.CAP_CLEAR_SEARCH);
  });

  it('showsAHintAndNoItems_WhenNothingIsSelected', () => {
    const el = render('en', []);
    expect(items(el).length).toBe(0);
    expect(el.textContent).toContain(en.SIMULATOR.FTMO_GROUP.PICKER.SELECTED_NONE);
  });

  it('rendersTranslatedCopyWithoutLeaks_InBothLanguages', () => {
    for (const lang of ['en', 'es'] as const) {
      const el = render(lang, ['1', '2'], 2);
      search(el, 'zzz-no-match');
      assertNoLeaks(el.textContent ?? '');
      const label = items(el)[0].querySelector('button')!.getAttribute('aria-label')!;
      expect(label).toContain('Gold Breakout');
      assertNoLeaks(label);
    }
  });
});
