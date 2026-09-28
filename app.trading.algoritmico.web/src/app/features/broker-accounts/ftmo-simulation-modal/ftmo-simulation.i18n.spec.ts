import { describe, expect, it } from 'vitest';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

/**
 * Scopes the parity and banned-wording checks to the FTMO_SIMULATION namespace only (spec.md
 * "Banned Wording Is Excluded From The FTMO i18n Keys"; hard rule 3/4). The SQX pipeline keys
 * legitimately use "passed" (e.g. `SQX.WORKFLOW.PASSED`) and MUST NOT be touched by this sweep.
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
  'habria aprobado',
];

/** Lowercases and strips diacritics, matching the banned-wording sweep's normalization. */
function normalize(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
}

describe('FTMO_SIMULATION i18n', () => {
  const enFlat = flatten((en as JsonTree)['FTMO_SIMULATION'] ?? {});
  const esFlat = flatten((es as JsonTree)['FTMO_SIMULATION'] ?? {});

  it('every FTMO_SIMULATION key present in en.json has a matching key in es.json, and vice versa', () => {
    const enKeys = Object.keys(enFlat).sort();
    const esKeys = Object.keys(esFlat).sort();
    expect(enKeys.length).toBeGreaterThan(0);
    expect(enKeys).toEqual(esKeys);
  });

  it('no FTMO_SIMULATION key text contains banned survival/pass wording', () => {
    const offenders: string[] = [];
    for (const [locale, flat] of [
      ['en', enFlat],
      ['es', esFlat],
    ] as const) {
      for (const [key, value] of Object.entries(flat)) {
        const normalized = normalize(value);
        for (const banned of BANNED_SUBSTRINGS) {
          const bannedNormalized = normalize(banned);
          const wordBoundary = new RegExp(`\\b${bannedNormalized.replace(/\s+/g, '\\s+')}\\b`);
          if (wordBoundary.test(normalized)) {
            offenders.push(`${locale}:${key} contains "${banned}"`);
          }
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it('an SQX pipeline key using banned-adjacent wording is not scoped by the banned-wording check', () => {
    // SQX.WORKFLOW.PASSED legitimately contains "passed" and is out of scope for the FTMO sweep.
    expect((en as JsonTree)['SQX']?.['WORKFLOW']?.['PASSED']).toBeDefined();
    const sqxFlatKeys = Object.keys(flatten((en as JsonTree)['SQX'] ?? {}));
    expect(sqxFlatKeys.some((k) => k.includes('PASSED'))).toBe(true);
    // The FTMO-scoped flattened set never includes an SQX key.
    expect(Object.keys(enFlat).some((k) => k.startsWith('SQX'))).toBe(false);
  });
});
