import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { FtmoSimulationService } from '../../core/services/ftmo-simulation.service';
import { TradingAccountService } from '../../core/services/trading-account.service';
import { FtmoGroupSimulationPageComponent } from './ftmo-group-simulation-page/ftmo-group-simulation-page.component';
import en from '../../../../public/assets/i18n/en.json';
import es from '../../../../public/assets/i18n/es.json';

/**
 * Scopes parity and banned-wording to the SIMULATOR namespace (tasks hard rules 6 and 10). Later
 * slices only extend this file's data, not its checks.
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

const BANNED_SUBSTRINGS = [
  'passed',
  'safe',
  'survived',
  'would have passed',
  'aprobado',
  'aprobó',
  'seguro',
  'sobrevivió',
  'habría aprobado',
];

/** Lowercases and strips diacritics, matching the banned-wording sweep's normalization. */
function normalize(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
}

function placeholders(text: string): string[] {
  return Array.from(text.matchAll(/{{\s*([\w.]+)\s*}}/g))
    .map((m) => m[1])
    .sort();
}

function offendersIn(flat: Record<string, string>, locale: string): string[] {
  const offenders: string[] = [];
  for (const [key, value] of Object.entries(flat)) {
    const normalized = normalize(value);
    for (const banned of BANNED_SUBSTRINGS) {
      const pattern = new RegExp(`\\b${normalize(banned).replace(/\s+/g, '\\s+')}\\b`);
      if (pattern.test(normalized)) offenders.push(`${locale}:${key} contains "${banned}"`);
    }
  }
  return offenders;
}

describe('SIMULATOR i18n', () => {
  const enFlat = flatten((en as JsonTree)['SIMULATOR'] ?? {});
  const esFlat = flatten((es as JsonTree)['SIMULATOR'] ?? {});

  it('theNamespaceExistsInBothLocales_WithMatchingKeys', () => {
    const enKeys = Object.keys(enFlat).sort();
    expect(enKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(Object.keys(esFlat).sort());
  });

  it('everyKeyUsesTheSamePlaceholderNamesInBothLocales', () => {
    for (const key of Object.keys(enFlat)) {
      expect(placeholders(esFlat[key] ?? ''), key).toEqual(placeholders(enFlat[key]));
    }
  });

  it('noSimulatorKeyTextContainsBannedSurvivalOrPassWording', () => {
    expect([...offendersIn(enFlat, 'en'), ...offendersIn(esFlat, 'es')]).toEqual([]);
  });

  it('falsification_TheSweepFlagsABannedWordInAnSpanishKey', () => {
    const tainted = { ...esFlat, 'NAV.TITLE': 'El grupo habría aprobado' };
    expect(offendersIn(tainted, 'es')).toContain('es:NAV.TITLE contains "habría aprobado"');
    const taintedEn = { ...enFlat, 'NAV.TITLE': 'Safe to run' };
    expect(offendersIn(taintedEn, 'en')).toEqual(['en:NAV.TITLE contains "safe"']);
  });

  it('falsification_ThePlaceholderCheckSeesAMissingParamName', () => {
    expect(placeholders('Max {{count}} of {{max}}')).toEqual(['count', 'max']);
    expect(placeholders('Max {{count}}')).not.toEqual(placeholders('Max {{count}} of {{max}}'));
  });
});

describe('FtmoGroupSimulationPageComponent shell with the real dictionaries', () => {
  function render(lang: 'en' | 'es'): string {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupSimulationPageComponent, TranslateModule.forRoot()],
      providers: [
        { provide: TradingAccountService, useValue: { getAll: () => of([]) } },
        { provide: FtmoSimulationService, useValue: { getGroupCandidates: () => of() } },
      ],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en);
    translate.setTranslation('es', es);
    translate.use(lang);
    const fixture = TestBed.createComponent(FtmoGroupSimulationPageComponent);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('rendersTheTitleAndTheAlwaysVisibleDisclosureInEnglish', () => {
    const text = render('en');
    expect(text).toContain((en as JsonTree)['SIMULATOR']['FTMO_GROUP']['TITLE']);
    expect(text).toContain((en as JsonTree)['SIMULATOR']['FTMO_GROUP']['DISCLOSURE']);
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  });

  it('rendersTheTitleAndTheAlwaysVisibleDisclosureInSpanish', () => {
    const text = render('es');
    expect(text).toContain((es as JsonTree)['SIMULATOR']['FTMO_GROUP']['TITLE']);
    expect(text).toContain((es as JsonTree)['SIMULATOR']['FTMO_GROUP']['DISCLOSURE']);
    expect(text).not.toContain('{{');
    expect(text).not.toContain('SIMULATOR.');
  });

  it('theTwoLocalesRenderDifferentText', () => {
    expect(render('en')).not.toBe(render('es'));
  });
});
