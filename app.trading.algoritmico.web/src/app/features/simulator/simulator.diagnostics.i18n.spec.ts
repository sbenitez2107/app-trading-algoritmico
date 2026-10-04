import { describe, expect, it } from 'vitest';
import en from '../../../../public/assets/i18n/en.json';
import es from '../../../../public/assets/i18n/es.json';

/**
 * F4.4: parity, banned-wording and no-correlation checks scoped to the diagnostics keys. The namespace-wide
 * sweeps in `simulator.i18n.spec.ts` already scan these keys; this file proves they are present and pins the
 * diagnostics-specific rules (no correlation wording, neutral elimination wording) with its own falsification.
 */

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type JsonTree = { [key: string]: any };

function flatten(tree: JsonTree, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object')
      Object.assign(out, flatten(value as JsonTree, path));
    else out[path] = String(value);
  }
  return out;
}

function normalize(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/\p{M}/gu, '');
}

const BANNED = ['passed', 'safe', 'survived', 'aprobado', 'seguro', 'sobrevivio', 'correl'];

function offenders(flat: Record<string, string>): string[] {
  const found: string[] = [];
  for (const [key, value] of Object.entries(flat)) {
    const text = normalize(`${key} ${value}`);
    for (const word of BANNED) {
      if (new RegExp(`\\b${word}`).test(text)) found.push(`${key}: ${word}`);
    }
  }
  return found;
}

function placeholders(text: string): string[] {
  return Array.from(text.matchAll(/{{\s*([\w.]+)\s*}}/g))
    .map((m) => m[1])
    .sort();
}

describe('SIMULATOR.FTMO_GROUP.DIAGNOSTICS i18n (F4.4)', () => {
  const enFlat = flatten((en as JsonTree)['SIMULATOR']['FTMO_GROUP']['DIAGNOSTICS']);
  const esFlat = flatten((es as JsonTree)['SIMULATOR']['FTMO_GROUP']['DIAGNOSTICS']);

  it('bothLocalesCarryTheSameDiagnosticsKeys', () => {
    expect(Object.keys(enFlat).length).toBeGreaterThan(20);
    expect(Object.keys(enFlat).sort()).toEqual(Object.keys(esFlat).sort());
  });

  it('everyKeyUsesTheSamePlaceholdersInBothLocales', () => {
    for (const key of Object.keys(enFlat)) {
      expect(placeholders(esFlat[key]), key).toEqual(placeholders(enFlat[key]));
    }
  });

  it('noDiagnosticsKeyOrTextUsesPassSurvivalOrCorrelationWording', () => {
    expect(offenders(enFlat)).toEqual([]);
    expect(offenders(esFlat)).toEqual([]);
  });

  it('falsification_TheSweepFlagsASafeAndACorrelationKey', () => {
    expect(offenders({ 'PEAK.NOTE': 'This group is safe' })).toEqual(['PEAK.NOTE: safe']);
    expect(offenders({ 'CORRELATION.TITLE': 'Mapa' })).toEqual(['CORRELATION.TITLE: correl']);
    expect(offenders({ 'PEAK.NOTE': 'La correlación entre miembros' })).toEqual([
      'PEAK.NOTE: correl',
    ]);
  });
});
