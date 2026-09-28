import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FtmoOutcomeBarsComponent } from './ftmo-outcome-bars.component';
import {
  CHAIN_OUTCOME_ORDER,
  CHAIN_OUTCOME_LABELS,
  FtmoOutcomeRowVm,
} from '../ftmo-simulation.mappers';
import en from '../../../../../../public/assets/i18n/en.json';
import es from '../../../../../../public/assets/i18n/es.json';

function makeRows(
  overrides: Partial<Record<number, Partial<FtmoOutcomeRowVm>>> = {},
): FtmoOutcomeRowVm[] {
  return CHAIN_OUTCOME_ORDER.map((outcome, index) => ({
    outcome,
    labelKey: CHAIN_OUTCOME_LABELS[outcome],
    count: 10 + index,
    share: 0.1 + index * 0.01,
    isCensored: false,
    ...(overrides[outcome] ?? {}),
  }));
}

function resolveKey(dict: Record<string, unknown>, key: string): unknown {
  return key.split('.').reduce<unknown>((acc, part) => {
    if (acc && typeof acc === 'object' && part in (acc as Record<string, unknown>)) {
      return (acc as Record<string, unknown>)[part];
    }
    return undefined;
  }, dict);
}

/** Collapses template whitespace so the assertion reads the text a user sees. */
function visibleText(el: Element): string {
  return (el.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('FtmoOutcomeBarsComponent', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoOutcomeBarsComponent, TranslateModule.forRoot()],
    });
  });

  function create(rows: FtmoOutcomeRowVm[]): ComponentFixture<FtmoOutcomeBarsComponent> {
    const fixture = TestBed.createComponent(FtmoOutcomeBarsComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    return fixture;
  }

  it('renders_ExactlySixOutcomeRows_InTheFixedOrder', () => {
    const fixture = create(makeRows());
    const rowEls = fixture.nativeElement.querySelectorAll('.ftmo-outcome-bars__row');
    expect(rowEls.length).toBe(6);
    CHAIN_OUTCOME_ORDER.forEach((outcome, index) => {
      expect(rowEls[index].getAttribute('data-outcome')).toBe(String(outcome));
    });
  });

  it('everyShareBarLabel_ResolvesToAnI18nKeyPresentInBothLocales', () => {
    const rows = makeRows();
    rows.forEach((row) => {
      const enText = resolveKey(en, row.labelKey);
      const esText = resolveKey(es, row.labelKey);
      expect(enText).toBeTruthy();
      expect(esText).toBeTruthy();
    });
    const fixture = create(rows);
    const text = (fixture.nativeElement.textContent as string).trim();
    // No loader is configured in this test module, so ngx-translate renders the key itself —
    // asserting the exact key text is present is the proof each label resolves through translate,
    // not a hardcoded string (the `SOURCE_PLATFORM_UNDECLARED_NOTE` precedent).
    rows.forEach((row) => {
      expect(text).toContain(row.labelKey);
    });
  });

  describe('with the real en/es dictionaries', () => {
    // The REAL dictionaries (Dual-Entry precedent: portfolio-detail.component.spec.ts): only a real
    // dictionary exposes a `{{param}}` placeholder the template forgot to fill.
    beforeEach(() => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('en', en);
      translate.setTranslation('es', es);
      translate.use('en');
    });

    // Moved here from the no-dictionary block: the count/share now render through translate params,
    // so only a real dictionary shows the number (the old `toContain('0')` would read the key text).
    it('rendersZeroCountRow_StillShowsCountAndZeroPercentShare_NotOmitted', () => {
      const rows = makeRows({
        [CHAIN_OUTCOME_ORDER[0]]: { count: 0, share: 0 },
      });
      const fixture = create(rows);
      const rowEls = fixture.nativeElement.querySelectorAll('.ftmo-outcome-bars__row');
      expect(rowEls.length).toBe(6);
      expect(visibleText(rowEls[0])).toContain('Count: 0 Share: 0%');
    });

    it('everyRow_RendersLabelCountAndPercentShare_AsFinalText_WithNoPlaceholderLeak', () => {
      const rows = makeRows();
      const fixture = create(rows);
      const rowEls = fixture.nativeElement.querySelectorAll('.ftmo-outcome-bars__row');
      rows.forEach((row, index) => {
        const label = resolveKey(en, row.labelKey) as string;
        const percent = Math.round(row.share * 1000) / 10;
        expect(visibleText(rowEls[index])).toBe(`${label} Count: ${row.count} Share: ${percent}%`);
      });
      expect(visibleText(fixture.nativeElement)).not.toContain('{{');
    });

    it('inSpanish_RendersTheCountAndShareThroughTheirParams_WithNoPlaceholderLeak', () => {
      TestBed.inject(TranslateService).use('es');
      const rows = makeRows();
      const fixture = create(rows);
      const firstRow = fixture.nativeElement.querySelector('.ftmo-outcome-bars__row');
      const label = resolveKey(es, rows[0].labelKey) as string;
      expect(visibleText(firstRow)).toBe(`${label} Cantidad: 10 Porcentaje: 10%`);
      expect(visibleText(fixture.nativeElement)).not.toContain('{{');
    });
  });
});
