import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { FtmoGroupSearchService } from '../../core/services/ftmo-group-search.service';
import { FtmoSimulationService } from '../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../core/services/trading-account.service';
import { describe, expect, it } from 'vitest';
import en from '../../../../public/assets/i18n/en.json';
import es from '../../../../public/assets/i18n/es.json';
import { FtmoGroupSearchPageComponent } from './ftmo-group-search-page/ftmo-group-search-page.component';

/**
 * Sweeps the SIMULATOR.FTMO_SEARCH namespace (tasks 3a.7). The shared `simulator.i18n.spec.ts` already
 * covers parity and its banned list for the whole SIMULATOR tree; this file adds the search-specific
 * checks, "survival"/"supervivencia" and Spanish voseo included.
 */

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

function flatten(tree: JsonTree, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object') {
      Object.assign(out, flatten(value as JsonTree, path));
    } else {
      out[path] = String(value);
    }
  }
  return out;
}

function normalize(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
}

/** English and Spanish banned wording, "survival" included (the search is elimination risk, not survival). */
const BANNED = ['survival', 'survive', 'survived', 'supervivencia', 'sobrevivio', 'sobrevive'];

/** Rioplatense voseo forms the Spanish copy must not use. */
const VOSEO = ['vos', 'tenes', 'podes', 'queres', 'elegi', 'fijate', 'mira', 'segui', 'hace'];

function bannedIn(flat: Record<string, string>, locale: string): string[] {
  const hits: string[] = [];
  for (const [key, value] of Object.entries(flat)) {
    const text = normalize(value);
    for (const word of BANNED) {
      if (new RegExp(`\\b${word}\\b`).test(text)) hits.push(`${locale}:${key} contains "${word}"`);
    }
  }
  return hits;
}

function voseoIn(flat: Record<string, string>): string[] {
  const hits: string[] = [];
  for (const [key, value] of Object.entries(flat)) {
    const text = normalize(value);
    for (const word of VOSEO) {
      if (new RegExp(`\\b${word}\\b`).test(text)) hits.push(`es:${key} contains "${word}"`);
    }
  }
  return hits;
}

function placeholders(text: string): string[] {
  return Array.from(text.matchAll(/{{\s*([\w.]+)\s*}}/g))
    .map((m) => m[1])
    .sort();
}

const enSearch = flatten(
  (en as JsonTree)['SIMULATOR']['FTMO_SEARCH'] ?? {},
  'SIMULATOR.FTMO_SEARCH',
);
const esSearch = flatten(
  (es as JsonTree)['SIMULATOR']['FTMO_SEARCH'] ?? {},
  'SIMULATOR.FTMO_SEARCH',
);

describe('SIMULATOR.FTMO_SEARCH i18n', () => {
  it('theNamespaceExistsInBothLocales_WithTheSameKeys', () => {
    expect(Object.keys(enSearch).length).toBeGreaterThan(0);
    expect(Object.keys(enSearch).sort()).toEqual(Object.keys(esSearch).sort());
  });

  it('theNavEntryExistsInBothLocales', () => {
    expect((en as JsonTree)['SIMULATOR']['NAV']['FTMO_SEARCH']).toBeTruthy();
    expect((es as JsonTree)['SIMULATOR']['NAV']['FTMO_SEARCH']).toBeTruthy();
  });

  it('everyKeyUsesTheSamePlaceholderNamesInBothLocales', () => {
    for (const key of Object.keys(enSearch)) {
      expect(placeholders(esSearch[key] ?? ''), key).toEqual(placeholders(enSearch[key]));
    }
  });

  it('everyEnumValueHasAKeyInBothLocales_IncludingTheZeroUnknowns', () => {
    for (const group of ['STATUS', 'STAGE', 'STOP_REASON', 'INELIGIBLE_REASON']) {
      expect(enSearch[`SIMULATOR.FTMO_SEARCH.${group}.UNKNOWN`], group).toBeTruthy();
      expect(esSearch[`SIMULATOR.FTMO_SEARCH.${group}.UNKNOWN`], group).toBeTruthy();
    }
    expect(enSearch['SIMULATOR.FTMO_SEARCH.ERRORS.LOST']).toBeTruthy();
  });

  it('theScatterKeysExistInBothLocales_WithTheirPlaceholders', () => {
    const keys = ['TITLE', 'ARIA', 'X_AXIS', 'Y_AXIS', 'POINT_TITLE', 'NOT_PLOTTED', 'EMPTY'];
    for (const key of keys) {
      expect(enSearch[`SIMULATOR.FTMO_SEARCH.SCATTER.${key}`], key).toBeTruthy();
      expect(esSearch[`SIMULATOR.FTMO_SEARCH.SCATTER.${key}`], key).toBeTruthy();
    }
    expect(placeholders(enSearch['SIMULATOR.FTMO_SEARCH.SCATTER.POINT_TITLE'])).toEqual([
      'breach',
      'days',
      'members',
    ]);
    expect(placeholders(enSearch['SIMULATOR.FTMO_SEARCH.SCATTER.NOT_PLOTTED'])).toEqual(['count']);
  });

  it('noKeyOrTextUsesSurvivalOrSupervivenciaWording', () => {
    const keyHits = [...Object.keys(enSearch), ...Object.keys(esSearch)].filter((k) =>
      /surviv|supervivencia/i.test(k),
    );
    expect(keyHits).toEqual([]);
    expect([...bannedIn(enSearch, 'en'), ...bannedIn(esSearch, 'es')]).toEqual([]);
  });

  it('theSpanishCopyIsNeutral_NoVoseo', () => {
    expect(voseoIn(esSearch)).toEqual([]);
  });

  it('falsification_TheSweepsFlagASurvivalWordAndAVoseoForm', () => {
    expect(bannedIn({ 'A.B': 'Survival rate' }, 'en')).toEqual(['en:A.B contains "survival"']);
    expect(bannedIn({ 'A.B': 'La supervivencia' }, 'es')).toEqual([
      'es:A.B contains "supervivencia"',
    ]);
    expect(voseoIn({ 'A.B': 'Vos podés elegir' })).toEqual([
      'es:A.B contains "vos"',
      'es:A.B contains "podes"',
    ]);
  });

  it('everyTextResolvesWithItsParamsAndLeavesNoBraces', () => {
    for (const [locale, dictionary, flat] of [
      ['en', en, enSearch],
      ['es', es, esSearch],
    ] as const) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()] });
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation(locale, dictionary);
      translate.use(locale);
      for (const key of Object.keys(flat)) {
        const params: Record<string, string> = {};
        for (const name of placeholders(flat[key])) params[name] = 'X';
        const text = translate.instant(key, params);
        expect(text, `${locale}:${key}`).not.toContain('{{');
        expect(text, `${locale}:${key}`).not.toBe(key);
      }
    }
  });
});

describe('FtmoGroupSearchPageComponent shell with the real dictionaries', () => {
  function render(lang: 'en' | 'es'): string {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSearchPageComponent, TranslateModule.forRoot()],
      providers: [
        { provide: FtmoGroupSearchService, useValue: { getCurrentGroupSearch: () => of(null) } },
        { provide: TradingAccountService, useValue: { getAll: () => of([]) } },
        { provide: FtmoSimulationService, useValue: {} },
      ],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en);
    translate.setTranslation('es', es);
    translate.use(lang);
    const fixture = TestBed.createComponent(FtmoGroupSearchPageComponent);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it.each([
    ['en', en],
    ['es', es],
  ] as const)('rendersTheTitleAndTheAlwaysVisibleDisclosure_%s', (lang, dictionary) => {
    const text = render(lang);
    const tree = (dictionary as JsonTree)['SIMULATOR']['FTMO_SEARCH'];
    expect(tree['TITLE']).toBeTruthy();
    expect(tree['DISCLOSURE']).toBeTruthy();
    expect(text).toContain(tree['TITLE']);
    expect(text).toContain(tree['DISCLOSURE']);
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  });

  it('theTwoLocalesRenderDifferentText', () => {
    expect(render('en')).not.toBe(render('es'));
  });
});
