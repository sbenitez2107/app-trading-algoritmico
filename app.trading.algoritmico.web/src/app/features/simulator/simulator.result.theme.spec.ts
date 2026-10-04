import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { describe, expect, it } from 'vitest';
import { FtmoGroupRefusal } from '../../core/models/ftmo-group-simulation.model';
import { BacktestRunKind } from '../../core/services/backtest.service';
import { FtmoGroupResultComponent } from './ftmo-group-result/ftmo-group-result.component';
import { groupResult, refusedKind, successKind } from './ftmo-group-simulation.result.fixtures';
import { toGroupResultVm } from './ftmo-group-simulation.result.mappers';

/**
 * F3.5 theme check for the result component (new file: the committed `simulator.theme.spec.ts` is untouched).
 * The component is rendered with a success and a refusal so its stylesheet is injected, then every rule that
 * belongs to it is checked: theme variables only, no colour literal, no undeclared variable.
 */

const COLOUR_LITERAL = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/;
const VARIABLES_FILE = '/src/styles/_variables.scss';

async function readThemeFile(): Promise<string> {
  const specifier = 'node:fs';
  const fs = (await import(/* @vite-ignore */ specifier)) as {
    readFileSync: (path: string, encoding: 'utf8') => string;
  };
  const proc = (globalThis as unknown as { process: { cwd: () => string } }).process;
  return fs.readFileSync(proc.cwd() + VARIABLES_FILE, 'utf8');
}

function declaredIn(scss: string): Set<string> {
  return new Set(Array.from(scss.matchAll(/^\s*(--[\w-]+)\s*:/gm)).map((m) => m[1]));
}

function variablesRead(css: string): string[] {
  return Array.from(css.matchAll(/var\(\s*(--[\w-]+)/g)).map((m) => m[1]);
}

function resultCss(): string {
  return Array.from(document.head.querySelectorAll('style'))
    .map((s) => s.textContent ?? '')
    .join('\n')
    .split('}')
    .filter((rule) => /ftmo-group-result/.test(rule))
    .join('}\n');
}

describe('simulator result theming', () => {
  let declared: Set<string>;

  beforeAll(async () => {
    declared = declaredIn(await readThemeFile());
  });

  // Angular drops a component's styles when its fixture is destroyed, so render before every test.
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [FtmoGroupResultComponent, TranslateModule.forRoot()],
    });
    const fixture = TestBed.createComponent(FtmoGroupResultComponent);
    fixture.componentRef.setInput(
      'vm',
      toGroupResultVm(
        groupResult([
          successKind(BacktestRunKind.Deploy),
          refusedKind(BacktestRunKind.Evaluation, FtmoGroupRefusal.NoCommonWindow),
        ]),
      ),
    );
    fixture.detectChanges();
  });

  it('theResultStylesheetIsPartOfTheCheckedCss', () => {
    expect(resultCss()).toContain('.ftmo-group-result__slots');
  });

  it('theResultStylesheetContainsNoColourLiteral_FallbacksIncluded', () => {
    expect(COLOUR_LITERAL.test(resultCss())).toBe(false);
  });

  it('everyVariableTheResultStylesheetReadsIsDeclaredInTheThemeFile', () => {
    const used = variablesRead(resultCss());
    expect(used.length).toBeGreaterThan(0);
    expect(used.filter((name) => !declared.has(name))).toEqual([]);
  });

  it('falsification_TheCheckFlagsAHardcodedColourAndAnUndeclaredVariable', () => {
    expect(COLOUR_LITERAL.test('.ftmo-group-result { color: #f00; }')).toBe(true);
    const css = '.ftmo-group-result { color: var(--color-nope); }';
    expect(variablesRead(css).filter((n) => !declared.has(n))).toEqual(['--color-nope']);
  });
});
